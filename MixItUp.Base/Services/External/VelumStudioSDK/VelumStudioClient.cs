#nullable enable
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace Velum.Studio;

/// <summary>
/// A connection to Velum Studio's control API. Every action in Studio's
/// catalog is a typed method (<see cref="SceneSwitchAsync"/>), every event a
/// typed C# event (<see cref="SceneChanged"/>), and every state topic a typed
/// property of <see cref="State"/>.
/// </summary>
/// <remarks>
/// Events and state changes are raised on the connection's own reading
/// thread, in the order Studio sent them; hand long work off to your own.
/// With <see cref="VelumClientOptions.AutoReconnect"/> (the default) the
/// client reconnects when Studio restarts or the connection drops, finds
/// Studio again through the discovery file, and subscribes to the same
/// topics again.
/// </remarks>
public sealed partial class VelumStudioClient : IAsyncDisposable
{
    /// <summary>The WebSocket subprotocol Studio speaks.</summary>
    public const string Subprotocol = "velum.v1";

    private const int MaxMessage = 32 * 1024 * 1024;

    private readonly VelumClientOptions _options;
    private readonly SemaphoreSlim _send = new(1, 1);
    private readonly ConcurrentDictionary<long, Pending> _pending = new();
    private readonly HashSet<TopicType> _topics = new();
    private readonly CancellationTokenSource _life = new();
    private ClientWebSocket? _socket;
    private Task? _reader;
    private long _nextId;
    private int _disposed;

    private VelumStudioClient(VelumClientOptions options)
    {
        _options = options;
    }

    /// <summary>What Studio said when this connection opened (the latest one, after a reconnect).</summary>
    public StudioHello Hello { get; private set; } = StudioHello.Empty;

    /// <summary>The Studio instance this client is connected to (from the discovery file), or null for a paired device or an explicit address.</summary>
    public StudioInstance? Instance { get; private set; }

    /// <summary>The latest value of every topic subscribed to or fetched, with an event per topic.</summary>
    public StudioState State { get; } = new();

    /// <summary>Whether the connection is open now.</summary>
    public bool IsConnected => _socket?.State == WebSocketState.Open;

    /// <summary>Any event this SDK knows, typed: switch on <see cref="StudioEvent.Type"/> or pattern-match the record.</summary>
    public event EventHandler<StudioEvent>? EventReceived;

    /// <summary>A notification this version of the SDK doesn't know (a newer Studio's event), as its method and raw JSON.</summary>
    public event EventHandler<RawNotification>? UnknownNotification;

    /// <summary>The connection closed. With auto-reconnect on, the client is already trying again.</summary>
    public event EventHandler<DisconnectedEventArgs>? Disconnected;

    /// <summary>The client connected again (after Studio restarted, say) and subscribed to the same topics again.</summary>
    public event EventHandler<StudioHello>? Reconnected;

    /// <summary>One of your event handlers threw. The client carries on; this is where you hear about it.</summary>
    public event EventHandler<Exception>? HandlerFailed;

    /// <summary>
    /// Connect to Studio. With no options, the first running Studio on this
    /// computer, found through its discovery file.
    /// </summary>
    /// <exception cref="VelumConnectionException">No Studio could be reached.</exception>
    public static async Task<VelumStudioClient> ConnectAsync(VelumClientOptions? options = null, CancellationToken cancellationToken = default)
    {
        var client = new VelumStudioClient(options ?? new VelumClientOptions());
        await client.OpenAsync(cancellationToken).ConfigureAwait(false);
        client._reader = Task.Run(client.ReadLoopAsync);
        return client;
    }

    /// <summary>
    /// Pair this device with a Studio on the network, from the link in the
    /// QR code Studio shows (Settings › Remote control › Pair a device).
    /// Keep the result: connect with it from then on
    /// (<see cref="VelumClientOptions.Device"/>).
    /// </summary>
    /// <param name="link">The QR code's link.</param>
    /// <param name="deviceName">How Studio lists this device.</param>
    /// <param name="cancellationToken">Stops pairing.</param>
    /// <exception cref="VelumUnauthorizedException">The code is wrong or expired.</exception>
    /// <exception cref="VelumConnectionException">Studio couldn't be reached, or its certificate isn't the one in the link.</exception>
    public static async Task<PairedDevice> PairAsync(PairingLink link, string deviceName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(link);
        var endpoint = new Endpoint(new Uri($"wss://{Pairing.HostForUri(link.Host)}:{link.Port}/pair"), null, link.Fingerprint, null);
        var options = new VelumClientOptions { ClientName = deviceName, AutoReconnect = false };
        var client = new VelumStudioClient(options);
        await client.OpenAtAsync(endpoint, cancellationToken).ConfigureAwait(false);
        client._reader = Task.Run(client.ReadLoopAsync);
        try
        {
            var p = new JsonObject { ["code"] = link.Code, ["name"] = deviceName };
            var r = await client.CallAsync("pair.claim", p, cancellationToken).ConfigureAwait(false);
            return new PairedDevice
            {
                Host = link.Host,
                Port = link.Port,
                Fingerprint = link.Fingerprint,
                Token = r.GetProperty("token").GetString() ?? "",
                DeviceId = r.GetProperty("device_id").GetString() ?? "",
                Name = r.GetProperty("name").GetString() ?? deviceName,
                InstanceName = r.TryGetProperty("instance_name", out var n) ? n.GetString() ?? link.InstanceName : link.InstanceName,
            };
        }
        finally
        {
            await client.DisposeAsync().ConfigureAwait(false);
        }
    }

    // ── Topics ──

    /// <summary>
    /// Follow topics: Studio sends each one's value now and every change from
    /// then on (into <see cref="State"/>), plus the topic's events. When this
    /// returns, <see cref="State"/> holds each topic's value. Topics add up;
    /// this returns every topic followed now.
    /// </summary>
    public Task<IReadOnlyList<TopicType>> SubscribeAsync(params TopicType[] topics) => SubscribeAsync(topics, CancellationToken.None);

    /// <inheritdoc cref="SubscribeAsync(TopicType[])"/>
    public async Task<IReadOnlyList<TopicType>> SubscribeAsync(IEnumerable<TopicType> topics, CancellationToken cancellationToken)
    {
        var list = topics.Distinct().ToList();
        lock (_topics)
        {
            _topics.UnionWith(list);
        }
        var r = await CallAsync("subscribe", TopicParams(list, typed: true), cancellationToken).ConfigureAwait(false);
        // The first values follow the reply; reading them once more makes
        // them certain to be in State now (an unchanged value isn't raised twice).
        if (list.Count > 0)
        {
            await FetchStateAsync(list, cancellationToken).ConfigureAwait(false);
        }
        return ReadTopics(r);
    }

    /// <summary>Follow the topics that bring these events.</summary>
    public Task<IReadOnlyList<TopicType>> SubscribeAsync(params EventType[] events) =>
        SubscribeAsync(events.Select(e => e.Info().Topic), CancellationToken.None);

    /// <summary>Stop following topics. Returns every topic still followed.</summary>
    public async Task<IReadOnlyList<TopicType>> UnsubscribeAsync(IEnumerable<TopicType> topics, CancellationToken cancellationToken = default)
    {
        var list = topics.Distinct().ToList();
        lock (_topics)
        {
            _topics.ExceptWith(list);
        }
        var r = await CallAsync("unsubscribe", TopicParams(list, typed: false), cancellationToken).ConfigureAwait(false);
        return ReadTopics(r);
    }

    /// <summary>Read topics now (every topic when none are named) into <see cref="State"/>, raising their change events.</summary>
    public async Task<StudioState> FetchStateAsync(IEnumerable<TopicType>? topics = null, CancellationToken cancellationToken = default)
    {
        JsonObject? p = null;
        if (topics is not null)
        {
            p = new JsonObject { ["topics"] = new JsonArray(topics.Select(t => (JsonNode)JsonValue.Create(t.Id())!).ToArray()) };
        }
        // Applied on the reading thread as the reply arrives, so a value
        // pushed after it is never overwritten by this older one.
        await SendAsync("state.get", p is null ? null : w => p.WriteTo(w), cancellationToken, r =>
        {
            foreach (var prop in r.EnumerateObject())
            {
                State.Apply(prop.Name, prop.Value, this);
            }
        }).ConfigureAwait(false);
        return State;
    }

    // ── Actions ──

    /// <summary>
    /// Run an action chosen at run time (from a picker built on
    /// <see cref="ActionTypes.All"/>). <paramref name="parameters"/> is the
    /// action's parameters record (such as <see cref="SceneSwitchParams"/>),
    /// a <see cref="JsonObject"/>, or null for none. Returns the action's
    /// result record (such as <see cref="SceneSwitchResult"/>).
    /// </summary>
    /// <exception cref="ArgumentException">The parameters aren't a type this SDK can send.</exception>
    public async Task<object?> RunAsync(ActionType action, object? parameters = null, CancellationToken cancellationToken = default)
    {
        var json = await SendAsync(action.Id(), w => WriteParams(w, parameters), cancellationToken).ConfigureAwait(false);
        return ReadResult(action, json);
    }

    /// <summary>
    /// Run an action by its id, with JSON parameters, for an action newer
    /// than this SDK. Returns Studio's result as JSON.
    /// </summary>
    public Task<JsonElement> RunAsync(string actionId, JsonObject? parameters = null, CancellationToken cancellationToken = default) =>
        CallAsync(actionId, parameters, cancellationToken);

    /// <summary>The catalog, as Studio has it now (<c>catalog.list</c>).</summary>
    public Task<JsonElement> GetCatalogAsync(CancellationToken cancellationToken = default) => CallAsync("catalog.list", null, cancellationToken);

    /// <summary>Checks the connection: Studio answers at once.</summary>
    public Task PingAsync(CancellationToken cancellationToken = default) => CallAsync("ping", null, cancellationToken);

    /// <summary>
    /// Call any method of the protocol with JSON parameters and get its
    /// result as JSON. The typed methods are built on this.
    /// </summary>
    /// <exception cref="VelumException">Studio answered with an error (its own subclass per code).</exception>
    public Task<JsonElement> CallAsync(string method, JsonNode? parameters, CancellationToken cancellationToken = default) =>
        SendAsync(method, parameters is null ? null : w => parameters.WriteTo(w), cancellationToken);

    private async Task<TResult> InvokeAsync<TParams, TResult>(string method, TParams parameters, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var info = VelumJson.Info<TParams>();
        var json = await SendAsync(method, w => JsonSerializer.Serialize(w, parameters, info), cancellationToken).ConfigureAwait(false);
        return Read(json, VelumJson.Info<TResult>(), method);
    }

    private async Task<TResult> InvokeAsync<TResult>(string method, CancellationToken cancellationToken)
    {
        var json = await SendAsync(method, null, cancellationToken).ConfigureAwait(false);
        return Read(json, VelumJson.Info<TResult>(), method);
    }

    private static TResult Read<TResult>(JsonElement json, JsonTypeInfo<TResult> info, string method) =>
        json.Deserialize(info) ?? throw new VelumProtocolException($"{method} answered with nothing");

    private static void WriteParams(Utf8JsonWriter w, object? parameters)
    {
        switch (parameters)
        {
            case null:
                w.WriteStartObject();
                w.WriteEndObject();
                break;
            case JsonNode node:
                node.WriteTo(w);
                break;
            case JsonElement element:
                element.WriteTo(w);
                break;
            default:
                var info = VelumJsonContext.Default.GetTypeInfo(parameters.GetType())
                    ?? throw new ArgumentException($"{parameters.GetType().Name} isn't an action's parameters: pass the action's Params record or a JsonObject", nameof(parameters));
                JsonSerializer.Serialize(w, parameters, info);
                break;
        }
    }

    private static partial object? ReadResult(ActionType type, JsonElement result);

    private partial StudioEvent? ReadEvent(string method, JsonElement data);

    private partial void RaiseTyped(StudioEvent e);

    private static JsonObject TopicParams(IEnumerable<TopicType> topics, bool typed)
    {
        var o = new JsonObject { ["topics"] = new JsonArray(topics.Select(t => (JsonNode)JsonValue.Create(t.Id())!).ToArray()) };
        if (typed)
        {
            // Each event as its own notification (method = its id).
            o["typed_events"] = true;
        }
        return o;
    }

    private static IReadOnlyList<TopicType> ReadTopics(JsonElement r)
    {
        var list = new List<TopicType>();
        if (r.TryGetProperty("topics", out var topics))
        {
            foreach (var t in topics.EnumerateArray())
            {
                if (TopicTypes.TryParse(t.GetString() ?? "", out var type))
                {
                    list.Add(type);
                }
            }
        }
        return list;
    }

    // ── Requests ──

    private async Task<JsonElement> SendAsync(string method, Action<Utf8JsonWriter>? writeParams, CancellationToken cancellationToken, Action<JsonElement>? onResult = null)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        var socket = _socket;
        if (socket is null || socket.State != WebSocketState.Open)
        {
            throw new VelumConnectionException("not connected to Studio");
        }
        var id = Interlocked.Increment(ref _nextId);
        var buffer = new ArrayBufferWriter<byte>();
        using (var w = new Utf8JsonWriter(buffer))
        {
            w.WriteStartObject();
            w.WriteString("jsonrpc", "2.0");
            w.WriteNumber("id", id);
            w.WriteString("method", method);
            if (writeParams is not null)
            {
                w.WritePropertyName("params");
                writeParams(w);
            }
            w.WriteEndObject();
        }
        var reply = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = new Pending(reply, onResult);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _life.Token);
        timeout.CancelAfter(_options.RequestTimeout);
        using var registration = timeout.Token.Register(() =>
        {
            if (_pending.TryRemove(id, out var pending))
            {
                var t = pending.Reply;
                if (cancellationToken.IsCancellationRequested)
                {
                    t.TrySetCanceled(cancellationToken);
                }
                else if (_life.IsCancellationRequested)
                {
                    t.TrySetException(new ObjectDisposedException(nameof(VelumStudioClient)));
                }
                else
                {
                    t.TrySetException(new TimeoutException($"Studio didn't answer {method} within {_options.RequestTimeout.TotalSeconds:0.#} s"));
                }
            }
        });
        await _send.WaitAsync(timeout.Token).ConfigureAwait(false);
        try
        {
            await socket.SendAsync(buffer.WrittenMemory, WebSocketMessageType.Text, true, timeout.Token).ConfigureAwait(false);
        }
        catch (Exception e) when (e is WebSocketException or IOException)
        {
            _pending.TryRemove(id, out _);
            throw new VelumConnectionException($"couldn't send {method}: {e.Message}", e);
        }
        finally
        {
            _send.Release();
        }
        return await reply.Task.ConfigureAwait(false);
    }

    // ── Connecting ──

    private async Task OpenAsync(CancellationToken cancellationToken)
    {
        Exception? last = null;
        foreach (var endpoint in Endpoints())
        {
            try
            {
                await OpenAtAsync(endpoint, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (Exception e) when (e is VelumConnectionException or WebSocketException or IOException or TimeoutException)
            {
                last = e;
            }
        }
        throw last as VelumConnectionException
            ?? new VelumConnectionException(last is null ? "no Velum Studio is running on this computer (its discovery file lists none)" : $"couldn't connect to Studio: {last.Message}", last);
    }

    /// <summary>Where to connect, in the order to try.</summary>
    private IEnumerable<Endpoint> Endpoints()
    {
        if (_options.Device is { } d)
        {
            yield return new Endpoint(new Uri($"wss://{Pairing.HostForUri(d.Host)}:{d.Port}/"), d.Token, d.Fingerprint, null);
            yield break;
        }
        if (_options.Url is { } url)
        {
            yield return new Endpoint(url, _options.TokenProvider?.Invoke() ?? _options.Token, _options.Fingerprint, null);
            yield break;
        }
        var running = StudioDiscovery.FindRunning(_options.DiscoveryPath);
        IEnumerable<StudioInstance> picked = running;
        if (_options.InstanceName is { } name)
        {
            picked = running.Where(i => string.Equals(i.InstanceName, name, StringComparison.OrdinalIgnoreCase));
        }
        else if (_options.Port is { } port)
        {
            picked = running.Where(i => i.Port == port);
        }
        else if (Instance is { } before)
        {
            // After a restart: the same Studio by name first, then the rest.
            picked = running.OrderBy(i => i.InstanceName == before.InstanceName ? 0 : 1);
        }
        foreach (var i in picked)
        {
            yield return new Endpoint(new Uri($"ws://127.0.0.1:{i.Port}/"), i.Token, null, i);
        }
    }

    private async Task OpenAtAsync(Endpoint endpoint, CancellationToken cancellationToken)
    {
        var socket = new ClientWebSocket();
        socket.Options.AddSubProtocol(Subprotocol);
        if (endpoint.Token is { } token)
        {
            socket.Options.SetRequestHeader("Authorization", "Bearer " + token);
        }
        socket.Options.SetRequestHeader("X-Velum-Client", HeaderSafe(_options.ClientName));
        if (endpoint.Fingerprint is { } pin)
        {
            // Studio's certificate is self-signed: it's trusted only when its
            // SHA-256 fingerprint is the one from pairing, whatever its name.
            socket.Options.RemoteCertificateValidationCallback = (_, cert, _, _) => cert is not null && Pairing.Matches(cert, pin);
        }
        using var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        connectTimeout.CancelAfter(_options.ConnectTimeout);
        try
        {
            await MixItUp.Base.Web.WebSocketConnectionFactory.ConnectAsync(socket, endpoint.Url, connectTimeout.Token).ConfigureAwait(false);
        }
        catch (Exception e) when (e is WebSocketException or IOException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            socket.Dispose();
            throw new VelumConnectionException(ConnectFailure(endpoint, e), e);
        }
        if (socket.SubProtocol is { } sp && sp != Subprotocol)
        {
            socket.Dispose();
            throw new VelumConnectionException($"Studio answered with the {sp} protocol, not {Subprotocol}");
        }
        // The hello comes first.
        StudioHello hello;
        try
        {
            var first = await ReceiveAsync(socket, connectTimeout.Token).ConfigureAwait(false)
                ?? throw new VelumConnectionException("Studio closed the connection before saying hello");
            using var doc = JsonDocument.Parse(first);
            var root = doc.RootElement;
            if (!root.TryGetProperty("method", out var m) || m.GetString() != "hello")
            {
                throw new VelumConnectionException("Studio didn't say hello first");
            }
            hello = StudioHello.From(root.GetProperty("params"));
        }
        catch (Exception e) when (e is not VelumConnectionException && !cancellationToken.IsCancellationRequested)
        {
            socket.Dispose();
            throw new VelumConnectionException($"no hello from Studio: {e.Message}", e);
        }
        Hello = hello;
        if (endpoint.Instance is not null)
        {
            Instance = endpoint.Instance;
        }
        _socket = socket;
    }

    private static string ConnectFailure(Endpoint endpoint, Exception e)
    {
        var why = e.Message;
        if (e is WebSocketException && why.Contains("401", StringComparison.Ordinal))
        {
            why = "Studio refused the token (401): it may have been regenerated in Settings › Remote control";
        }
        return $"couldn't connect to Studio at {endpoint.Url}: {why}";
    }

    /// <summary>Header values are plain ASCII.</summary>
    private static string HeaderSafe(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            sb.Append(c is >= ' ' and <= '~' ? c : '_');
        }
        return sb.Length == 0 ? "Velum SDK" : sb.ToString();
    }

    /// <summary>One whole text message, or null when the socket closed.</summary>
    private static async Task<byte[]?> ReceiveAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        try
        {
            using var message = new MemoryStream();
            while (true)
            {
                var r = await socket.ReceiveAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                if (r.MessageType == WebSocketMessageType.Close)
                {
                    return null;
                }
                message.Write(buffer, 0, r.Count);
                if (message.Length > MaxMessage)
                {
                    throw new VelumProtocolException("a message from Studio was too large");
                }
                if (r.EndOfMessage)
                {
                    return message.ToArray();
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    // ── Reading ──

    private async Task ReadLoopAsync()
    {
        while (!_life.IsCancellationRequested)
        {
            var socket = _socket;
            string reason = "the connection closed";
            Exception? error = null;
            if (socket is not null)
            {
                try
                {
                    while (true)
                    {
                        var message = await ReceiveAsync(socket, _life.Token).ConfigureAwait(false);
                        if (message is null)
                        {
                            reason = socket.CloseStatusDescription is { Length: > 0 } d ? d : "Studio closed the connection";
                            break;
                        }
                        Handle(message);
                    }
                }
                catch (OperationCanceledException) when (_life.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception e)
                {
                    error = e;
                    reason = e.Message;
                }
            }
            FailPending(new VelumConnectionException($"the connection to Studio closed: {reason}", error));
            socket?.Dispose();
            _socket = null;
            if (_life.IsCancellationRequested)
            {
                return;
            }
            var reconnecting = _options.AutoReconnect;
            Raise(Disconnected, new DisconnectedEventArgs(reason, error, reconnecting));
            if (!reconnecting || !await ReconnectAsync().ConfigureAwait(false))
            {
                return;
            }
        }
    }

    /// <summary>Try again until connected (or disposed), then follow the same topics.</summary>
    private async Task<bool> ReconnectAsync()
    {
        var delay = _options.ReconnectDelay;
        while (!_life.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(delay, _life.Token).ConfigureAwait(false);
                await OpenAsync(_life.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_life.IsCancellationRequested)
            {
                return false;
            }
            catch (VelumConnectionException)
            {
                delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 2, _options.MaxReconnectDelay.TotalMilliseconds));
                continue;
            }
            // Read replies again before subscribing (the subscribe waits for one).
            _ = Task.Run(async () =>
            {
                List<TopicType> topics;
                lock (_topics)
                {
                    topics = _topics.ToList();
                }
                try
                {
                    if (topics.Count > 0)
                    {
                        await CallAsync("subscribe", TopicParams(topics, typed: true), _life.Token).ConfigureAwait(false);
                    }
                    Raise(Reconnected, Hello);
                }
                catch (Exception e) when (e is VelumException or VelumConnectionException or TimeoutException or OperationCanceledException)
                {
                    // The connection dropped again; the read loop reconnects.
                }
            });
            return true;
        }
        return false;
    }

    private void FailPending(Exception e)
    {
        foreach (var id in _pending.Keys.ToList())
        {
            if (_pending.TryRemove(id, out var t))
            {
                t.Reply.TrySetException(e);
            }
        }
    }

    private void Handle(byte[] message)
    {
        using var doc = JsonDocument.Parse(message);
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var one in root.EnumerateArray())
            {
                HandleOne(one);
            }
            return;
        }
        HandleOne(root);
    }

    private void HandleOne(JsonElement m)
    {
        if (m.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.Number && (m.TryGetProperty("result", out _) || m.TryGetProperty("error", out _)))
        {
            if (_pending.TryRemove(idProp.GetInt64(), out var pending))
            {
                if (m.TryGetProperty("error", out var err))
                {
                    var code = err.TryGetProperty("code", out var c) ? c.GetInt32() : (int)ErrorCode.Internal;
                    var text = err.TryGetProperty("message", out var t) ? t.GetString() ?? "" : "";
                    JsonElement? data = err.TryGetProperty("data", out var d) ? d.Clone() : null;
                    pending.Reply.TrySetException(VelumException.FromError(code, text, data));
                }
                else
                {
                    var result = m.GetProperty("result").Clone();
                    pending.OnResult?.Invoke(result);
                    pending.Reply.TrySetResult(result);
                }
            }
            return;
        }
        if (!m.TryGetProperty("method", out var methodProp) || methodProp.GetString() is not { } method)
        {
            return;
        }
        var p = m.TryGetProperty("params", out var pp) ? pp : default;
        try
        {
            switch (method)
            {
                case "state":
                    State.Apply(p.GetProperty("topic").GetString() ?? "", p.GetProperty("value"), this);
                    return;
                case "hello":
                    Hello = StudioHello.From(p);
                    return;
            }
            var e = ReadEvent(method, p);
            if (e is null)
            {
                Raise(UnknownNotification, new RawNotification(method, p.ValueKind == JsonValueKind.Undefined ? default : p.Clone()));
                return;
            }
            try
            {
                RaiseTyped(e);
            }
            catch (Exception ex)
            {
                ReportHandler(ex);
            }
            Raise(EventReceived, e);
        }
        catch (JsonException ex)
        {
            ReportHandler(new VelumProtocolException($"couldn't read Studio's {method}: {ex.Message}", ex));
        }
    }

    private void Raise<T>(EventHandler<T>? handler, T args)
    {
        if (handler is null)
        {
            return;
        }
        try
        {
            handler(this, args);
        }
        catch (Exception e)
        {
            ReportHandler(e);
        }
    }

    internal void ReportHandler(Exception e)
    {
        try
        {
            HandlerFailed?.Invoke(this, e);
        }
        catch (Exception)
        {
            // A failing failure handler has nowhere left to report to.
        }
    }

    /// <summary>Closes the connection and stops reconnecting.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }
        _life.Cancel();
        var socket = _socket;
        if (socket is { State: WebSocketState.Open })
        {
            try
            {
                using var t = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", t.Token).ConfigureAwait(false);
            }
            catch (Exception e) when (e is WebSocketException or IOException or OperationCanceledException)
            {
                // Closing anyway.
            }
        }
        if (_reader is not null)
        {
            try
            {
                await _reader.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Stopped as asked.
            }
        }
        socket?.Dispose();
        FailPending(new ObjectDisposedException(nameof(VelumStudioClient)));
        _life.Dispose();
        _send.Dispose();
    }

    private sealed record Endpoint(Uri Url, string? Token, string? Fingerprint, StudioInstance? Instance);

    private sealed record Pending(TaskCompletionSource<JsonElement> Reply, Action<JsonElement>? OnResult);
}

/// <summary>Why the connection closed, and whether the client is reconnecting.</summary>
public sealed class DisconnectedEventArgs(string reason, Exception? error, bool reconnecting) : EventArgs
{
    /// <summary>Why, in plain English.</summary>
    public string Reason { get; } = reason;

    /// <summary>The error behind it, if there was one.</summary>
    public Exception? Error { get; } = error;

    /// <summary>The client is trying to connect again.</summary>
    public bool Reconnecting { get; } = reconnecting;
}

/// <summary>A notification this SDK has no type for: its method and its params.</summary>
public sealed record RawNotification(string Method, JsonElement Params);

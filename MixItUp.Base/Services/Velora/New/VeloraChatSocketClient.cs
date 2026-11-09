using MixItUp.Base.Model;
using MixItUp.Base.Util;
using Newtonsoft.Json.Linq;
using SocketIOClient;
using SocketIOClient.Transport;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Base.Services.Velora.New
{
    /// <summary>
    /// Client-direct Socket.IO connection to Velora's Chat WebSocket (namespace <c>/chat</c>), owned by
    /// the streamer account (the bot has no token to authenticate a socket of its own; bot messages go
    /// over REST with sendAsBot). Replaces the chat (and chat-moderation) portion of the old
    /// webhook-relay path.
    /// </summary>
    public class VeloraChatSocketClient
    {
        // The chat gateway lives on the /chat namespace; connecting to the root disconnects within
        // seconds. SocketIOClient carries the namespace in the URL path.
        private const string ChatSocketURL = "wss://api.velora.tv/chat";

        // The gateway may drop an otherwise-idle socket, so emit a heartbeat well inside the 60-120s window.
        private const int HeartbeatIntervalSeconds = 90;

        private const int ConnectTimeoutSeconds = 20;

        private readonly VeloraClient dispatch;
        private readonly Func<string> accessTokenProvider;
        private readonly Func<string> channelIDProvider;

        // The streamer socket feeds received chat + moderation events into MIU; the bot socket is
        // send-only (registering the receive handlers on both would double-process every message).
        private readonly bool processIncomingEvents;

        private SocketIOClient.SocketIO socket;
        private readonly object socketLock = new object();

        private CancellationTokenSource heartbeatCancellationTokenSource;
        private TaskCompletionSource<bool> connectedSignal;

        public bool IsConnected
        {
            get
            {
                SocketIOClient.SocketIO current = this.socket;
                return current != null && current.Connected;
            }
        }

        public VeloraChatSocketClient(VeloraClient dispatch, Func<string> accessTokenProvider, Func<string> channelIDProvider, bool processIncomingEvents)
        {
            this.dispatch = dispatch;
            this.accessTokenProvider = accessTokenProvider;
            this.channelIDProvider = channelIDProvider;
            this.processIncomingEvents = processIncomingEvents;
        }

        public async Task<Result> Connect()
        {
            try
            {
                string token = this.accessTokenProvider?.Invoke();
                if (string.IsNullOrWhiteSpace(token))
                {
                    return new Result("No Velora access token available for the chat socket");
                }

                await this.DisconnectInternal();

                SocketIOClient.SocketIO newSocket = new SocketIOClient.SocketIO(ChatSocketURL, new SocketIOOptions
                {
                    // Auth payload is the gateway's recommended method; header/query are also accepted.
                    Auth = new { token = token },
                    Transport = TransportProtocol.WebSocket,
                    EIO = SocketIO.Core.EngineIO.V4,
                    Reconnection = true,
                    ReconnectionAttempts = int.MaxValue,
                    ReconnectionDelay = 2000,
                    ReconnectionDelayMax = 15000,
                });

                newSocket.ClientWebSocketProvider = () => new MixItUp.Base.Web.SocketIOWebSocket(newSocket.Options.RemoteCertificateValidationCallback);

                this.connectedSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                newSocket.OnConnected += this.Socket_OnConnected;
                newSocket.OnReconnected += this.Socket_OnReconnected;
                newSocket.OnDisconnected += this.Socket_OnDisconnected;
                newSocket.OnError += this.Socket_OnError;

                // commandResult surfaces send/slash rejections (500-char, sub-only, unverified email, ...).
                newSocket.On("commandResult", response => this.HandleSocketEvent("commandResult", response, this.OnCommandResult));

                if (this.processIncomingEvents)
                {
                    newSocket.On("newMessage", response => this.HandleSocketEvent("newMessage", response, this.dispatch.HandleChatSocketNewMessage));
                    newSocket.On("userTimedOut", response => this.HandleSocketEvent("userTimedOut", response, this.dispatch.HandleChatSocketUserTimedOut));
                    newSocket.On("userBanned", response => this.HandleSocketEvent("userBanned", response, this.dispatch.HandleChatSocketUserBanned));
                    newSocket.On("chatCleared", response => this.HandleSocketEvent("chatCleared", response, this.dispatch.HandleChatSocketChatCleared));
                    newSocket.On("moderationNotice", response => this.HandleSocketEvent("moderationNotice", response, this.dispatch.HandleChatSocketModerationNotice));
                    newSocket.On("viewer_count_update", response => this.HandleSocketEvent("viewer_count_update", response, this.dispatch.HandleChatSocketViewerCountUpdate));
                }

                lock (this.socketLock)
                {
                    this.socket = newSocket;
                }

                // ConnectAsync retries in the background per the reconnection options, so wait on the
                // OnConnected signal with a bound instead of blocking session init indefinitely.
                _ = newSocket.ConnectAsync();

                Task completed = await Task.WhenAny(this.connectedSignal.Task, Task.Delay(TimeSpan.FromSeconds(ConnectTimeoutSeconds)));
                if (completed == this.connectedSignal.Task && newSocket.Connected)
                {
                    return new Result();
                }

                return new Result("Timed out connecting to the Velora chat socket");
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                return new Result(ex);
            }
        }

        public async Task Disconnect()
        {
            await this.DisconnectInternal();
        }

        /// <summary>Reconnect after an OAuth token refresh so the handshake carries the new token.</summary>
        public async Task<Result> ReconnectWithFreshToken()
        {
            return await this.Connect();
        }

        public async Task SendMessage(string message, string effect = null, string effectColor = null, string replyToMessageID = null, string replyToUsername = null, string replyToSnippet = null)
        {
            SocketIOClient.SocketIO current = this.socket;
            if (current == null || !current.Connected)
            {
                throw new InvalidOperationException("Velora chat socket is not connected");
            }

            string channelID = this.channelIDProvider?.Invoke();

            Dictionary<string, object> payload = new Dictionary<string, object>
            {
                { "channelId", channelID },
                { "message", message },
                { "platform", "velora" },
            };
            if (!string.IsNullOrWhiteSpace(effect)) { payload["effect"] = effect; }
            if (!string.IsNullOrWhiteSpace(effectColor)) { payload["effectColor"] = effectColor; }
            if (!string.IsNullOrWhiteSpace(replyToMessageID))
            {
                Dictionary<string, object> replyTo = new Dictionary<string, object> { { "messageId", replyToMessageID } };
                if (!string.IsNullOrWhiteSpace(replyToUsername)) { replyTo["username"] = replyToUsername; }
                if (!string.IsNullOrWhiteSpace(replyToSnippet)) { replyTo["snippet"] = replyToSnippet; }
                payload["replyTo"] = replyTo;
            }

            await current.EmitAsync("sendMessage", payload);
        }

        /// <summary>
        /// Slash commands (e.g. <c>/announce</c>) are sent as an ordinary chat message whose body starts
        /// with '/'; the gateway executes them with the channel owner's permissions.
        /// </summary>
        public async Task SendSlashCommand(string command)
        {
            await this.SendMessage(command);
        }

        private async Task DisconnectInternal()
        {
            this.SetChatSocketActive(false);
            this.StopHeartbeat();

            SocketIOClient.SocketIO current;
            lock (this.socketLock)
            {
                current = this.socket;
                this.socket = null;
            }

            if (current != null)
            {
                try
                {
                    current.OnConnected -= this.Socket_OnConnected;
                    current.OnReconnected -= this.Socket_OnReconnected;
                    current.OnDisconnected -= this.Socket_OnDisconnected;
                    current.OnError -= this.Socket_OnError;

                    string channelID = this.channelIDProvider?.Invoke();
                    if (current.Connected && !string.IsNullOrWhiteSpace(channelID))
                    {
                        try { await current.EmitAsync("leaveChannel", new { channelId = channelID }); }
                        catch (Exception ex) { Logger.Log(ex); }
                    }

                    await current.DisconnectAsync();
                    current.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
            }
        }

        private void Socket_OnConnected(object sender, EventArgs e)
        {
            this.connectedSignal?.TrySetResult(true);
            this.SetChatSocketActive(true);
            this.JoinChannelAndStartHeartbeat();
        }

        private void Socket_OnReconnected(object sender, int attempt)
        {
            // joinChannel subscriptions are socket-lifetime scoped, so re-join after every reconnect.
            this.SetChatSocketActive(true);
            this.JoinChannelAndStartHeartbeat();
        }

        private void Socket_OnDisconnected(object sender, string reason)
        {
            // Open the relay-suppression gate while the socket is down so the webhook relay covers the gap.
            this.SetChatSocketActive(false);
            Logger.Log(LogLevel.Debug, $"Velora chat socket disconnected: {reason}");
        }

        // Only the streamer's receiving socket toggles the relay-suppression gate; the bot socket is send-only.
        private void SetChatSocketActive(bool active)
        {
            if (this.processIncomingEvents && this.dispatch != null)
            {
                this.dispatch.ChatSocketActive = active;
            }
        }

        private void Socket_OnError(object sender, string error)
        {
            Logger.Log(LogLevel.Error, $"Velora chat socket error: {error}");
        }

        private void JoinChannelAndStartHeartbeat()
        {
#pragma warning disable CS4014 // Fire-and-forget: the emit/heartbeat run on the socket's callback thread.
            AsyncRunner.RunAsyncBackground(async (cancellationToken) =>
            {
                try
                {
                    SocketIOClient.SocketIO current = this.socket;
                    string channelID = this.channelIDProvider?.Invoke();
                    if (current != null && current.Connected && !string.IsNullOrWhiteSpace(channelID))
                    {
                        await current.EmitAsync("joinChannel", new { channelId = channelID });
                    }
                    this.StartHeartbeat();
                }
                catch (Exception ex) { Logger.Log(ex); }
            }, CancellationToken.None);
#pragma warning restore CS4014
        }

        private void StartHeartbeat()
        {
            this.StopHeartbeat();

            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            this.heartbeatCancellationTokenSource = cancellationTokenSource;

#pragma warning disable CS4014 // Fire-and-forget heartbeat loop; cancelled on disconnect.
            AsyncRunner.RunAsyncBackground(async (_) =>
            {
                try
                {
                    while (!cancellationTokenSource.IsCancellationRequested)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(HeartbeatIntervalSeconds), cancellationTokenSource.Token);

                        SocketIOClient.SocketIO current = this.socket;
                        if (current != null && current.Connected)
                        {
                            await current.EmitAsync("heartbeat");
                        }
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) { Logger.Log(ex); }
            }, cancellationTokenSource.Token);
#pragma warning restore CS4014
        }

        private void StopHeartbeat()
        {
            CancellationTokenSource cancellationTokenSource = this.heartbeatCancellationTokenSource;
            this.heartbeatCancellationTokenSource = null;
            if (cancellationTokenSource != null)
            {
                try
                {
                    cancellationTokenSource.Cancel();
                    cancellationTokenSource.Dispose();
                }
                catch (Exception ex) { Logger.Log(ex); }
            }
        }

        private void HandleSocketEvent(string eventName, SocketIOResponse response, Func<JObject, Task> handler)
        {
            try
            {
                JObject payload = ParsePayload(response);
                if (payload != null)
                {
#pragma warning disable CS4014 // Fire-and-forget: the handler owns its own error handling.
                    this.RunHandler(eventName, payload, handler);
#pragma warning restore CS4014
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        private async Task RunHandler(string eventName, JObject payload, Func<JObject, Task> handler)
        {
            try
            {
                await handler(payload);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                Logger.Log(LogLevel.Error, $"Failed to handle Velora chat socket event '{eventName}'");
            }
        }

        private Task OnCommandResult(JObject payload)
        {
            // commandResult reports whether a sendMessage / slash command succeeded; log rejections so a
            // blocked message (unverified bot email, sub-only mode, blacklist, ...) is diagnosable.
            bool success = payload.GetValueOrDefault<bool>("success", true);
            if (!success)
            {
                string message = payload.GetValueOrDefault<string>("message", null) ?? payload.GetValueOrDefault<string>("error", null);
                Logger.Log(LogLevel.Error, $"Velora chat command rejected: {message}");
            }
            return Task.CompletedTask;
        }

        /// <summary>Bridge the SocketIOClient (System.Text.Json) payload into a Newtonsoft JObject so the
        /// existing defensive Velora models bind it exactly as the webhook payloads did.</summary>
        private static JObject ParsePayload(SocketIOResponse response)
        {
            if (response == null)
            {
                return null;
            }

            try
            {
                System.Text.Json.JsonElement element = response.GetValue<System.Text.Json.JsonElement>();
                string rawText = element.GetRawText();
                if (string.IsNullOrWhiteSpace(rawText))
                {
                    return null;
                }

                JToken token = JToken.Parse(rawText);
                return token as JObject;
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                return null;
            }
        }
    }
}

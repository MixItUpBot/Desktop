using MixItUp.Base.Model;
using MixItUp.Base.Util;
using Newtonsoft.Json.Linq;
using SocketIOClient;
using SocketIOClient.Transport;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Base.Services.Velora.New
{
    /// <summary>
    /// Client-direct Socket.IO connection to Velora's Events WebSocket (<c>wss://api.velora.tv/ws/events</c>).
    /// A valid token auto-subscribes to the authorizing user's own channel, so there is nothing to
    /// subscribe/re-subscribe. Replaces the webhook-relay path for non-chat real-time events.
    /// </summary>
    public class VeloraEventSocketClient
    {
        private const string EventSocketURL = "wss://api.velora.tv/ws/events";

        private const int ConnectTimeoutSeconds = 20;

        private readonly VeloraClient dispatch;
        private readonly Func<string> accessTokenProvider;

        private SocketIOClient.SocketIO socket;
        private readonly object socketLock = new object();

        private TaskCompletionSource<bool> connectedSignal;

        public bool IsConnected
        {
            get
            {
                SocketIOClient.SocketIO current = this.socket;
                return current != null && current.Connected;
            }
        }

        public VeloraEventSocketClient(VeloraClient dispatch, Func<string> accessTokenProvider)
        {
            this.dispatch = dispatch;
            this.accessTokenProvider = accessTokenProvider;
        }

        public async Task<Result> Connect()
        {
            try
            {
                string token = this.accessTokenProvider?.Invoke();
                if (string.IsNullOrWhiteSpace(token))
                {
                    return new Result("No Velora access token available for the events socket");
                }

                await this.DisconnectInternal();

                SocketIOClient.SocketIO newSocket = new SocketIOClient.SocketIO(EventSocketURL, new SocketIOOptions
                {
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

                // The gateway confirms auth + auto-subscription on the "connected" event.
                newSocket.On("connected", response => this.HandleSocketEvent("connected", response, this.OnConnectedEvent));
                // Single generic handler for ALL events: the { event, timestamp, data } envelope.
                newSocket.On("event", response => this.HandleSocketEvent("event", response, this.OnEventEnvelope));

                lock (this.socketLock)
                {
                    this.socket = newSocket;
                }

                _ = newSocket.ConnectAsync();

                Task completed = await Task.WhenAny(this.connectedSignal.Task, Task.Delay(TimeSpan.FromSeconds(ConnectTimeoutSeconds)));
                if (completed == this.connectedSignal.Task && newSocket.Connected)
                {
                    return new Result();
                }

                return new Result("Timed out connecting to the Velora events socket");
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

        private async Task DisconnectInternal()
        {
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
        }

        private void Socket_OnReconnected(object sender, int attempt)
        {
            // Auto-subscribed to our own channel, so there is nothing to re-subscribe after a reconnect.
            Logger.Log(LogLevel.Debug, $"Velora events socket reconnected (attempt {attempt})");
        }

        private void Socket_OnDisconnected(object sender, string reason)
        {
            Logger.Log(LogLevel.Debug, $"Velora events socket disconnected: {reason}");
        }

        private void Socket_OnError(object sender, string error)
        {
            Logger.Log(LogLevel.Error, $"Velora events socket error: {error}");
        }

        private Task OnConnectedEvent(JObject payload)
        {
            bool authenticated = payload.GetValueOrDefault<bool>("authenticated", false);
            bool autoSubscribed = payload.GetValueOrDefault<bool>("autoSubscribed", false);
            string channelUsername = payload.GetValueOrDefault<string>("channelUsername", null);

            if (!authenticated)
            {
                Logger.Log(LogLevel.Error, $"Velora events socket connected but NOT authenticated (channel: {channelUsername}); the access token may be invalid.");
            }
            else
            {
                Logger.Log(LogLevel.Debug, $"Velora events socket connected + authenticated for {channelUsername} (autoSubscribed: {autoSubscribed})");
            }
            return Task.CompletedTask;
        }

        private Task OnEventEnvelope(JObject envelope)
        {
            // Log every raw envelope at Debug: this is the only capture point for what the Events WS
            // actually delivers. There is no per-type subscription - a single generic "event" handler
            // receives them all and DispatchEvent routes by the "event" field - so an event whose name no
            // case matches (or an unexpected shape) otherwise falls through invisibly. This is the pipe to
            // watch to confirm whether Velora emits channel-point redemptions here at all, and under what
            // name. Mirrors the Chat WS newMessage raw-frame log.
            Logger.Log(LogLevel.Debug, $"Velora event: {envelope.ToString(Newtonsoft.Json.Formatting.None)}");

            // Envelope: { event, timestamp, data }.
            string eventType = envelope.GetValueOrDefault<string>("event", null);
            if (string.IsNullOrWhiteSpace(eventType))
            {
                return Task.CompletedTask;
            }

            envelope.TryGetJObject("data", out JObject data);
            return this.dispatch.HandleEventSocketEvent(eventType, data ?? new JObject());
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
                Logger.Log(LogLevel.Error, $"Failed to handle Velora events socket event '{eventName}'");
            }
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

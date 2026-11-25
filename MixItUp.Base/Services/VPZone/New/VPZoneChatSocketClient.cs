using MixItUp.Base.Model;
using MixItUp.Base.Model.VPZone.Realtime;
using MixItUp.Base.Util;
using MixItUp.Base.Web;
using Newtonsoft.Json;
using System;
using System.Globalization;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Base.Services.VPZone.New
{
    /// <summary>
    /// Client-direct connection to VPZone's chat gateway (wss://chat.vpzone.tv/ws). This is a plain
    /// WebSocket carrying newline-free JSON frames, and it is the primary path for everything that
    /// happens in a channel: messages, Pixel cheers, subscriptions, gifts, raids, clips, moderation
    /// actions and stream start/stop. There is nothing to poll.
    ///
    /// Connecting anonymously is supported and read-only. A token is only needed to send, so the
    /// streamer's socket authenticates and the bot's socket is a second one under the bot's own token.
    /// </summary>
    public class VPZoneChatSocketClient : ClientWebSocketBase
    {
        private const string ChatSocketBaseURL = "wss://chat.vpzone.tv/ws";

        // VPZone rate limits at 15 connections per 10 seconds per IP and answers a breach with close
        // code 1013, so reconnects back off rather than retrying immediately.
        private static readonly TimeSpan InitialReconnectDelay = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan MaximumReconnectDelay = TimeSpan.FromSeconds(60);

        // The gateway sends a presence frame at least every 30 seconds, so a socket that has gone
        // longer than this without any frame at all is treated as dead and rebuilt.
        private static readonly TimeSpan FrameSilenceTimeout = TimeSpan.FromSeconds(90);

        private readonly VPZoneClient dispatch;
        private readonly Func<string> accessTokenProvider;
        private readonly Func<string> channelSlugProvider;

        // The streamer socket feeds received frames into Mix It Up; the bot socket is send-only, since
        // registering both would process every frame twice.
        private readonly bool processIncomingEvents;

        /// <summary>
        /// The ts of the last frame handled, handed back as ?since= so VPZone replays only what was
        /// missed. That is what closes the gap on a reconnect without duplicating anything.
        ///
        /// Seeded with the current time rather than zero: without a cursor the gateway replays the
        /// whole live session's history on connect, which would dump an entire stream's backlog into
        /// chat the moment Mix It Up starts.
        /// </summary>
        private long lastFrameTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        private DateTimeOffset lastFrameReceived = DateTimeOffset.MinValue;

        private CancellationTokenSource monitorCancellationTokenSource;
        private readonly object connectionLock = new object();

        private bool shouldReconnect;
        private bool isBanned;

        /// <summary>
        /// When a ban is a timeout rather than a permanent ban, VPZone says so on the error frame that
        /// precedes the close. Holding the expiry lets the socket wait it out and come back on its own
        /// instead of staying dark until Mix It Up restarts. Null means permanent.
        /// </summary>
        private DateTimeOffset? bannedUntil;

        public bool IsConnected { get { return this.IsOpen(); } }

        /// <summary>Set when the gateway closed with 1008 for a ban, which must not be retried.</summary>
        public bool IsBanned { get { return this.isBanned; } }

        public VPZoneChatSocketClient(VPZoneClient dispatch, Func<string> accessTokenProvider, Func<string> channelSlugProvider, bool processIncomingEvents)
        {
            this.dispatch = dispatch;
            this.accessTokenProvider = accessTokenProvider;
            this.channelSlugProvider = channelSlugProvider;
            this.processIncomingEvents = processIncomingEvents;

            this.OnDisconnectOccurred += this.VPZoneChatSocketClient_OnDisconnectOccurred;
        }

        public async Task<Result> Connect()
        {
            this.shouldReconnect = true;
            this.isBanned = false;

            Result result = await this.ConnectInternal();
            if (result.Success)
            {
                this.StartConnectionMonitor();
            }
            return result;
        }

        private async Task<Result> ConnectInternal()
        {
            try
            {
                string channelSlug = this.channelSlugProvider?.Invoke();
                if (string.IsNullOrWhiteSpace(channelSlug))
                {
                    return new Result(Resources.VPZoneChatSocketNoChannel);
                }

                if (await this.Connect(this.BuildSocketURL(channelSlug)))
                {
                    this.lastFrameReceived = DateTimeOffset.Now;
                    this.SetChatSocketActive(true);
                    Logger.Log(LogLevel.Debug, $"VPZone {(this.processIncomingEvents ? "streamer" : "bot")} chat gateway connected for {channelSlug}.");
                    return new Result();
                }

                return new Result(Resources.VPZoneChatSocketConnectFailed);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                return new Result(ex);
            }
        }

        /// <summary>
        /// Builds the gateway URL. The channel slug is required; the token is omitted entirely for a
        /// read-only connection, which is a supported mode rather than a degraded one.
        /// </summary>
        private string BuildSocketURL(string channelSlug)
        {
            string url = $"{ChatSocketBaseURL}?channel={AdvancedHttpClient.URLEncodeString(channelSlug)}";

            string token = this.accessTokenProvider?.Invoke();
            if (!string.IsNullOrWhiteSpace(token))
            {
                url += "&token=" + AdvancedHttpClient.URLEncodeString(token);
            }

            if (this.lastFrameTimestamp > 0)
            {
                url += "&since=" + this.lastFrameTimestamp;
            }

            return url;
        }

        public async Task Disconnect()
        {
            this.shouldReconnect = false;
            this.StopConnectionMonitor();
            this.SetChatSocketActive(false);
            await this.Disconnect(WebSocketCloseStatus.NormalClosure);
        }

        /// <summary>Reconnect after an OAuth token refresh so the handshake carries the new token.</summary>
        public async Task<Result> ReconnectWithFreshToken()
        {
            this.StopConnectionMonitor();
            await this.Disconnect(WebSocketCloseStatus.NormalClosure);
            return await this.Connect();
        }

        /// <summary>
        /// Sends a message and returns the nonce it was tagged with. The gateway echoes that nonce on
        /// the resulting broadcast frame, so the app's own message can be matched without comparing
        /// text. Only the msg frame shape is accepted; anything else is ignored by the gateway.
        /// </summary>
        /// <param name="nonce">
        /// Supply this when the caller has to be listening for the echo before the send goes out, as
        /// pinning does. Left null, a fresh one is generated.
        /// </param>
        public async Task<string> SendMessage(string message, string replyToMessageID = null, string nonce = null)
        {
            if (!this.IsOpen())
            {
                throw new InvalidOperationException("VPZone chat socket is not connected");
            }

            VPZoneSendMessageModel frame = new VPZoneSendMessageModel()
            {
                Body = message,
                Nonce = string.IsNullOrWhiteSpace(nonce) ? Guid.NewGuid().ToString("N") : nonce,
                ReplyTo = string.IsNullOrWhiteSpace(replyToMessageID) ? null : replyToMessageID,
            };

            Logger.Log(LogLevel.Debug, $"VPZone chat send requested (nonce: {frame.Nonce}).");
            await this.Send(JsonConvert.SerializeObject(frame));
            return frame.Nonce;
        }

        protected override async Task ProcessReceivedPacket(string packet)
        {
            if (string.IsNullOrWhiteSpace(packet))
            {
                return;
            }

            this.lastFrameReceived = DateTimeOffset.Now;

            try
            {
                VPZoneChatEventModel frame = VPZoneChatEventModel.Parse(packet);
                if (frame == null || string.IsNullOrWhiteSpace(frame.Type))
                {
                    return;
                }

                // Advance the replay cursor for every frame that reaches this point, handled or not, so
                // a reconnect never replays something already seen.
                if (frame.Timestamp > this.lastFrameTimestamp)
                {
                    this.lastFrameTimestamp = frame.Timestamp;
                }

                // Error frames are addressed to this connection alone and report why its own send was
                // rejected, so they matter on the bot's send-only socket too.
                bool isError = string.Equals(frame.Type, VPZoneFrameTypes.Error, StringComparison.OrdinalIgnoreCase);
                if (Logger.Level == LogLevel.Debug && (this.processIncomingEvents || isError))
                {
                    Logger.Log(LogLevel.Debug, $"VPZone {(this.processIncomingEvents ? "streamer" : "bot")} gateway event received: {packet}");
                }
                if (isError)
                {
                    this.RecordBanExpiry(frame);
                    await this.dispatch.HandleErrorFrame(frame);
                    return;
                }

                if (this.processIncomingEvents)
                {
                    await this.dispatch.HandleFrame(frame);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                Logger.Log(LogLevel.Error, "Failed to handle a VPZone chat gateway frame");
            }
        }

        /// <summary>
        /// An error frame with code "banned" carries expires_at when the ban is a timeout. It arrives
        /// before the socket closes, so recording it here is what tells the close handler whether the
        /// ban is worth waiting out.
        /// </summary>
        private void RecordBanExpiry(VPZoneChatEventModel frame)
        {
            if (!string.Equals(frame.Code, VPZoneErrorCodes.Banned, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            this.bannedUntil = null;
            if (!string.IsNullOrWhiteSpace(frame.ExpiresAt)
                && DateTimeOffset.TryParse(frame.ExpiresAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset expiry))
            {
                this.bannedUntil = expiry.ToLocalTime();
            }
        }

        private void SetChatSocketActive(bool active)
        {
            if (this.processIncomingEvents && this.dispatch != null)
            {
                this.dispatch.ChatSocketActive = active;
            }
        }

        private void VPZoneChatSocketClient_OnDisconnectOccurred(object sender, WebSocketCloseStatus closeStatus)
        {
            this.SetChatSocketActive(false);

            // 1008 means the channel slug is invalid or this account is banned from the channel, and a
            // ban landing mid-session closes the socket the same way.
            if (closeStatus == WebSocketCloseStatus.PolicyViolation)
            {
                this.isBanned = true;

                // A timeout will lapse, so the socket waits it out rather than giving up for the rest
                // of the session. Anything else is permanent and retrying would just be refused.
                if (this.bannedUntil.HasValue && this.bannedUntil.Value > DateTimeOffset.Now)
                {
                    Logger.Log(LogLevel.Error, $"VPZone chat gateway timed this account out until {this.bannedUntil.Value:u}; reconnecting once it lapses.");
                    return;
                }

                this.shouldReconnect = false;
                Logger.Log(LogLevel.Error, "VPZone chat gateway rejected the connection (invalid channel or banned); not reconnecting.");
                return;
            }

            Logger.Log(LogLevel.Debug, $"VPZone chat gateway disconnected: {closeStatus}");
        }

        /// <summary>
        /// Watches the socket and rebuilds it when it drops or falls silent. VPZone's presence frame
        /// doubles as a 30 second keepalive, so prolonged silence means the connection is gone even
        /// when the socket has not reported a close.
        /// </summary>
        private void StartConnectionMonitor()
        {
            this.StopConnectionMonitor();

            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            lock (this.connectionLock)
            {
                this.monitorCancellationTokenSource = cancellationTokenSource;
            }

#pragma warning disable CS4014 // Fire-and-forget monitor loop; cancelled on disconnect.
            AsyncRunner.RunAsyncBackground(async (_) =>
            {
                TimeSpan reconnectDelay = InitialReconnectDelay;
                try
                {
                    while (!cancellationTokenSource.IsCancellationRequested)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), cancellationTokenSource.Token);

                        if (!this.shouldReconnect || cancellationTokenSource.IsCancellationRequested)
                        {
                            return;
                        }

                        // Sitting out a timeout. Reconnecting early is refused with another 1008, so
                        // the loop idles until it lapses and then picks up as normal.
                        if (this.bannedUntil.HasValue)
                        {
                            if (this.bannedUntil.Value > DateTimeOffset.Now)
                            {
                                continue;
                            }

                            this.bannedUntil = null;
                            this.isBanned = false;
                            Logger.Log(LogLevel.Debug, "VPZone timeout has lapsed; reconnecting to the chat gateway.");
                        }

                        bool silent = this.IsOpen() && (DateTimeOffset.Now - this.lastFrameReceived) > FrameSilenceTimeout;
                        if (this.IsOpen() && !silent)
                        {
                            reconnectDelay = InitialReconnectDelay;
                            continue;
                        }

                        if (silent)
                        {
                            Logger.Log(LogLevel.Debug, "VPZone chat gateway went quiet past the keepalive window; rebuilding the connection.");
                            await this.Disconnect(WebSocketCloseStatus.NormalClosure);
                        }

                        Result result = await this.ConnectInternal();
                        if (result.Success)
                        {
                            reconnectDelay = InitialReconnectDelay;
                            continue;
                        }

                        await Task.Delay(reconnectDelay, cancellationTokenSource.Token);
                        reconnectDelay = TimeSpan.FromTicks(Math.Min(reconnectDelay.Ticks * 2, MaximumReconnectDelay.Ticks));
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) { Logger.Log(ex); }
            }, cancellationTokenSource.Token);
#pragma warning restore CS4014
        }

        private void StopConnectionMonitor()
        {
            CancellationTokenSource cancellationTokenSource;
            lock (this.connectionLock)
            {
                cancellationTokenSource = this.monitorCancellationTokenSource;
                this.monitorCancellationTokenSource = null;
            }

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
    }
}

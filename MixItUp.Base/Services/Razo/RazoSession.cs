using MixItUp.Base.Model;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.User;
using MixItUp.Base.Model.User.Platform;
using MixItUp.Base.Model.Razo;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Chat;
using MixItUp.Base.ViewModel.User;
using MixItUp.Base.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Base.Services.Razo
{
    public class RazoSession : ChannelChatSessionBase
    {
        private static readonly Uri gateway = new Uri("wss://razo.tv/api/bot/v1/events");
        private static readonly HttpMessageInvoker transport = new HttpMessageInvoker(HttpConnectionFactory.CreateHandler(false));
        private static readonly HttpClient sharedClient = new HttpClient(HttpConnectionFactory.CreateHandler(false))
        {
            BaseAddress = new Uri("https://razo.tv/api/bot/v1/"), Timeout = TimeSpan.FromSeconds(10)
        };
        private readonly HttpClient client;
        public RazoSession() : this(sharedClient) { }
        internal RazoSession(HttpClient client) { this.client = client; }
        private (ClientWebSocket socket, JObject hello) pending;
        private HashSet<string> scopes = new HashSet<string>();
        private DateTimeOffset retryAt;
        private bool hasStreamState;
        private long streamRevision;
        private readonly SemaphoreSlim streamLock = new SemaphoreSlim(1);
        public override bool RequiresToken => true;
        public override bool RequiresChannelName => false;
        public override StreamingPlatformTypeEnum Platform => StreamingPlatformTypeEnum.Razo;
        public override int MaxMessageLength => 500;
        public bool CanDelete => this.IsConnected && this.scopes.Contains("chat:delete");

        private static async Task<JObject> Read(ClientWebSocket socket, CancellationToken cancel)
        {
            var buffer = new ArraySegment<byte>(new byte[4096]);
            using var packet = new MemoryStream();
            WebSocketReceiveResult part;
            do
            {
                part = await socket.ReceiveAsync(buffer, cancel);
                if (part.MessageType == WebSocketMessageType.Close) return null;
                if (part.MessageType != WebSocketMessageType.Text || packet.Length + part.Count > 262144)
                    throw new InvalidDataException("Invalid Razo event");
                packet.Write(buffer.Array, buffer.Offset, part.Count);
            } while (!part.EndOfMessage);
            return JObject.Parse(Encoding.UTF8.GetString(packet.GetBuffer(), 0, (int)packet.Length));
        }

        private async Task<(ClientWebSocket socket, JObject hello)> Open(string token, CancellationToken cancel)
        {
            if (DateTimeOffset.UtcNow < this.retryAt) throw new HttpRequestException("Razo rate limit");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
            timeout.CancelAfter(TimeSpan.FromSeconds(12));
            var socket = new ClientWebSocket();
            socket.Options.CollectHttpResponseDetails = true;
            socket.Options.SetRequestHeader("Authorization", "Bearer " + token);
            try
            {
                await socket.ConnectAsync(gateway, transport, timeout.Token);
                var frame = await Read(socket, timeout.Token);
                if (frame?.Value<string>("type") != "hello" || !(frame["data"] is JObject hello) ||
                    string.IsNullOrWhiteSpace(hello.Value<string>("channel")) || string.IsNullOrWhiteSpace(hello.Value<string>("bot")) ||
                    !(hello["scopes"] is JArray)) throw new InvalidDataException("Invalid Razo hello");
                return (socket, hello);
            }
            catch
            {
                var status = socket.HttpStatusCode;
                if (status == HttpStatusCode.TooManyRequests)
                {
                    int seconds = 60;
                    if (socket.HttpResponseHeaders?.TryGetValue("Retry-After", out var values) == true &&
                        int.TryParse(values.FirstOrDefault(), out int delay) && delay > 0) seconds = delay;
                    this.retryAt = DateTimeOffset.UtcNow.AddSeconds(seconds);
                }
                var close = (int?)socket.CloseStatus;
                socket.Dispose();
                if (close == 4001) throw new HttpRequestException("Razo token revoked", null, HttpStatusCode.Unauthorized);
                if (status == HttpStatusCode.Unauthorized || status == HttpStatusCode.Forbidden)
                    throw new HttpRequestException("Razo credentials rejected", null, status);
                throw;
            }
        }

        protected override async Task<string> VerifyChannel(string channel, string token, CancellationToken cancel)
        {
            this.pending = await this.Open(token, cancel);
            return this.pending.hello.Value<string>("channel");
        }

        protected override async Task Receive(string channel, string token, CancellationToken cancel)
        {
            var connection = this.pending;
            this.pending = default;
            this.hasStreamState = false;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var order = new Queue<string>();
            int delay = 1;
            while (!cancel.IsCancellationRequested)
            {
                try
                {
                    if (connection.socket == null) connection = await this.Open(token, cancel);
                    if (!string.Equals(connection.hello.Value<string>("channel"), channel, StringComparison.OrdinalIgnoreCase)) return;
                    this.scopes = new HashSet<string>(connection.hello["scopes"].Values<string>(), StringComparer.Ordinal);
                    this.BotUsername = connection.hello.Value<string>("bot");
                    this.BotID = RazoUserPlatformV2Model.GetUserID(this.BotUsername);
                    this.IsConnected = true;
                    this.IsBotConnected = this.scopes.Contains("chat:write");
                    this.ChannelLink = "https://razo.tv/watch/" + Uri.EscapeDataString(channel);
                    if (this.scopes.Contains("stream:read")) await this.RefreshDetails();
                    while (await Read(connection.socket, cancel) is JObject frame)
                    {
                        if (!(frame["data"] is JObject data)) continue;
                        string type = frame.Value<string>("type");
                        if (type == null || !this.scopes.Contains(EventScope(type)) ||
                            (data.Value<string>("channel") is string other && !string.Equals(other, channel, StringComparison.OrdinalIgnoreCase))) continue;
                        string id = data.Value<string>("id");
                        if (type == "follow.new") id = data.Value<string>("username") + ":" + data.Value<string>("followed_at");
                        if (id != null)
                        {
                            string key = (type == "chat.command" ? "chat.message" : type) + ":" + id;
                            if (!seen.Add(key)) continue;
                            order.Enqueue(key);
                            if (order.Count > 4096) seen.Remove(order.Dequeue());
                        }
                        await this.Handle(type, data);
                        delay = 1;
                    }
                    if ((int?)connection.socket.CloseStatus == 4001)
                        throw new HttpRequestException("Razo token revoked", null, HttpStatusCode.Unauthorized);
                    if ((int?)connection.socket.CloseStatus is 4000 or 4003)
                    {
                        this.IsConnected = false;
                        return;
                    }
                }
                catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized || ex.StatusCode == HttpStatusCode.Forbidden) { throw; }
                catch (OperationCanceledException) when (cancel.IsCancellationRequested) { return; }
                catch (Exception ex) { Logger.Log(LogLevel.Warning, $"Razo feed disconnected ({ex.GetType().Name})"); }
                finally
                {
                    connection.socket?.Dispose();
                    connection = default;
                    this.IsBotConnected = false;
                    this.BotID = null;
                    this.scopes = new HashSet<string>();
                }
                var backoff = TimeSpan.FromSeconds(Math.Max(6, delay));
                var providerDelay = this.retryAt - DateTimeOffset.UtcNow;
                await Task.Delay(backoff > providerDelay ? backoff : providerDelay, cancel);
                delay = Math.Min(60, delay * 2);
            }
        }

        internal static string EventScope(string type) => type switch
        {
            "chat.message" or "chat.command" or "chat.deleted" => "chat:read",
            "follow.new" => "followers:read",
            "stream.online" or "stream.offline" or "viewers.update" or "clip.created" or "clip.featured" => "stream:read",
            "razos.gift" or "sub.new" or "sub.gifted" or "raid.incoming" or "boost.new" or "jumpscare" => "alerts:read",
            _ => null
        };

        private async Task<JObject> Request(HttpMethod method, string path, object body = null)
        {
            string token = this.Token;
            if (token == null || this.retryAt > DateTimeOffset.UtcNow) throw new HttpRequestException("Razo request unavailable");
            using var request = new HttpRequestMessage(method, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body != null) request.Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
            using var response = await this.client.SendAsync(request);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                this.retryAt = response.Headers.RetryAfter?.Date ?? DateTimeOffset.UtcNow.Add(response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMinutes(1));
            if (response.StatusCode == HttpStatusCode.Unauthorized) await this.RejectActiveToken(token);
            response.EnsureSuccessStatusCode();
            string json = await response.Content.ReadAsStringAsync();
            return string.IsNullOrWhiteSpace(json) ? null : JObject.Parse(json);
        }

        public override async Task<Result> RefreshDetails()
        {
            if (!this.scopes.Contains("stream:read")) return new Result();
            string channel = this.ChannelID;
            long revision = Interlocked.Read(ref this.streamRevision);
            if (string.IsNullOrWhiteSpace(channel)) return new Result(false);
            var stream = await AsyncRunner.RunAsync(this.Request(HttpMethod.Get, "stream"));
            if (channel != this.ChannelID || !string.Equals((string)(stream?["channel"] as JValue), channel, StringComparison.OrdinalIgnoreCase))
                return new Result(false);
            return new Result(await this.ApplyStream(stream, revision));
        }

        private sealed class StreamDetails
        {
            public bool? Live { get; set; }
            public string Title { get; set; }
            public string Category { get; set; }
            [JsonProperty("thumbnail_url")] public string Thumbnail { get; set; }
            [JsonProperty("viewer_count")] public int? Viewers { get; set; }
            [JsonProperty("started_at")] public DateTimeOffset? Start { get; set; }
        }

        internal async Task<bool> ApplyStream(JObject data, long? expectedRevision = null)
        {
            StreamDetails stream;
            try { stream = data?.ToObject<StreamDetails>(); }
            catch (JsonException) { return false; }
            if (stream?.Live == null) return false;
            bool live = stream.Live.Value;
            bool changed;
            await this.streamLock.WaitAsync();
            try
            {
                if (expectedRevision.HasValue && expectedRevision.Value != this.streamRevision) return false;
                Interlocked.Increment(ref this.streamRevision);
                changed = this.hasStreamState && (this.IsLive != live || (live && stream.Start.HasValue && this.StreamStart != stream.Start.Value));
                this.IsLive = live;
                this.StreamTitle = stream.Title;
                this.StreamCategoryName = stream.Category;
                this.StreamCategoryImageURL = stream.Thumbnail;
                this.StreamViewerCount = stream.Viewers ?? 0;
                this.StreamStart = stream.Start ?? DateTimeOffset.MinValue;
                this.hasStreamState = true;
            }
            finally { this.streamLock.Release(); }
            if (changed) await ServiceManager.Get<EventService>().PerformEvent(live ? EventTypeEnum.RazoChannelStreamStart : EventTypeEnum.RazoChannelStreamStop, new CommandParametersModel(this.Platform));
            return true;
        }

        public override async Task SendMessage(string message, bool sendAsStreamer = false)
        {
            if (!this.IsBotConnected || string.IsNullOrWhiteSpace(message)) return;
            foreach (string text in this.SplitLargeMessage(message))
            {
                await this.Request(HttpMethod.Post, "chat", new { message = text });
            }
        }

        public override async Task DeleteMessage(ChatMessageViewModel message)
        {
            if (this.CanDelete && message is RazoChatMessage row && row.ID == RazoChatMessage.LocalID(this.ChannelID, row.ProviderID))
                await this.Request(HttpMethod.Delete, "chat/" + Uri.EscapeDataString(row.ProviderID));
        }

        private async Task Handle(string type, JObject data)
        {
            var chat = ServiceManager.Get<ChatService>();
            if (type == "chat.deleted")
            {
                if (data.Value<string>("message_id") is string id) await chat.DeleteMessage(RazoChatMessage.LocalID(this.ChannelID, id));
                return;
            }
            if (type == "stream.online" || type == "stream.offline") { await this.ApplyStream(data); return; }
            if (type == "viewers.update") { this.StreamViewerCount = data.Value<int?>("viewer_count") ?? this.StreamViewerCount; return; }
            if (type == "chat.message" || type == "chat.command" || type == "follow.new")
            {
                var author = RazoUserPlatformV2Model.FromChat(data, this.ChannelID);
                if (author == null) return;
                var user = await ServiceManager.Get<UserService>().CreateUser(author, matchUsername: false);
                if (user == null) return;
                var model = user.GetPlatformData<RazoUserPlatformV2Model>(this.Platform);
                if (type == "follow.new")
                {
                    model.FollowDate = data["followed_at"]?.ToObject<DateTimeOffset?>() ?? DateTimeOffset.Now;
                    model.Roles.Add(UserRoleEnum.Follower);
                    if (await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.RazoChannelFollowed, new CommandParametersModel(user, this.Platform)))
                        EventService.FollowOccurred(user);
                }
                else if (!string.IsNullOrWhiteSpace(data.Value<string>("id")))
                {
                    model.DisplayName = author.DisplayName;
                    if (model.Roles.Contains(UserRoleEnum.Follower)) author.Roles.Add(UserRoleEnum.Follower);
                    model.Roles = author.Roles;
                    model.RoleBadgeLink = author.RoleBadgeLink;
                    model.SubscriberBadgeLink = author.SubscriberBadgeLink;
                    model.SpecialtyBadgeLink = author.SpecialtyBadgeLink;
                    var row = new RazoChatMessage(this.ChannelID, data, user);
                    await chat.AddMessage(row);
                }
                return;
            }
            await this.HandleAlert(type, data);
        }

        private async Task HandleAlert(string type, JObject data)
        {
            string display = data.Value<string>("user");
            if (string.IsNullOrWhiteSpace(display) || string.Equals(display, "Someone", StringComparison.OrdinalIgnoreCase)) display = Resources.Anonymous;
            var actor = new UserV2ViewModel(this.Platform, new UserV2Model(new RazoUserPlatformV2Model
            {
                ID = "alert:" + data.Value<string>("id"), Username = display, DisplayName = display
            }));
            var parameters = new CommandParametersModel(actor, this.Platform) { DisplayOnlyActor = true };
            parameters.SpecialIdentifiers["razoeventid"] = data.Value<string>("id") ?? string.Empty;
            parameters.SpecialIdentifiers["razotest"] = data.Value<bool?>("test") == true ? "true" : "false";
            var events = ServiceManager.Get<EventService>();
            switch (type)
            {
                case "sub.new":
                    bool resub = data.Value<bool?>("resub") == true;
                    int? months = data.Value<int?>("months");
                    if (months.HasValue) parameters.SpecialIdentifiers["usersubmonths"] = months.Value.ToString();
                    if (await events.PerformEvent(resub ? EventTypeEnum.RazoChannelResubscribed : EventTypeEnum.RazoChannelSubscribed, parameters))
                    {
                        var details = new SubscriptionDetailsModel(this.Platform, actor, months: months ?? 0);
                        if (resub) EventService.ResubscribeOccurred(details);
                        else EventService.SubscribeOccurred(details);
                    }
                    break;
                case "sub.gifted":
                    if (data.Value<int?>("count") is int count && count > 0)
                    {
                        parameters.HasNoTarget = true;
                        parameters.SpecialIdentifiers["subsgiftedamount"] = count.ToString();
                        if (await events.PerformEvent(EventTypeEnum.RazoChannelMassSubscriptionsGifted, parameters))
                            EventService.NotifyRazoSubscriptionsGifted(actor, count);
                    }
                    break;
                case "raid.incoming":
                    if (data.Value<int?>("viewers") is int viewers && viewers >= 0)
                    {
                        parameters.SpecialIdentifiers["raidviewercount"] = parameters.SpecialIdentifiers["hostviewercount"] = viewers.ToString();
                        if (await events.PerformEvent(EventTypeEnum.RazoChannelRaided, parameters)) EventService.RaidOccurred(actor, viewers);
                    }
                    break;
                case "razos.gift":
                    if (data.Value<int?>("amount") is int amount && amount > 0)
                    {
                        string gift = data.Value<string>("gift") ?? string.Empty;
                        parameters.SpecialIdentifiers["cheeramount"] = parameters.SpecialIdentifiers["razosamount"] = amount.ToString();
                        parameters.SpecialIdentifiers["razogift"] = gift;
                        if (await events.PerformEvent(EventTypeEnum.RazoChannelGift, parameters))
                            EventService.RazoChannelGiftOccurred(new RazoGiftEventModel(actor, amount, gift));
                    }
                    break;
                case "clip.created":
                case "clip.featured":
                    foreach (var pair in new[] { ("clipid","id"), ("cliptitle","title"), ("clipurl","watch_url"), ("clipthumbnail","thumbnail_url"),
                        ("clipduration","duration_seconds"), ("clipfeaturedrank","featured_rank") })
                        parameters.SpecialIdentifiers[pair.Item1] = data.Value<string>(pair.Item2) ?? string.Empty;
                    await events.PerformEvent(type == "clip.created" ? EventTypeEnum.RazoChannelClipCreated : EventTypeEnum.RazoChannelClipFeatured, parameters);
                    break;
                case "boost.new":
                    parameters.SpecialIdentifiers["razoboostminutes"] = data.Value<string>("duration_minutes") ?? string.Empty;
                    await events.PerformEvent(EventTypeEnum.RazoChannelBoost, parameters);
                    break;
                case "jumpscare":
                    await events.PerformEvent(EventTypeEnum.RazoChannelJumpscare, parameters);
                    break;
            }
        }
    }
}

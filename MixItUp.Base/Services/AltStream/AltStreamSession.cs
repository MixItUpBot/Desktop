using MixItUp.Base.Model;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.User;
using MixItUp.Base.Model.User.Platform;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Chat;
using MixItUp.Base.ViewModel.User;
using MixItUp.Base.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.ServerSentEvents;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Base.Services.AltStream
{
    public class AltStreamSession : ChannelChatSessionBase
    {
        private static readonly TimeSpan requestTimeout = TimeSpan.FromSeconds(12);
        // An idle channel can take a heartbeat interval to return the stream headers.
        private static readonly TimeSpan streamHeaderTimeout = TimeSpan.FromSeconds(45);
        private static readonly HttpClient sharedClient = new HttpClient(HttpConnectionFactory.CreateHandler(allowAutoRedirect: false))
        {
            BaseAddress = new Uri("https://altstream.online/"), Timeout = Timeout.InfiniteTimeSpan
        };
        private readonly HttpClient client;
        private IReadOnlyDictionary<string, ChatEmoteViewModelBase> emotes;
        private readonly Queue<DateTimeOffset> publicRequests = new Queue<DateTimeOffset>();
        private DateTimeOffset retryAt;
        public AltStreamSession() : this(sharedClient) { }
        internal AltStreamSession(HttpClient client) { this.client = client; }
        private bool hasStreamState;
        public override StreamingPlatformTypeEnum Platform => StreamingPlatformTypeEnum.AltStream;
        public override bool RequiresToken => true;
        public override int MaxMessageLength => 500;
        protected override TimeSpan VerificationTimeout => requestTimeout + requestTimeout;

        private async Task<HttpResponseMessage> Request(HttpMethod method, string path, string token, CancellationToken cancel, object body = null, TimeSpan? headerTimeout = null)
        {
            if (this.retryAt > DateTimeOffset.UtcNow) throw new HttpRequestException("AltStream rate limit");
            if (string.IsNullOrEmpty(token))
            {
                lock (this.publicRequests)
                {
                    var now = DateTimeOffset.UtcNow;
                    while (this.publicRequests.TryPeek(out var first) && first <= now.AddMinutes(-1)) this.publicRequests.Dequeue();
                    if (this.publicRequests.Count >= 60)
                        throw new HttpRequestException("AltStream public request limit", null, HttpStatusCode.TooManyRequests);
                    this.publicRequests.Enqueue(now);
                }
            }
            using var request = new HttpRequestMessage(method, path);
            if (!string.IsNullOrEmpty(token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body != null) request.Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
            timeout.CancelAfter(headerTimeout ?? requestTimeout);
            var response = await this.client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                this.retryAt = response.Headers.RetryAfter?.Date ?? DateTimeOffset.UtcNow.Add(response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMinutes(1));
            }
            if (!response.IsSuccessStatusCode)
            {
                var status = response.StatusCode;
                response.Dispose();
                if (status == HttpStatusCode.Unauthorized) await this.RejectActiveToken(token);
                throw new HttpRequestException("AltStream request failed", null, status);
            }
            return response;
        }

        private async Task<JToken> Get(string path, string token, CancellationToken cancel)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
            timeout.CancelAfter(requestTimeout);
            using var response = await this.Request(HttpMethod.Get, path, token, timeout.Token);
            return JToken.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
        }

        protected override async Task<string> VerifyChannel(string channel, string token, CancellationToken cancel)
        {
            string name = Uri.EscapeDataString(channel);
            var details = await this.Get("api/v1/channels/" + name, null, cancel);
            if (!string.Equals(((details as JObject)?["data"] as JObject)?.Value<string>("username"), channel, StringComparison.OrdinalIgnoreCase)) return null;
            try
            {
                var grants = await this.Get("api/v2/rewards/redemptions?status=unfulfilled&channel=" + name, token, cancel);
                return grants.Value<bool?>("success") == true && string.Equals(grants.Value<string>("channel"), channel, StringComparison.OrdinalIgnoreCase) ? channel : null;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
            {
                var profile = await this.Get("api/v2/users/" + name, token, cancel) as JObject;
                return string.Equals(profile?.Value<string>("channelName"), channel, StringComparison.OrdinalIgnoreCase)
                    && (profile["roles"] as JObject)?.Value<bool?>("broadcaster") == true ? channel : null;
            }
        }

        protected override async Task Receive(string channel, string token, CancellationToken cancel)
        {
            string query = "?channel=" + Uri.EscapeDataString(channel);
            this.ChannelLink = "https://altstream.online/" + Uri.EscapeDataString(channel);
            this.BotID = "bot:" + channel.ToLowerInvariant();
            this.hasStreamState = false;
            this.emotes = null;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var recent = new Queue<string>();
            int delay = 1;
            bool loadHistory = true;
            while (!cancel.IsCancellationRequested)
            {
                try
                {
                    long connectedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    using var response = await this.Request(HttpMethod.Get, "api/v2/chat/stream" + query, token, cancel, headerTimeout: streamHeaderTimeout);
                    if (response.Content.Headers.ContentType?.MediaType != "text/event-stream")
                        throw new HttpRequestException("Invalid AltStream chat response");
                    using var stream = await response.Content.ReadAsStreamAsync(cancel);
                    if (loadHistory)
                    {
                        loadHistory = false;
                        try
                        {
                            var catalog = await this.Get("api/v1/channels/" + Uri.EscapeDataString(channel) + "/emotes", null, cancel);
                            if ((catalog as JObject)?["data"] is JObject data && data["emotes"] is JArray items)
                            {
                                var emotes = new Dictionary<string, ChatEmoteViewModelBase>(StringComparer.Ordinal);
                                foreach (var item in items.OfType<JObject>())
                                {
                                    var emote = AltStreamChatMessage.ReadEmote(item);
                                    if (emote != null) emotes[emote.Name] = emote;
                                }
                                this.emotes = emotes;
                            }
                        }
                        catch (HttpRequestException) { }
                        catch (JsonException) { }
                        try
                        {
                            var history = await this.Get("api/v2/chat" + query + "&limit=25", token, cancel);
                            if (history is JArray rows)
                            {
                                foreach (var row in rows.OfType<JObject>().OrderBy(r => r.Value<long?>("sentAt")))
                                {
                                    if (!(row.Value<long?>("sentAt") < connectedAt) || row.Value<string>("id") is not string id ||
                                        (row.Value<string>("channel") is string other && !string.Equals(channel, other, StringComparison.OrdinalIgnoreCase))) continue;
                                    if (ids.Add(id))
                                    {
                                        recent.Enqueue(id);
                                        await this.HandleFrame(channel, row, history: true);
                                    }
                                }
                            }
                        }
                        catch (HttpRequestException) { }
                    }
                    await foreach (var packet in SseParser.Create(stream).EnumerateAsync(cancel))
                    {
                        if (string.IsNullOrWhiteSpace(packet.Data)) continue;
                        try
                        {
                            JObject frame = JObject.Parse(packet.Data);
                            string id = frame.Value<string>("id") ?? (frame["message"] as JObject)?.Value<string>("id");
                            if (frame.Value<string>("type") == "redemption" && (frame["redemption"] as JObject)?.Value<string>("id") is string redemptionID)
                                id = "redemption:" + redemptionID;
                            if (frame.Value<string>("channel") is string target && !string.Equals(target, channel, StringComparison.OrdinalIgnoreCase)) continue;
                            if (!string.IsNullOrWhiteSpace(id))
                            {
                                if (!ids.Add(id)) continue;
                                recent.Enqueue(id);
                                if (recent.Count > 4096) ids.Remove(recent.Dequeue());
                            }
                            await this.HandleFrame(channel, frame);
                            delay = 1;
                        }
                        catch (Exception ex) when (ex is JsonException or InvalidCastException or FormatException or OverflowException or InvalidOperationException)
                        {
                            Logger.Log(LogLevel.Warning, $"Invalid AltStream event ({ex.GetType().Name})");
                        }
                    }
                }
                catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden || ex.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw;
                }
                catch (OperationCanceledException) when (cancel.IsCancellationRequested) { return; }
                catch (Exception ex) { Logger.Log(LogLevel.Warning, $"AltStream chat disconnected ({ex.GetType().Name})"); }
                TimeSpan backoff = TimeSpan.FromSeconds(delay);
                TimeSpan providerDelay = this.retryAt - DateTimeOffset.UtcNow;
                await Task.Delay(backoff > providerDelay ? backoff : providerDelay, cancel);
                delay = Math.Min(60, delay * 2);
            }
        }

        internal async Task HandleFrame(string channel, JObject frame, bool history = false)
        {
            string type = frame.Value<string>("type");
            string prefix = "altstream:" + channel.ToLowerInvariant() + ":";
            var chat = ServiceManager.Get<ChatService>();
            var message = frame["message"] as JObject;
            if (history && type is not ("message" or "reply" or "highlight")) return;
            if (type == "tip" || type == "subscription" || type == "shop")
            {
                this.DisplayNotice(frame, prefix, FormatPaymentNotice(type, frame));
                return;
            }
            EventTypeEnum? notification = type switch
            {
                "system" => EventTypeEnum.AltStreamSystemMessage,
                "pin" => EventTypeEnum.AltStreamMessagePinned,
                "unpin" => EventTypeEnum.AltStreamMessageUnpinned,
                "raid_started" => EventTypeEnum.AltStreamOutgoingRaidStarted,
                "raid_cancelled" => EventTypeEnum.AltStreamOutgoingRaidCancelled,
                "shoutout" => EventTypeEnum.AltStreamShoutout,
                _ => null
            };
            if (notification.HasValue)
            {
                var sender = AltStreamUserPlatformV2Model.FromAuthor(frame["author"] as JObject, fromBot: true);
                var noticeUser = new UserV2ViewModel(this.Platform, new UserV2Model(sender ?? new AltStreamUserPlatformV2Model { Username = "AltStream", DisplayName = "AltStream" }));
                var parameters = new CommandParametersModel(noticeUser, this.Platform) { DisplayOnlyActor = true };
                var outgoing = frame["raid"] as JObject;
                var target = (frame["shoutout"] as JObject)?["target"] as JObject ?? outgoing?["to"] as JObject;
                parameters.SpecialIdentifiers["message"] = message?.Value<string>("text") ?? string.Empty;
                parameters.SpecialIdentifiers["messageid"] = frame.Value<string>("targetId") ?? message?.Value<string>("id") ?? string.Empty;
                parameters.SpecialIdentifiers["altstreamtargetchannel"] = target?.Value<string>("channelName") ?? string.Empty;
                parameters.SpecialIdentifiers["raidviewercount"] = outgoing?.Value<string>("viewers") ?? string.Empty;
                this.DisplayNotice(frame, prefix, Resources.ResourceManager.GetString(notification.Value.ToString()));
                await ServiceManager.Get<EventService>().PerformEvent(notification.Value, parameters);
                return;
            }
            if (type == "delete")
            {
                string target = frame.Value<string>("targetId") ?? message?.Value<string>("id") ?? frame.Value<string>("messageId");
                if (!string.IsNullOrWhiteSpace(target)) await chat.DeleteMessage(prefix + target);
                return;
            }
            if (type == "clear")
            {
                await chat.ClearChannelMessages(this.Platform, prefix);
                return;
            }
            var actor = GetActor(frame);
            bool fromBot = frame.Value<string>("source") == "bot" || actor?.Value<bool?>("isBot") == true || (actor?["roles"] as JObject)?.Value<bool?>("isBot") == true;
            var author = AltStreamUserPlatformV2Model.FromAuthor(actor, fromBot);
            if (author == null)
            {
                if (!history) this.DisplayNotice(frame, prefix);
                return;
            }
            bool hasActorID = !string.IsNullOrWhiteSpace(author.ID);
            if (!hasActorID && !fromBot && type is ("follow" or "raid_received" or "redemption"))
            {
                var verified = await this.FindUser(author.Username);
                if (string.Equals(verified?.Username, author.Username, StringComparison.OrdinalIgnoreCase))
                {
                    verified.AvatarLink ??= author.AvatarLink;
                    author = verified;
                }
            }
            if (fromBot) author.ID = "bot:" + author.Username.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(author.ID)) author.ID = "user:" + author.Username.ToLowerInvariant();
            var user = history ? new UserV2ViewModel(this.Platform, new UserV2Model(author)) :
                await ServiceManager.Get<UserService>().CreateUser(author, matchUsername: false);
            if (user == null) return;
            var viewer = user.GetPlatformData<AltStreamUserPlatformV2Model>(this.Platform);
            if (!history)
            {
                viewer.UpdateFrom(author);
                if (author.AvatarLink == null && type is ("message" or "reply" or "highlight" or "follow" or "raid_received"))
                {
                    await viewer.RefreshAvatar(this);
                }
            }
            if (type == "follow" && !history)
            {
                viewer.Roles.Add(UserRoleEnum.Follower);
                if (frame.Value<long?>("sentAt") is long timestamp && timestamp >= 0 && timestamp <= 253402300799999)
                    viewer.FollowDate = DateTimeOffset.FromUnixTimeMilliseconds(timestamp);
                if (await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.AltStreamChannelFollowed, new CommandParametersModel(user, this.Platform)))
                {
                    ChannelSession.Settings.LatestSpecialIdentifiersData[SpecialIdentifierStringBuilder.LatestFollowerUserData] = user.ID;
                    EventService.FollowOccurred(user);
                }
                return;
            }
            if (type == "redemption" && !history && frame["redemption"] is JObject reward &&
                reward.Value<string>("status") is (null or "unfulfilled") && reward.Value<string>("id") is string redemption &&
                reward.Value<string>("rewardId") is string rewardID && reward.Value<string>("rewardName") is string name &&
                (reward.Value<int?>("cost") ?? reward.Value<int?>("rewardCost")) is int cost && cost >= 0)
            {
                if (author.AvatarLink == null)
                {
                    await viewer.RefreshAvatar(this);
                }
                var p = new CommandParametersModel(user, this.Platform);
                p.SpecialIdentifiers["rewardname"] = name;
                p.SpecialIdentifiers["rewardcost"] = cost.ToString(CultureInfo.InvariantCulture);
                p.SpecialIdentifiers["rewardid"] = rewardID;
                p.SpecialIdentifiers["redemptionid"] = redemption;
                p.SpecialIdentifiers["message"] = reward.Value<string>("userInput") ?? string.Empty;
                p.Arguments.AddRange(p.SpecialIdentifiers["message"].Split(' ', StringSplitOptions.RemoveEmptyEntries));
                await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.AltStreamChannelPointsRedeemed, p);
                EventService.ChannelPointsRedeemedOccurred(user, cost);
                var commands = ServiceManager.Get<CommandService>();
                var bound = commands.AltStreamChannelPointsCommands.FirstOrDefault(c => c.ChannelPointRewardID == rewardID);
                if (bound != null) await commands.Queue(bound, p);
                return;
            }
            if (type == "raid_received" && !history && frame["raid"] is JObject raid &&
                (raid.Value<int?>("viewers") ?? raid.Value<int?>("viewerCount")) is int viewers && viewers >= 0)
            {
                var p = new CommandParametersModel(user, this.Platform);
                p.SpecialIdentifiers["raidviewercount"] = p.SpecialIdentifiers["hostviewercount"] = viewers.ToString();
                if (await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.AltStreamChannelRaided, p))
                {
                    ChannelSession.Settings.LatestSpecialIdentifiersData[SpecialIdentifierStringBuilder.LatestRaidUserData] = user.ID;
                    ChannelSession.Settings.LatestSpecialIdentifiersData[SpecialIdentifierStringBuilder.LatestRaidViewerCountData] = viewers;
                    EventService.RaidOccurred(user, viewers);
                }
                return;
            }
            if (type is not ("message" or "reply" or "highlight")) return;
            var entry = new AltStreamChatMessage(prefix, frame, user, this.emotes);
            if (history)
            {
                if ((!fromBot || !ChannelSession.Settings.HideBotMessages) && !ChannelSession.Settings.HideSpecificUserMessages.Contains(author.Username.ToLowerInvariant()))
                    chat.DisplayMessage(entry);
            }
            else await chat.AddMessage(entry);
        }

        internal static JObject GetActor(JObject frame)
        {
            var author = frame["author"] as JObject;
            return frame.Value<string>("type") switch
            {
                "follow" => frame["followedBy"] as JObject ?? author,
                "raid_received" when frame["raid"] is JObject raid => raid["from"] as JObject ?? raid["raider"] as JObject ?? author,
                "redemption" when frame["redemption"] is JObject reward => reward["from"] as JObject ?? reward["user"] as JObject ?? author,
                _ => author
            };
        }

        private static string FormatPaymentNotice(string type, JObject frame)
        {
            var payment = frame["kofi"] as JObject ?? frame[type] as JObject;
            var payer = (payment?["from"] as JValue)?.Value<string>() ?? (payment?["from_name"] as JValue)?.Value<string>() ?? ((frame["author"] as JObject)?["displayName"] as JValue)?.Value<string>();
            string amount = (payment?["amount"] as JValue)?.Value<string>();
            string currency = (payment?["currencyCode"] as JValue)?.Value<string>() ?? (payment?["currency"] as JValue)?.Value<string>();
            string label = type == "tip" ? Resources.KoFiTip : type == "shop" ? Resources.KoFiShopOrder : Resources.KoFiMembership;
            if (string.IsNullOrWhiteSpace(payer) || currency?.Length != 3 ||
                !decimal.TryParse(amount, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal paid) || paid <= 0)
            {
                return label;
            }
            return $"{label} · {payer} · {amount} {currency}";
        }

        private void DisplayNotice(JObject frame, string prefix, string fallback = null)
        {
            string text = (frame["message"] as JObject)?.Value<string>("text");
            if (string.IsNullOrWhiteSpace(text)) text = fallback;
            if (string.IsNullOrWhiteSpace(text)) return;
            var notice = new ChatMessageViewModel(prefix + frame.Value<string>("id"), this.Platform, null);
            notice.AddStringMessagePart(text);
            ServiceManager.Get<ChatService>().DisplayMessage(notice);
        }

        private sealed class ChannelDetails
        {
            public string Username { get; set; }
            public StreamDetails Stream { get; set; }
        }

        private sealed class StreamDetails
        {
            public bool? IsLive { get; set; }
            public string Title { get; set; }
            public int? Viewers { get; set; }
            public DateTimeOffset? StartedAt { get; set; }
            public Category Game { get; set; }
        }

        private sealed class Category
        {
            public string Name { get; set; }
            public string CoverUrl { get; set; }
        }

        public override async Task<Result> RefreshDetails()
        {
            string channel = this.ChannelID;
            if (!this.IsConnected || string.IsNullOrWhiteSpace(channel)) return new Result(false);
            var details = await AsyncRunner.RunAsync(async () =>
            {
                var response = await this.Get("api/v1/channels/" + Uri.EscapeDataString(channel), null, CancellationToken.None);
                return ((response as JObject)?["data"] as JObject)?.ToObject<ChannelDetails>();
            });
            if (channel != this.ChannelID || !string.Equals(details?.Username, channel, StringComparison.OrdinalIgnoreCase))
                return new Result(false);
            var stream = details.Stream;
            if (stream != null && !stream.IsLive.HasValue) return new Result(false);
            bool live = stream?.IsLive == true;
            bool changed = this.hasStreamState && this.IsLive != live;
            this.IsLive = live;
            this.StreamTitle = stream?.Title;
            this.StreamCategoryName = stream?.Game?.Name;
            this.StreamCategoryImageURL = stream?.Game?.CoverUrl;
            this.StreamViewerCount = stream?.Viewers ?? 0;
            this.StreamStart = stream?.StartedAt ?? DateTimeOffset.MinValue;
            this.hasStreamState = true;
            if (changed) await ServiceManager.Get<EventService>().PerformEvent(live ? EventTypeEnum.AltStreamChannelStreamStart : EventTypeEnum.AltStreamChannelStreamStop, new CommandParametersModel(this.Platform));
            return new Result();
        }

        public override Task SendMessage(string message, bool sendAsStreamer = false) => this.SendChatMessage(message, null);

        public async Task SendChatMessage(string message, string replyMessageID)
        {
            if (!this.IsBotConnected || string.IsNullOrWhiteSpace(message)) return;
            string channel = this.ChannelID;
            string prefix = "altstream:" + channel.ToLowerInvariant() + ":";
            foreach (string text in this.SplitLargeMessage(message))
            {
                var body = new JObject { ["channel"] = channel, ["message"] = text };
                if (replyMessageID?.StartsWith(prefix, StringComparison.Ordinal) == true && replyMessageID.Length > prefix.Length)
                    body["replyToId"] = replyMessageID.Substring(prefix.Length);
                using var response = await this.Request(HttpMethod.Post, "api/v2/chat", this.Token, CancellationToken.None, body);
                replyMessageID = null;
            }
        }

        public async Task<AltStreamUserPlatformV2Model> FindUser(string username)
        {
            if (!this.IsBotConnected || string.IsNullOrWhiteSpace(username)) return null;
            try
            {
                var profile = await this.Get("api/v2/users/" + Uri.EscapeDataString(username), this.Token, CancellationToken.None);
                var user = AltStreamUserPlatformV2Model.FromAuthor(profile as JObject);
                if (user != null) user.LastAvatarLookup = DateTimeOffset.UtcNow;
                return user;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound) { return null; }
            catch (HttpRequestException ex) when (ex.StatusCode != HttpStatusCode.Unauthorized)
            {
                Logger.Log(LogLevel.Warning, $"AltStream user lookup failed ({ex.StatusCode?.ToString() ?? "network error"}).");
                return null;
            }
            catch (OperationCanceledException)
            {
                Logger.Log(LogLevel.Warning, "AltStream user lookup timed out.");
                return null;
            }
        }

        public async Task Shoutout(string target)
        {
            if (!this.IsBotConnected || string.IsNullOrWhiteSpace(target)) return;
            using var response = await this.Request(HttpMethod.Post, "api/shoutout", this.Token, CancellationToken.None,
                new { channel = this.ChannelID, target = target.Trim().TrimStart('@') });
        }

        public async Task SetRedemptionStatus(long id, bool refund)
        {
            if (!this.IsBotConnected || id <= 0) return;
            using var response = await this.Request(HttpMethod.Patch, "api/v2/rewards/redemptions", this.Token, CancellationToken.None,
                new { channel = this.ChannelID, id, action = refund ? "refund" : "fulfill" });
        }

        public class Reward
        {
            public string ID { get; set; }
            public string Name { get; set; }
        }

        public async Task<IEnumerable<Reward>> GetRewards()
        {
            if (!this.IsConnected) return Array.Empty<Reward>();
            var response = await AsyncRunner.RunAsync(this.Get("api/v1/channels/" + Uri.EscapeDataString(this.ChannelID) + "/rewards", null, CancellationToken.None));
            var rows = ((response as JObject)?["data"] as JObject)?["rewards"] as JArray;
            return (rows ?? new JArray()).OfType<JObject>()
                .Select(r => new Reward { ID = (r["id"] as JValue)?.ToString(), Name = (r["name"] as JValue)?.ToString() })
                .Where(r => !string.IsNullOrWhiteSpace(r.ID) && !string.IsNullOrWhiteSpace(r.Name))
                .ToList();
        }
    }
}

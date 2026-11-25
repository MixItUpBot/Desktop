using MixItUp.Base.Model;
using MixItUp.Base.Model.Joystick;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.User;
using MixItUp.Base.Model.User.Platform;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Chat;
using MixItUp.Base.ViewModel.User;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace MixItUp.Base.Services.Joystick
{
    public class JoystickSession : StreamingPlatformSessionBase
    {
        public JoystickSession() : this(new JoystickService()) { }
        internal JoystickSession(JoystickService service) { this.Service = service; }

        public JoystickService Service { get; }
        public override StreamingPlatformTypeEnum Platform => StreamingPlatformTypeEnum.Joystick;
        public override OAuthServiceBase StreamerOAuthService => this.Service;
        public override OAuthServiceBase BotOAuthService => this.Service;
        public override int MaxMessageLength => 500;
        public override bool IsBotUser(UserV2ViewModel user) => this.IsConnected && !string.IsNullOrEmpty(this.BotUsername)
            && string.Equals(user?.Username, this.BotUsername, StringComparison.OrdinalIgnoreCase);
        public HashSet<string> Permissions { get; } = new HashSet<string>(StringComparer.Ordinal);

        private readonly HashSet<string> eventIDs = new HashSet<string>(StringComparer.Ordinal);
        private readonly Queue<string> recentEvents = new Queue<string>();
        private bool hasStreamState;

        protected override async Task<Result> InitializeStreamerInternal()
        {
            var identity = JoystickUserPlatformV2Model.FromIdentity(await this.Service.Request(HttpMethod.Get, "identity"));
            if (identity == null) { return new Result(Resources.AuthenticationFailedGeneric); }
            JObject installation = await this.Service.Request(HttpMethod.Get, "permissions");
            if (!(installation?["permissions"] is JArray permissions)) return new Result(Resources.AuthenticationFailedGeneric);
            this.Permissions.Clear();
            this.Permissions.UnionWith(permissions.Values<string>());
            this.StreamerID = this.ChannelID = identity.ID;
            this.StreamerUsername = identity.Username;
            this.StreamerAvatarURL = identity.AvatarLink;
            this.ChannelLink = "https://joystick.tv/u/" + Uri.EscapeDataString(identity.Username);
            this.BotUsername = (installation["bot"] as JObject)?.Value<string>("username");
            var users = ServiceManager.Get<UserService>();
            this.Streamer = await users.GetUserByPlatform(this.Platform, platformID: identity.ID, performPlatformSearch: false)
                ?? await users.CreateUser(identity, matchUsername: false);
            var profile = this.Streamer?.GetPlatformData<JoystickUserPlatformV2Model>(this.Platform);
            if (profile != null)
            {
                profile.Username = identity.Username;
                profile.DisplayName = identity.DisplayName;
                profile.AvatarLink = identity.AvatarLink;
                profile.Roles.Add(UserRoleEnum.Streamer);
            }
            return new Result();
        }

        protected override Task DisconnectStreamerInternal()
        {
            lock (this.eventIDs) { this.eventIDs.Clear(); this.recentEvents.Clear(); }
            this.Permissions.Clear();
            this.hasStreamState = false;
            this.BotUsername = null;
            return Task.CompletedTask;
        }

        protected override Task<Result> InitializeBotInternal() => Task.FromResult(new Result(false));
        protected override Task DisconnectBotInternal() => Task.CompletedTask;
        public override Task RefreshOAuthTokenIfCloseToExpiring() => Task.CompletedTask;

        public override async Task<Result> RefreshDetails()
        {
            if (!this.Permissions.Contains("stream:read")) { return new Result(); }
            string channel = this.ChannelID;
            if (string.IsNullOrWhiteSpace(channel)) return new Result(false);
            JObject stream = await AsyncRunner.RunAsync(this.Service.Request(HttpMethod.Get, "stream"));
            if (channel != this.ChannelID) return new Result(false);
            return await this.ApplyStream(stream, channel);
        }

        private sealed class StreamDetails
        {
            [JsonProperty("channel_id")] public string ChannelID { get; set; }
            public bool? Live { get; set; }
            [JsonProperty("stream_title")] public string Title { get; set; }
            [JsonProperty("view_count")] public int? Viewers { get; set; }
            [JsonProperty("live_at")] public DateTimeOffset? Start { get; set; }
        }

        internal async Task<Result> ApplyStream(JObject response, string channel)
        {
            StreamDetails stream;
            try { stream = response?.ToObject<StreamDetails>(); }
            catch (JsonException ex) { return new Result(ex); }
            if (stream?.Live == null || stream.ChannelID != channel || string.IsNullOrWhiteSpace(channel)) return new Result(false);
            bool live = stream.Live.Value;
            bool changed = this.hasStreamState && live != this.IsLive;
            this.StreamTitle = stream.Title;
            this.StreamViewerCount = stream.Viewers ?? 0;
            this.StreamStart = stream.Start ?? DateTimeOffset.MinValue;
            this.IsLive = live;
            this.hasStreamState = true;
            if (changed) await ServiceManager.Get<EventService>().PerformEvent(live ? EventTypeEnum.JoystickChannelStreamStart :
                EventTypeEnum.JoystickChannelStreamStop, new CommandParametersModel(this.Platform));
            return new Result();
        }

        public override async Task SendMessage(string message, bool sendAsStreamer = false)
        {
            if (!this.IsConnected || string.IsNullOrWhiteSpace(message)) return;
            var settings = ChannelSession.Settings;
            string channel = this.ChannelID;
            foreach (string text in this.SplitLargeMessage(message))
            {
                Logger.Log(LogLevel.Debug, "Joystick chat send requested.");
                JObject receipt = null;
                try
                {
                    receipt = await this.Service.Request(HttpMethod.Post, "messages", new { text });
                }
                catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException || ex is JsonException)
                {
                    string reason = ex is HttpRequestException http && http.StatusCode.HasValue
                        ? $"HTTP {(int)http.StatusCode.Value}" : ex.GetType().Name;
                    Logger.Log(LogLevel.Warning, $"Joystick chat request failed ({reason}).");
                }

                bool sent = receipt?["sent"]?.Type == JTokenType.Boolean && (bool)receipt["sent"];
                Logger.Log(sent ? LogLevel.Debug : LogLevel.Warning, sent ? "Joystick confirmed the chat send." : "Joystick did not confirm the chat send.");
                if (!this.IsConnected || channel != this.ChannelID || settings != ChannelSession.Settings) return;

                if (!sent)
                {
                    var notice = new ChatMessageViewModel(Guid.NewGuid().ToString(), this.Platform, null);
                    notice.AddStringMessagePart(Resources.JoystickChatSendUnconfirmed);
                    ServiceManager.Get<ChatService>().DisplayMessage(notice);
                    return;
                }
            }
        }

        public async Task HandleFrame(JObject frame)
        {
            if (!this.IsConnected) return;
            var item = JoystickGatewayEvent.FromFrame(frame, this.ChannelID);
            if (item == null) return;
            if (!string.IsNullOrEmpty(item.ID))
            {
                lock (this.eventIDs)
                {
                    if (!this.eventIDs.Add(item.ID))
                    {
                        Logger.Log(LogLevel.Debug, $"Skipping duplicate Joystick event: {item.Type} ({item.ID})");
                        return;
                    }
                    this.recentEvents.Enqueue(item.ID);
                    if (this.recentEvents.Count > 2048) this.eventIDs.Remove(this.recentEvents.Dequeue());
                }
            }

            JObject data = item.Data;
            if (item.Type == "viewer_count_updated")
            {
                this.StreamViewerCount = data.Value<int?>("number_of_viewers") ?? this.StreamViewerCount;
                return;
            }
            if (item.Type == "stream_started" || item.Type == "stream_ending") { await this.RefreshDetails(); return; }

            var author = data["author"] as JObject;
            string username = author?.Value<string>("username") ?? data.Value<string>("username") ?? data.Value<string>("who") ?? data.Value<string>("gifter");
            if (string.IsNullOrWhiteSpace(username)) return;
            var users = ServiceManager.Get<UserService>();
            UserV2ViewModel user;
            if ((author?["badges"] as JObject)?.Value<bool?>("host") == true && string.Equals(username, this.StreamerUsername, StringComparison.OrdinalIgnoreCase))
                user = this.Streamer;
            else
            {
                var model = JoystickUserPlatformV2Model.FromAuthor(author ?? new JObject { ["username"] = username });
                if (model == null) return;
                user = await users.GetUserByPlatform(this.Platform, platformID: model.ID, performPlatformSearch: false)
                    ?? await users.CreateUser(model, matchUsername: false);
            }
            if (user == null) return;
            if (author != null)
            {
                user.GetPlatformData<JoystickUserPlatformV2Model>(this.Platform)?.SetAuthor(author);
                user.RefreshCachedProperties();
            }
            var events = ServiceManager.Get<EventService>();
            var parameters = new CommandParametersModel(user, this.Platform);
            switch (item.Type)
            {
                case "new_message":
                case "bot_message":
                    var message = new JoystickChatMessage(item, user, this.StreamerUsername);
                    await ServiceManager.Get<ChatService>().AddMessage(message);
                    break;
                case "enter_stream": await users.AddOrUpdateActiveUser(user); break;
                case "leave_stream": await users.RemoveActiveUser(user); break;
                case "followed":
                    user.Roles.Add(UserRoleEnum.Follower);
                    user.FollowDate = item.OccurredAt ?? DateTimeOffset.Now;
                    if (await events.PerformEvent(EventTypeEnum.JoystickChannelFollowed, parameters))
                    {
                        ChannelSession.Settings.LatestSpecialIdentifiersData[SpecialIdentifierStringBuilder.LatestFollowerUserData] = user.ID;
                        EventService.FollowOccurred(user);
                    }
                    break;
                case "subscribed":
                case "resubscribed":
                    user.Roles.Add(UserRoleEnum.Subscriber);
                    int months = Math.Max(1, data.Value<int?>("months") ?? 1);
                    user.SubscribeDate = item.OccurredAt ?? DateTimeOffset.Now;
                    user.TotalMonthsSubbed = Math.Max(user.TotalMonthsSubbed, (uint)months);
                    parameters.SpecialIdentifiers["usersubmonths"] = months.ToString();
                    var subscription = new SubscriptionDetailsModel(this.Platform, user, months: months);
                    bool resub = item.Type == "resubscribed";
                    if (await events.PerformEvent(resub ? EventTypeEnum.JoystickChannelResubscribed : EventTypeEnum.JoystickChannelSubscribed, parameters))
                    {
                        ChannelSession.Settings.LatestSpecialIdentifiersData[SpecialIdentifierStringBuilder.LatestSubscriberUserData] = user.ID;
                        ChannelSession.Settings.LatestSpecialIdentifiersData[SpecialIdentifierStringBuilder.LatestSubscriberSubMonthsData] = months;
                        if (resub) EventService.ResubscribeOccurred(subscription);
                        else EventService.SubscribeOccurred(subscription);
                    }
                    break;
                case "gifted_subscriptions":
                    int? count = data.Value<int?>("quantity") ?? data.Value<int?>("how_many");
                    var names = (data["recipients"] as JArray)?.Values<string>().Where(n => !string.IsNullOrWhiteSpace(n)).ToList() ?? new List<string>();
                    if (names.Count == 0 && data["recipient"]?.Type == JTokenType.String) names.Add((string)data["recipient"]);
                    names = names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    int quantity = Math.Max(count ?? 0, names.Count);
                    if (quantity <= 0) break;
                    int filter = ChannelSession.Settings.MassGiftedSubsFilterAmount;
                    bool fireMassEvent = names.Count == 0 || filter == 0 || quantity > filter;
                    user.TotalSubsGifted += (uint)quantity;
                    var gifts = new List<SubscriptionDetailsModel>();
                    foreach (string name in names)
                    {
                        var recipient = JoystickUserPlatformV2Model.FromAuthor(new JObject { ["username"] = name });
                        var target = await users.CreateUser(recipient, matchUsername: false);
                        if (target == null) continue;
                        target.Roles.Add(UserRoleEnum.Subscriber);
                        target.SubscribeDate = item.OccurredAt ?? DateTimeOffset.Now;
                        target.TotalSubsReceived++;
                        var gift = new SubscriptionDetailsModel(this.Platform, target, user);
                        gifts.Add(gift);
                        parameters.Arguments.Add(target.Username);
                        if (filter == 0 || quantity <= filter)
                        {
                            var single = new CommandParametersModel(user, this.Platform) { TargetUser = target };
                            single.Arguments.Add(target.Username);
                            if (await events.PerformEvent(EventTypeEnum.JoystickChannelSubscriptionGifted, single) && !fireMassEvent) EventService.SubscriptionGiftedOccurred(gift);
                        }
                    }
                    if (fireMassEvent)
                    {
                        parameters.HasNoTarget = names.Count == 0;
                        parameters.SpecialIdentifiers["subsgiftedamount"] = quantity.ToString();
                        parameters.SpecialIdentifiers["subsgiftedlifetimeamount"] = user.TotalSubsGifted.ToString();
                        if (await events.PerformEvent(EventTypeEnum.JoystickChannelMassSubscriptionsGifted, parameters))
                        {
                            if (gifts.Count > 0) EventService.MassSubscriptionsGiftedOccurred(gifts);
                            else EventService.NotifySubscriptionsGiftedWithoutRecipients(user, quantity);
                        }
                    }
                    break;
                case "tipped":
                    int? amount = data.Value<int?>("how_much");
                    if (amount > 0)
                    {
                        parameters.SpecialIdentifiers["cheeramount"] = parameters.SpecialIdentifiers["joysticktokens"] = amount.Value.ToString();
                        parameters.SpecialIdentifiers["message"] = data.Value<string>("tip_menu_item") ?? string.Empty;
                        await events.PerformEvent(EventTypeEnum.JoystickChannelTipped, parameters);
                        EventService.JoystickChannelTippedOccurred(new JoystickTippedEventModel(user, amount.Value, parameters.SpecialIdentifiers["message"]));
                    }
                    break;
            }
        }

        public override async Task<Result> SetStreamTitle(string title)
        {
            if (!this.Permissions.Contains("stream:manage")) return new Result(false);
            try { await this.Service.Request(HttpMethod.Put, "stream/title", new { title }); }
            catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException || ex is JsonException) { return new Result(ex); }
            return await this.RefreshDetails();
        }

        public async Task Whisper(string username, string text)
        {
            if (this.Permissions.Contains("chat:whisper:write"))
                await this.Service.Request(HttpMethod.Post, "whispers", new { username, text });
        }
        public override Task<Result> SetStreamCategory(string category) => Task.FromResult(new Result(false));
        public bool CanModerateMessage(ChatMessageViewModel message) =>
            this.IsConnected && this.Permissions.Contains("chat:moderate") && message is JoystickChatMessage row && !row.IsWhisper &&
            !string.IsNullOrWhiteSpace(row.ProviderID) && row.ChannelID == this.ChannelID;

        public override async Task DeleteMessage(ChatMessageViewModel message)
        {
            if (message is JoystickChatMessage row && this.CanModerateMessage(row))
                await this.Service.Request(HttpMethod.Delete, "messages/" + Uri.EscapeDataString(row.ProviderID));
        }

        public async Task ModerateMessageAuthor(JoystickChatMessage message, bool ban)
        {
            if (!this.CanModerateMessage(message)) { return; }
            await this.Service.Request(HttpMethod.Post, $"messages/{Uri.EscapeDataString(message.ProviderID)}/{(ban ? "ban" : "mute")}");
            if (ban) { ChatService.ChatUserBanned(message.User); }
            else { ChatService.ChatUserTimedOut(message.User); }
        }
        public override Task ClearMessages() => Task.CompletedTask;
        public override Task TimeoutUser(UserV2ViewModel user, int durationInSeconds, string reason = null) => Task.CompletedTask;
        public override Task ModUser(UserV2ViewModel user) => Task.CompletedTask;
        public override Task UnmodUser(UserV2ViewModel user) => Task.CompletedTask;
        public override Task BanUser(UserV2ViewModel user, string reason = null) => Task.CompletedTask;
        public override async Task UnbanUser(UserV2ViewModel user)
        {
            if (user?.Platform == this.Platform && this.Permissions.Contains("chat:moderate"))
                await this.Service.Request(HttpMethod.Delete, "bans/" + Uri.EscapeDataString(user.Username));
        }
    }
}

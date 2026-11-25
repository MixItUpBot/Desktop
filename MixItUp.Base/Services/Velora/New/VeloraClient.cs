using MixItUp.Base.Model;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.Currency;
using MixItUp.Base.Model.User;
using MixItUp.Base.Model.User.Platform;
using MixItUp.Base.Model.Velora;
using MixItUp.Base.Model.Velora.Webhooks;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Chat;
using MixItUp.Base.ViewModel.Chat.Velora;
using MixItUp.Base.ViewModel.User;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MixItUp.Base.Services.Velora.New
{
    public class VeloraClient : ServiceClientBase
    {
        private const int MaxProcessedEventsCacheSize = 3000;

        public override bool IsConnected { get { return this.isConnected; } }
        private bool isConnected;

        // Chat flows over the Chat WS (newMessage); everything else over the Events WS. This flag lets the
        // Events WS skip its own chat.message copy while the Chat WS stream is live (see HandleEventSocketEvent),
        // since both channels carry chat and consuming both would double every message.
        public bool ChatSocketActive { get; set; }

        private readonly object processedEventIDsLock = new object();
        private readonly Queue<string> processedEventIDsQueue = new Queue<string>();
        private readonly HashSet<string> processedEventIDs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly HashSet<string> channelPointRedemptionCache = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public override Task<Result> Connect()
        {
            this.isConnected = true;
            return Task.FromResult(new Result());
        }

        public override Task Disconnect()
        {
            this.isConnected = false;
            return Task.CompletedTask;
        }

        // ===== Events WebSocket (wss://api.velora.tv/ws/events) callback =====
        // The event socket delivers the { event, timestamp, data } envelope; route the data through the
        // same handlers as the webhook path. Because you only receive your own channel's events,
        // broadcaster routing is implicit - no readBroadcasterVeloraUserId step is needed.
        public async Task HandleEventSocketEvent(string eventType, JObject data)
        {
            try
            {
                if (!this.IsConnected || string.IsNullOrWhiteSpace(eventType) || data == null)
                {
                    return;
                }

                // Chat is consumed from the Chat WS newMessage when it is up; skip the Events WS copy.
                if (string.Equals(eventType, "chat.message", StringComparison.OrdinalIgnoreCase) && this.ChatSocketActive)
                {
                    return;
                }

                // The Events WS envelope carries no relay MessageID, so dedup on any natural ID the payload
                // carries (a reconnect can redeliver). Handlers with their own caches (redemption) and the
                // cross-socket ban/timeout dedup cover the rest.
                string naturalID = data.GetValueOrDefault<string>("redemptionId", null)
                    ?? data.GetValueOrDefault<string>("messageId", null)
                    ?? data.GetValueOrDefault<string>("id", null)
                    ?? data.GetValueOrDefault<string>("eventId", null);
                if (!string.IsNullOrWhiteSpace(naturalID) && !this.ShouldProcessSocketEvent($"velora.evt:{eventType}:{naturalID}"))
                {
                    return;
                }

                await this.DispatchEvent(eventType, data);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        // Shared event dispatch used by both the webhook relay (HandleWebhookEvent) and the Events WS
        // (HandleEventSocketEvent). Velora's docs and dashboard use a couple of naming variants for some
        // events, so several cases carry an alias to stay compatible with whichever form is delivered.
        private async Task DispatchEvent(string eventType, JObject payload)
        {
            switch (eventType.ToLowerInvariant())
            {
                case "chat.message":
                    await this.HandleChatMessage(payload);
                    break;
                case "user.follow":
                case "channel.follow":
                    await this.HandleFollow(payload);
                    break;
                case "user.unfollow":
                    break;
                case "channel.subscribe":
                    await this.HandleSubscribe(payload);
                    break;
                case "channel.subscription.gift":
                case "channel.gift":
                    await this.HandleSubscriptionGift(payload);
                    break;
                case "channel.subscription.end":
                    await this.HandleSubscriptionEnd(payload);
                    break;
                case "channel.cheer":
                case "channel.volts":
                    await this.HandleCheer(payload);
                    break;
                case "channel.raid":
                    await this.HandleRaid(payload);
                    break;
                case "channel.ban":
                    await this.HandleBan(payload);
                    break;
                case "channel.unban":
                    await this.HandleUnban(payload);
                    break;
                case "channel.moderator.add":
                    await this.HandleModerator(payload, added: true);
                    break;
                case "channel.moderator.remove":
                    await this.HandleModerator(payload, added: false);
                    break;
                case "channel.channel_points_redemption":
                case "channel.points.redeem":
                    await this.HandleChannelPointsRedemption(payload);
                    break;
                case "stream.online":
                    await this.HandleStreamOnline(payload);
                    break;
                case "stream.offline":
                    await this.HandleStreamOffline(payload);
                    break;
                case "stream.update":
                case "channel.update":
                    await this.HandleStreamUpdate(payload);
                    break;
            }
        }

        private async Task<UserV2ViewModel> GetOrCreateUser(WebhookUserModel userReference)
        {
            if (userReference == null || !userReference.IsValid)
            {
                return null;
            }

            UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Velora, platformID: userReference.UserID, platformUsername: userReference.Username);
            if (user == null)
            {
                user = await ServiceManager.Get<UserService>().CreateUser(new VeloraUserPlatformV2Model(userReference));
            }
            else
            {
                VeloraUserPlatformV2Model platformData = user.GetPlatformData<VeloraUserPlatformV2Model>(StreamingPlatformTypeEnum.Velora);
                platformData?.SetUserProperties(userReference);
            }
            return user;
        }

        private async Task HandleChatMessage(JObject payload)
        {
            WebhookChatMessageEventModel messageEvent = payload.ToObject<WebhookChatMessageEventModel>();
            await this.ProcessChatMessage(messageEvent);
        }

        // ===== Chat WebSocket (wss://api.velora.tv/chat) callbacks =====
        // The chat socket forwards each received frame here as a Newtonsoft JObject; these reuse the same
        // defensive models and handlers as the webhook path, adding message-ID / moderation dedup because
        // a reconnect can redeliver and ban/timeout also arrive on the Events WS.

        /// <summary>Chat WS <c>newMessage</c> (replaces the webhook <c>chat.message</c> chat path).</summary>
        public async Task HandleChatSocketNewMessage(JObject payload)
        {
            if (!this.IsConnected || payload == null)
            {
                return;
            }

            // Log the raw frame at Debug: the live newMessage shape is unverified (role/badge fields
            // vary across Velora's delivery channels) and this is the only capture point for it.
            if (Logger.Level == LogLevel.Debug)
            {
                Logger.Log(LogLevel.Debug, $"Velora newMessage: {payload.ToString(Newtonsoft.Json.Formatting.None)}");
            }

            WebhookChatMessageEventModel messageEvent = payload.ToObject<WebhookChatMessageEventModel>();
            if (messageEvent == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(messageEvent.MessageID) && !this.ShouldProcessSocketEvent("velora.chat.msg:" + messageEvent.MessageID))
            {
                return;
            }

            await this.ProcessChatMessage(messageEvent);
        }

        /// <summary>Chat WS <c>userTimedOut</c> (durationSeconds + expiresAt). Deduped vs the Events WS.</summary>
        public async Task HandleChatSocketUserTimedOut(JObject payload)
        {
            if (!this.IsConnected || payload == null)
            {
                return;
            }

            // Log the raw payload: the live shape is unverified, and the duration Velora actually applied
            // matters because the /timeout slash-command fallback has to assume a unit for its bare duration
            // argument - this is the only place the platform reports it back.
            if (Logger.Level == LogLevel.Debug)
            {
                Logger.Log(LogLevel.Debug, $"Velora userTimedOut: {payload.ToString(Newtonsoft.Json.Formatting.None)}");
            }

            await this.HandleBan(payload);
        }

        /// <summary>Chat WS <c>userBanned</c>. Deduped vs the Events WS <c>channel.ban</c>.</summary>
        public async Task HandleChatSocketUserBanned(JObject payload)
        {
            if (!this.IsConnected || payload == null)
            {
                return;
            }

            // Log the raw payload: the live shape is unverified (VERIFY-LIVE in the migration spec).
            if (Logger.Level == LogLevel.Debug)
            {
                Logger.Log(LogLevel.Debug, $"Velora userBanned: {payload.ToString(Newtonsoft.Json.Formatting.None)}");
            }

            await this.HandleBan(payload);
        }

        /// <summary>Chat WS <c>chatCleared</c> - a moderator cleared chat; drop the local buffer.</summary>
        public async Task HandleChatSocketChatCleared(JObject payload)
        {
            if (!this.IsConnected)
            {
                return;
            }

            if (Logger.Level == LogLevel.Debug)
            {
                Logger.Log(LogLevel.Debug, $"Velora chatCleared: {payload?.ToString(Newtonsoft.Json.Formatting.None)}");
            }

            await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(StreamingPlatformTypeEnum.Velora, MixItUp.Base.Resources.ChatCleared, ChannelSession.Settings.AlertModerationColor));
            ChatService.ChatCleared();
        }

        /// <summary>Chat WS <c>moderationNotice</c> - targeted ban/timeout notice to the affected user.</summary>
        public Task HandleChatSocketModerationNotice(JObject payload)
        {
            if (Logger.Level == LogLevel.Debug)
            {
                Logger.Log(LogLevel.Debug, $"Velora moderation notice: {payload?.ToString(Newtonsoft.Json.Formatting.None)}");
            }
            return Task.CompletedTask;
        }

        /// <summary>Chat WS <c>viewer_count_update</c> - live viewer count for the channel.</summary>
        public Task HandleChatSocketViewerCountUpdate(JObject payload)
        {
            if (this.IsConnected && payload != null)
            {
                if (Logger.Level == LogLevel.Debug)
                {
                    Logger.Log(LogLevel.Debug, $"Velora viewer_count_update: {payload.ToString(Newtonsoft.Json.Formatting.None)}");
                }

                int? count = payload.GetValueOrDefault<int?>("count", null)
                    ?? payload.GetValueOrDefault<int?>("viewers", null)
                    ?? payload.GetValueOrDefault<int?>("viewerCount", null);
                if (count.HasValue && count.Value >= 0)
                {
                    ServiceManager.Get<VeloraSession>().ApplyViewerCount(count.Value);
                }
            }
            return Task.CompletedTask;
        }

        private async Task ProcessChatMessage(WebhookChatMessageEventModel messageEvent)
        {
            if (messageEvent == null || string.IsNullOrWhiteSpace(messageEvent.Message))
            {
                return;
            }

            // System-generated posts (e.g. the "Velora Community Bot" channel-point celebration cards) are
            // not real users. Their sender is a synthetic account that does not exist in Velora's user
            // directory, so resolving it hits GET /users/{name}, which 404s and logs a full exception stack
            // on every post, then persists a phantom user. The underlying events these cards echo (channel
            // point redemptions, etc.) are handled through their own Events WS handlers, so drop the
            // redundant system chat echo here rather than track a user for it.
            if (messageEvent.IsSystem ?? false)
            {
                return;
            }

            UserV2ViewModel user = await this.GetOrCreateUser(messageEvent.ResolvedSender);
            if (user == null)
            {
                return;
            }

            VeloraUserPlatformV2Model platformData = user.GetPlatformData<VeloraUserPlatformV2Model>(StreamingPlatformTypeEnum.Velora);
            platformData?.SetChatMessageProperties(messageEvent);

            if (messageEvent.BestSubscriberMonths.HasValue)
            {
                user.TotalMonthsSubbed = Math.Max(user.TotalMonthsSubbed, (uint)Math.Max(messageEvent.BestSubscriberMonths.Value, 0));
            }

            await ServiceManager.Get<ChatService>().AddMessage(new VeloraChatMessageViewModel(messageEvent, user));
        }

        private async Task HandleFollow(JObject payload)
        {
            WebhookFollowEventModel followEvent = payload.ToObject<WebhookFollowEventModel>();
            UserV2ViewModel user = await this.GetOrCreateUser(followEvent?.ResolvedUser);
            if (user == null)
            {
                return;
            }

            user.Roles.Add(UserRoleEnum.Follower);
            user.FollowDate = DateTimeOffset.Now;

            CommandParametersModel parameters = new CommandParametersModel(user, StreamingPlatformTypeEnum.Velora);
            if (await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VeloraChannelFollowed, parameters))
            {
                EventService.FollowOccurred(user);
                await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format(MixItUp.Base.Resources.AlertFollow, user.FullDisplayName), ChannelSession.Settings.AlertFollowColor));
            }
        }

        // Velora subs are numbered tiers with no streamer-set plan names, so $usersubplan and
        // $usersubplanname both carry the tier label that Twitch/YouTube commands already expect.
        private static string GetSubPlanName(int tier)
        {
            return $"{MixItUp.Base.Resources.Tier} {tier}";
        }

        private async Task HandleSubscribe(JObject payload)
        {
            WebhookSubscribeEventModel subEvent = payload.ToObject<WebhookSubscribeEventModel>();
            if (subEvent == null || (subEvent.IsGift ?? false))
            {
                // Gifted subs also arrive here tagged isGift=true; they are handled by
                // HandleSubscriptionGift, so skip them here to avoid firing a duplicate subscribe event.
                return;
            }

            UserV2ViewModel user = await this.GetOrCreateUser(subEvent?.ResolvedUser);
            if (user == null)
            {
                return;
            }

            int months = subEvent.ResolvedMonths;
            int tier = subEvent.TierNumber;

            user.Roles.Add(UserRoleEnum.Subscriber);
            user.SubscribeDate = DateTimeOffset.Now;
            user.TotalMonthsSubbed = Math.Max(user.TotalMonthsSubbed, (uint)months);

            CommandParametersModel parameters = new CommandParametersModel(user, StreamingPlatformTypeEnum.Velora);
            parameters.SpecialIdentifiers["usersubmonths"] = months.ToString();
            parameters.SpecialIdentifiers["usersubtier"] = tier.ToString();
            parameters.SpecialIdentifiers["usersubplan"] = GetSubPlanName(tier);
            parameters.SpecialIdentifiers["usersubplanname"] = GetSubPlanName(tier);
            if (!string.IsNullOrWhiteSpace(subEvent.Message))
            {
                parameters.SpecialIdentifiers["message"] = subEvent.Message;
            }

            if (subEvent.IsResubscribe)
            {
                parameters.SpecialIdentifiers["usersubstreak"] = Math.Max(subEvent.Streak ?? months, 1).ToString();
                if (await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VeloraChannelResubscribed, parameters))
                {
                    EventService.ResubscribeOccurred(new SubscriptionDetailsModel(StreamingPlatformTypeEnum.Velora, user, months: months, tier: tier));
                    await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format(MixItUp.Base.Resources.AlertResubscribedTier, user.FullDisplayName, months, $"Velora Tier {tier}"), ChannelSession.Settings.AlertSubColor));
                }
            }
            else
            {
                if (await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VeloraChannelSubscribed, parameters))
                {
                    EventService.SubscribeOccurred(new SubscriptionDetailsModel(StreamingPlatformTypeEnum.Velora, user, months: months, tier: tier));
                    await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format(MixItUp.Base.Resources.AlertSubscribedTier, user.FullDisplayName, $"Velora Tier {tier}"), ChannelSession.Settings.AlertSubColor));
                }
            }
        }

        private async Task HandleSubscriptionGift(JObject payload)
        {
            WebhookSubscriptionGiftEventModel giftEvent = payload.ToObject<WebhookSubscriptionGiftEventModel>();
            if (giftEvent == null)
            {
                return;
            }

            WebhookUserModel gifterReference = giftEvent.ResolvedGifter;
            UserV2ViewModel gifter = null;
            if (gifterReference != null && !gifterReference.IsAnonymous)
            {
                gifter = await this.GetOrCreateUser(gifterReference);
            }
            if (gifter == null)
            {
                gifter = UserV2ViewModel.CreateUnassociated("Anonymous");
            }

            int tier = giftEvent.TierNumber;
            int quantity = giftEvent.ResolvedQuantity;
            List<WebhookUserModel> recipients = giftEvent.ResolvedRecipients;

            int filterAmount = ChannelSession.Settings.MassGiftedSubsFilterAmount;
            bool fireMassEvent = filterAmount == 0 || quantity > filterAmount;
            bool fireIndividualEvents = filterAmount == 0 || quantity <= filterAmount;

            bool isAnonymous = gifterReference?.IsAnonymous ?? (gifter.IsUnassociated);

            List<SubscriptionDetailsModel> subscriptions = new List<SubscriptionDetailsModel>();
            foreach (WebhookUserModel recipientReference in recipients)
            {
                UserV2ViewModel recipient = await this.GetOrCreateUser(recipientReference);
                if (recipient == null)
                {
                    continue;
                }

                recipient.Roles.Add(UserRoleEnum.Subscriber);
                recipient.SubscribeDate = DateTimeOffset.Now;
                recipient.TotalSubsReceived++;

                if (fireIndividualEvents)
                {
                    CommandParametersModel giftParameters = new CommandParametersModel(gifter, StreamingPlatformTypeEnum.Velora);
                    giftParameters.SpecialIdentifiers["isanonymous"] = isAnonymous.ToString();
                    giftParameters.SpecialIdentifiers["usersubtier"] = tier.ToString();
                    giftParameters.SpecialIdentifiers["usersubplan"] = GetSubPlanName(tier);
                    giftParameters.SpecialIdentifiers["usersubplanname"] = GetSubPlanName(tier);
                    giftParameters.TargetUser = recipient;
                    giftParameters.Arguments.Add(recipient.Username);
                    await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VeloraChannelSubscriptionGifted, giftParameters);
                }

                subscriptions.Add(new SubscriptionDetailsModel(StreamingPlatformTypeEnum.Velora, recipient, gifter, tier: tier));
            }

            if (recipients.Count == 0 && fireIndividualEvents)
            {
                // The payload did not identify the recipients, so fire the individual gifted event
                // once per gifted subscription without a target user.
                for (int i = 0; i < quantity; i++)
                {
                    CommandParametersModel giftParameters = new CommandParametersModel(gifter, StreamingPlatformTypeEnum.Velora);
                    giftParameters.SpecialIdentifiers["isanonymous"] = isAnonymous.ToString();
                    giftParameters.SpecialIdentifiers["usersubtier"] = tier.ToString();
                    giftParameters.SpecialIdentifiers["usersubplan"] = GetSubPlanName(tier);
                    giftParameters.SpecialIdentifiers["usersubplanname"] = GetSubPlanName(tier);
                    await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VeloraChannelSubscriptionGifted, giftParameters);
                }
            }

            if (!isAnonymous)
            {
                gifter.TotalSubsGifted += (uint)quantity;
            }

            if (fireMassEvent)
            {
                CommandParametersModel parameters = new CommandParametersModel(gifter, StreamingPlatformTypeEnum.Velora);
                parameters.SpecialIdentifiers["subsgiftedamount"] = quantity.ToString();
                parameters.SpecialIdentifiers["subsgiftedlifetimeamount"] = gifter.TotalSubsGifted.ToString();
                parameters.SpecialIdentifiers["isanonymous"] = isAnonymous.ToString();
                parameters.SpecialIdentifiers["usersubtier"] = tier.ToString();
                parameters.SpecialIdentifiers["usersubplan"] = GetSubPlanName(tier);
                parameters.SpecialIdentifiers["usersubplanname"] = GetSubPlanName(tier);
                foreach (SubscriptionDetailsModel sub in subscriptions)
                {
                    parameters.Arguments.Add(sub.User.Username);
                }

                await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VeloraChannelMassSubscriptionsGifted, parameters);
                if (subscriptions.Count > 0)
                {
                    EventService.MassSubscriptionsGiftedOccurred(subscriptions);
                }
                await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(gifter, string.Format(MixItUp.Base.Resources.AlertMassSubscriptionsGiftedTier, gifter.FullDisplayName, quantity, $"Velora Tier {tier}"), ChannelSession.Settings.AlertMassGiftedSubColor));
            }
        }

        private async Task HandleSubscriptionEnd(JObject payload)
        {
            WebhookSubscriptionEndEventModel subEndEvent = payload.ToObject<WebhookSubscriptionEndEventModel>();
            if (subEndEvent?.ResolvedUser == null)
            {
                return;
            }

            UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Velora, platformID: subEndEvent.ResolvedUser.UserID, platformUsername: subEndEvent.ResolvedUser.Username);
            if (user != null)
            {
                user.Roles.Remove(UserRoleEnum.Subscriber);
            }
        }

        private async Task HandleCheer(JObject payload)
        {
            WebhookCheerEventModel cheerEvent = payload.ToObject<WebhookCheerEventModel>();
            UserV2ViewModel user = await this.GetOrCreateUser(cheerEvent?.ResolvedUser);
            if (user == null || cheerEvent.ResolvedAmount <= 0)
            {
                return;
            }

            VeloraChatMessageViewModel message = new VeloraChatMessageViewModel(user, cheerEvent.Message);

            CommandParametersModel parameters = new CommandParametersModel(user, StreamingPlatformTypeEnum.Velora);
            parameters.SpecialIdentifiers["cheeramount"] = cheerEvent.ResolvedAmount.ToString();
            parameters.SpecialIdentifiers["voltsamount"] = cheerEvent.ResolvedAmount.ToString();
            parameters.SpecialIdentifiers["message"] = cheerEvent.Message ?? string.Empty;
            parameters.SpecialIdentifiers["messagenoemotes"] = message.TextOnlyMessageContents ?? string.Empty;
            await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VeloraChannelCheered, parameters);
            await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format(MixItUp.Base.Resources.AlertVeloraCheered, user.FullDisplayName, cheerEvent.ResolvedAmount), ChannelSession.Settings.AlertVeloraCheeredColor));

            EventService.VeloraChannelCheeredOccurred(new VeloraCheeredEventModel(user, cheerEvent.ResolvedAmount, cheerEvent.Message));
        }

        private async Task HandleRaid(JObject payload)
        {
            WebhookRaidEventModel raidEvent = payload.ToObject<WebhookRaidEventModel>();
            UserV2ViewModel user = await this.GetOrCreateUser(raidEvent?.ResolvedUser);
            if (user == null)
            {
                return;
            }

            int viewers = raidEvent.ResolvedViewers;

            CommandParametersModel parameters = new CommandParametersModel(user, StreamingPlatformTypeEnum.Velora);
            parameters.SpecialIdentifiers["hostviewercount"] = viewers.ToString();
            parameters.SpecialIdentifiers["raidviewercount"] = viewers.ToString();

            if (await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VeloraChannelRaided, parameters))
            {
                ChannelSession.Settings.LatestSpecialIdentifiersData[SpecialIdentifierStringBuilder.LatestRaidUserData] = user.ID;
                ChannelSession.Settings.LatestSpecialIdentifiersData[SpecialIdentifierStringBuilder.LatestRaidViewerCountData] = viewers;

                foreach (CurrencyModel currency in ChannelSession.Settings.Currency.Values.ToList())
                {
                    currency.AddAmount(user, currency.OnHostBonus);
                }

                foreach (StreamPassModel streamPass in ChannelSession.Settings.StreamPass.Values)
                {
                    if (user.MeetsRole(streamPass.UserPermission))
                    {
                        streamPass.AddAmount(user, streamPass.HostBonus);
                    }
                }

                EventService.RaidOccurred(user, viewers);

                await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format(MixItUp.Base.Resources.AlertRaid, user.FullDisplayName, viewers), ChannelSession.Settings.AlertRaidColor));
            }
        }

        private async Task HandleBan(JObject payload)
        {
            WebhookModerationEventModel moderationEvent = payload.ToObject<WebhookModerationEventModel>();
            UserV2ViewModel bannedUser = await this.GetOrCreateUser(moderationEvent?.ResolvedUser);
            if (bannedUser == null)
            {
                // The live ban/timeout payload shapes are unverified (VERIFY-LIVE in the migration spec), so a
                // shape the model cannot resolve a user from must be surfaced, not dropped silently.
                Logger.Log(LogLevel.Error, $"Velora ban/timeout event carried no resolvable user: {payload?.ToString(Newtonsoft.Json.Formatting.None)}");
                return;
            }

            int timeoutLength = 0;
            if (moderationEvent.IsPermanent ?? false)
            {
                // Permanent ban: keep timeoutLength at 0 so the ban branch below is taken.
            }
            else if (moderationEvent.ResolvedDurationSeconds.HasValue)
            {
                timeoutLength = Math.Max(0, moderationEvent.ResolvedDurationSeconds.Value);
            }
            else if (!string.IsNullOrWhiteSpace(moderationEvent.ExpiresAt))
            {
                DateTimeOffset createdAt = DateTimeOffset.Now;
                if (!string.IsNullOrWhiteSpace(moderationEvent.CreatedAt) && DateTimeOffset.TryParse(moderationEvent.CreatedAt, out DateTimeOffset createdAtResult))
                {
                    createdAt = createdAtResult;
                }

                if (DateTimeOffset.TryParse(moderationEvent.ExpiresAt, out DateTimeOffset expiresAt))
                {
                    timeoutLength = Math.Max(0, (int)Math.Round((expiresAt - createdAt).TotalSeconds));
                }
            }

            // channel.ban (Events WS) and userBanned / userTimedOut (Chat WS) can both fire for one
            // moderation action, so dedupe across the sockets on the resolved user + timeout/ban + expiry.
            string moderationDedupKey = (timeoutLength > 0)
                ? $"velora.mod.timeout:{bannedUser.PlatformID}:{moderationEvent.ExpiresAt ?? moderationEvent.ResolvedDurationSeconds?.ToString() ?? string.Empty}"
                : $"velora.mod.ban:{bannedUser.PlatformID}";
            if (!this.ShouldProcessSocketEvent(moderationDedupKey))
            {
                return;
            }

            if (timeoutLength > 0)
            {
                CommandParametersModel parameters = new CommandParametersModel(StreamingPlatformTypeEnum.Velora);
                parameters.Arguments.Add("@" + bannedUser.Username);
                parameters.TargetUser = bannedUser;
                parameters.SpecialIdentifiers["timeoutlength"] = timeoutLength.ToString();
                parameters.SpecialIdentifiers["timeoutreason"] = moderationEvent.Reason ?? string.Empty;
                await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.ChatUserTimeout, parameters);

                await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(bannedUser, string.Format(MixItUp.Base.Resources.AlertTimedOut, bannedUser.FullDisplayName, timeoutLength), ChannelSession.Settings.AlertModerationColor));
                ChatService.ChatUserTimedOut(bannedUser);

                // A timeout clears the user's visible messages just as a ban does - Twitch's clear_user_messages
                // fires for both - and a purge is a one-second timeout, so without this it appears to do nothing.
                int purged = await ServiceManager.Get<ChatService>().MarkUserMessagesAsDeleted(bannedUser, reason: moderationEvent.Reason);
                Logger.Log(LogLevel.Debug, $"Velora timeout echo purge: {purged} message(s) marked deleted for {bannedUser.Username}");
            }
            else
            {
                CommandParametersModel parameters = new CommandParametersModel(StreamingPlatformTypeEnum.Velora);
                parameters.Arguments.Add("@" + bannedUser.Username);
                parameters.TargetUser = bannedUser;
                await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.ChatUserBan, parameters);

                await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(bannedUser, string.Format(MixItUp.Base.Resources.AlertBanned, bannedUser.FullDisplayName), ChannelSession.Settings.AlertModerationColor));
                ChatService.ChatUserBanned(bannedUser);

                // A ban should purge the banned user's visible messages (the Chat WS userBanned contract).
                int purged = await ServiceManager.Get<ChatService>().MarkUserMessagesAsDeleted(bannedUser, reason: moderationEvent.Reason);
                Logger.Log(LogLevel.Debug, $"Velora ban echo purge: {purged} message(s) marked deleted for {bannedUser.Username}");
            }
        }

        private async Task HandleUnban(JObject payload)
        {
            WebhookModerationEventModel moderationEvent = payload.ToObject<WebhookModerationEventModel>();
            if (moderationEvent?.ResolvedUser != null)
            {
                Logger.Log(LogLevel.Debug, $"Velora user unbanned: {moderationEvent.ResolvedUser.Username}");
            }
            await Task.CompletedTask;
        }

        private async Task HandleModerator(JObject payload, bool added)
        {
            WebhookModerationEventModel moderationEvent = payload.ToObject<WebhookModerationEventModel>();
            // moderator.add/remove carry the affected user under "moderator", not "user"/"target".
            UserV2ViewModel user = await this.GetOrCreateUser(moderationEvent?.ResolvedModerator);
            if (user == null)
            {
                return;
            }

            if (added)
            {
                user.Roles.Add(UserRoleEnum.Moderator);
            }
            else
            {
                user.Roles.Remove(UserRoleEnum.Moderator);
            }
        }

        private async Task HandleChannelPointsRedemption(JObject payload)
        {
            WebhookChannelPointsRedemptionEventModel redemptionEvent = payload.ToObject<WebhookChannelPointsRedemptionEventModel>();
            if (redemptionEvent == null || string.IsNullOrWhiteSpace(redemptionEvent.RewardTitle))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(redemptionEvent.RedemptionID) && this.channelPointRedemptionCache.Contains(redemptionEvent.RedemptionID))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(redemptionEvent.Status) &&
                (string.Equals(redemptionEvent.Status, "canceled", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(redemptionEvent.Status, "cancelled", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(redemptionEvent.Status, "rejected", StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(redemptionEvent.RedemptionID))
            {
                this.channelPointRedemptionCache.Add(redemptionEvent.RedemptionID);
            }

            UserV2ViewModel user = await this.GetOrCreateUser(redemptionEvent.ResolvedUser);
            if (user == null)
            {
                return;
            }

            List<string> arguments = null;
            CommandParametersModel parameters = new CommandParametersModel(user, StreamingPlatformTypeEnum.Velora);
            parameters.SpecialIdentifiers["rewardname"] = redemptionEvent.RewardTitle;
            parameters.SpecialIdentifiers["rewardcost"] = redemptionEvent.RewardCost.ToString();
            if (!string.IsNullOrWhiteSpace(redemptionEvent.UserInput))
            {
                VeloraChatMessageViewModel message = new VeloraChatMessageViewModel(user, redemptionEvent.UserInput);
                parameters.SpecialIdentifiers["message"] = redemptionEvent.UserInput;
                parameters.SpecialIdentifiers["messagenoemotes"] = message.TextOnlyMessageContents;
                parameters.SpecialIdentifiers["messageemotecount"] = message.EmotesOnlyContents.Count().ToString();
                arguments = new List<string>(redemptionEvent.UserInput.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
                parameters.Arguments.AddRange(arguments);
            }

            await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VeloraChannelPointsRedeemed, parameters);
            await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format(MixItUp.Base.Resources.AlertVeloraChannelPointRedeemed, user.FullDisplayName, redemptionEvent.RewardTitle), ChannelSession.Settings.AlertVeloraChannelPointsColor));

            EventService.ChannelPointsRedeemedOccurred(user, redemptionEvent.RewardCost);

            VeloraChannelPointsCommandModel command = ServiceManager.Get<CommandService>().VeloraChannelPointsCommands.FirstOrDefault(c => string.Equals(c.ChannelPointRewardID, redemptionEvent.RewardID, StringComparison.OrdinalIgnoreCase));
            if (command == null)
            {
                command = ServiceManager.Get<CommandService>().VeloraChannelPointsCommands.FirstOrDefault(c => string.Equals(c.Name, redemptionEvent.RewardTitle, StringComparison.CurrentCultureIgnoreCase));
            }

            if (command != null)
            {
                Dictionary<string, string> channelPointSpecialIdentifiers = new Dictionary<string, string>(parameters.SpecialIdentifiers);
                await ServiceManager.Get<CommandService>().Queue(command, new CommandParametersModel(user, platform: StreamingPlatformTypeEnum.Velora, arguments: arguments, specialIdentifiers: channelPointSpecialIdentifiers));
            }
        }

        private async Task HandleStreamOnline(JObject payload)
        {
            WebhookStreamEventModel streamEvent = payload.ToObject<WebhookStreamEventModel>();

            ServiceManager.Get<VeloraSession>().ApplyStreamStatusUpdate(true, streamEvent?.Title, streamEvent?.StartedAt);

            // Going live carries the category the stream started with. Apply it before the event fires
            // so stream-start commands see the current category instead of whatever the last background
            // refresh left behind.
            await ServiceManager.Get<VeloraSession>().ApplyMetadataUpdate(streamEvent?.Title, streamEvent?.ResolvedCategorySlug, streamEvent?.ResolvedCategoryName, streamEvent?.ResolvedCategoryImageUrl);

            await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VeloraChannelStreamStart, new CommandParametersModel(StreamingPlatformTypeEnum.Velora));
        }

        private async Task HandleStreamOffline(JObject payload)
        {
            WebhookStreamEventModel streamEvent = payload.ToObject<WebhookStreamEventModel>();

            ServiceManager.Get<VeloraSession>().ApplyStreamStatusUpdate(false, streamEvent?.Title, null);

            await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VeloraChannelStreamStop, new CommandParametersModel(StreamingPlatformTypeEnum.Velora));
        }

        private async Task HandleStreamUpdate(JObject payload)
        {
            WebhookStreamEventModel streamEvent = payload.ToObject<WebhookStreamEventModel>();
            if (streamEvent == null)
            {
                return;
            }

            await ServiceManager.Get<VeloraSession>().ApplyMetadataUpdate(streamEvent.Title, streamEvent.ResolvedCategorySlug, streamEvent.ResolvedCategoryName, streamEvent.ResolvedCategoryImageUrl);

            CommandParametersModel parameters = new CommandParametersModel(ChannelSession.User, StreamingPlatformTypeEnum.Velora);
            parameters.SpecialIdentifiers["streamtitle"] = streamEvent.Title ?? string.Empty;
            parameters.SpecialIdentifiers["streamgameid"] = streamEvent.ResolvedCategorySlug ?? string.Empty;
            parameters.SpecialIdentifiers["streamgame"] = streamEvent.ResolvedCategoryName ?? string.Empty;
            parameters.SpecialIdentifiers["streamgamename"] = streamEvent.ResolvedCategoryName ?? string.Empty;
            parameters.SpecialIdentifiers["streamgameimage"] = ServiceManager.Get<VeloraSession>().StreamCategoryImageURL ?? string.Empty;
            await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.VeloraChannelUpdated, parameters);
        }

        // Dedup for socket-sourced events, which carry no per-event ID from the transport: chat message
        // IDs and the ban/timeout composite keys that collapse the Chat-WS + Events-WS duplicates.
        private bool ShouldProcessSocketEvent(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return true;
            }

            lock (this.processedEventIDsLock)
            {
                if (!this.processedEventIDs.Add(key))
                {
                    return false;
                }

                this.processedEventIDsQueue.Enqueue(key);
                while (this.processedEventIDsQueue.Count > MaxProcessedEventsCacheSize)
                {
                    this.processedEventIDs.Remove(this.processedEventIDsQueue.Dequeue());
                }
            }
            return true;
        }
    }
}

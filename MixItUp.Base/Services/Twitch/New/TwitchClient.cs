using MixItUp.Base.Model;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.Currency;
using MixItUp.Base.Model.Overlay;
using MixItUp.Base.Model.Twitch.Bits;
using MixItUp.Base.Model.Twitch.Channels;
using MixItUp.Base.Model.Twitch.Clients.EventSub;
using MixItUp.Base.Model.Twitch.EventSub;
using MixItUp.Base.Model.Twitch.Subscriptions;
using MixItUp.Base.Model.User;
using MixItUp.Base.Model.User.Platform;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Chat;
using MixItUp.Base.ViewModel.Chat.Twitch;
using MixItUp.Base.ViewModel.User;
using MixItUp.Base.Web;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Base.Services.Twitch.New
{
    public class TwitchClient : ServiceClientBase
    {
        public const string TwitchEventSubConnectionURL = "wss://eventsub.wss.twitch.tv/ws";

        public const string PrimeSubPlan = "Prime";

        public static int GetSubPoints(int tier)
        {
            if (tier == 3)
            {
                return 6;
            }
            return tier;
        }

        public static int GetSubTierNumberFromText(string subPlan)
        {
            if (int.TryParse(subPlan, out int subPlanNumber) && subPlanNumber >= 1000)
            {
                return subPlanNumber / 1000;
            }
            return 1;
        }

        private readonly IReadOnlyDictionary<string, string> DesiredSubscriptionsAndVersions = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "stream.online", null },
            { "stream.offline", null },

            { "channel.update", "2" },

            { "channel.follow", "2" },
            { "channel.raid", null },

            { "channel.poll.begin", null },
            { "channel.poll.progress", null },
            { "channel.poll.end", null },

            { "channel.prediction.begin", null },
            { "channel.prediction.progress", null },
            { "channel.prediction.lock", null },
            { "channel.prediction.end", null },

            { "channel.ad_break.begin", null },

            { "channel.hype_train.begin", "2" },
            { "channel.hype_train.progress", "2" },
            { "channel.hype_train.end", "2" },

            { "channel.charity_campaign.donate", null },

            { "user.whisper.message", null },

            //{ "channel.subscribe", null },
            //{ "channel.subscription.message", null },
            //{ "channel.subscription.gift", null },

            { "channel.channel_points_automatic_reward_redemption.add", null },
            { "channel.channel_points_custom_reward_redemption.add", null },
            { "channel.custom_power_up_redemption.add", null },

            { "channel.chat.message", null },
            { "channel.chat.message_delete", null },
            { "channel.chat.notification", null },
            { "channel.chat.user_message_hold", null },
            { "channel.chat.user_message_update", null },
            { "channel.chat.clear", null },
            { "channel.chat.clear_user_messages", null },

            { "channel.shared_chat.begin", null },
            { "channel.shared_chat.update", null },
            { "channel.shared_chat.end", null },

            { "channel.cheer", null },

            { "channel.moderate", "2" },

            { "channel.unban_request.create", null },
            { "channel.unban_request.resolve", null },

            { "channel.shoutout.receive", null },

            { "channel.suspicious_user.message", null },
            { "channel.suspicious_user.update", null },

            { "channel.shield_mode.begin", null },
            { "channel.shield_mode.end", null },

            { "channel.goal.begin", null },
            { "channel.goal.progress", null },
            { "channel.goal.end", null },
        };

        private const int EventSubSubscriptionListAttempts = 3;
        private const int EventSubSubscriptionListRetryDelay = 1500;
        private const int EventSubRegistrationTimeout = 120000;

        public override bool IsConnected { get { return this.webSocket != null && this.webSocket.IsOpen() && this.eventSubSubscriptionsConnected; } }

        private AdvancedClientWebSocket webSocket;

        private bool eventSubSubscriptionsConnected = false;

        // Bumped on every session welcome so a slow registration pass cannot report back against a
        // session that has since been replaced
        private int eventSubSessionGeneration = 0;

        private Task<bool> eventSubRegistrationTask;

        private HashSet<string> followCache = new HashSet<string>();
        private HashSet<string> channelPointRewardRedeemsCache = new HashSet<string>();
        private HashSet<string> customPowerUpRedeemsCache = new HashSet<string>();

        private int lastHypeTrainLevel = 1;

        private bool isReconnecting = false;

        public TwitchClient()
        {
            this.webSocket = new AdvancedClientWebSocket();

            this.webSocket.PacketSent += WebSocket_PacketSent;
            this.webSocket.PacketReceived += UserWebSocket_PacketReceived;
            this.webSocket.Disconnected += WebSocket_Disconnected;
        }

        public override async Task<Result> Connect()
        {
            this.eventSubRegistrationTask = null;

            if (await this.webSocket.Connect(TwitchEventSubConnectionURL + "?keepalive_timeout_seconds=120", CancellationToken.None))
            {
                await Task.Delay(2500);

                for (int i = 0; i < 15 && this.eventSubRegistrationTask == null; i++)
                {
                    await Task.Delay(1000);
                }

                Task<bool> registration = this.eventSubRegistrationTask;
                if (registration != null)
                {
                    // Registering the full desired set is dozens of sequential calls, so wait on the work
                    // itself finishing rather than on a fixed budget it can overrun on a slow connection
                    if (await Task.WhenAny(registration, Task.Delay(EventSubRegistrationTimeout)) == registration && this.eventSubSubscriptionsConnected)
                    {
                        ChannelSession.ReconnectionOccurred(Resources.TwitchClient);

                        return new Result();
                    }
                }
            }

            return new Result(Resources.TwitchEventServiceFailedToConnectEventSub);
        }

        public override async Task Disconnect()
        {
            await this.webSocket.Disconnect();
        }

        public async Task ProcessMockNotification(NotificationMessage message)
        {
            await this.ProcessNotification(message);
        }

        private Task ProcessSessionWelcome(WelcomeMessage message)
        {
            int generation = Interlocked.Increment(ref this.eventSubSessionGeneration);

            // Twitch closes the session if the welcome is not answered promptly, so the registration work
            // stays off the packet handler. Connect() waits on the task rather than polling for a flag
            this.eventSubRegistrationTask = AsyncRunner.RunAsyncBackground(async (cancellationToken) =>
            {
                bool registered = await this.RegisterAllEventSubSubscriptions(message);

                if (Volatile.Read(ref this.eventSubSessionGeneration) == generation)
                {
                    this.eventSubSubscriptionsConnected = registered;
                }

                return registered;
            }, CancellationToken.None);

            return Task.CompletedTask;
        }

        private async Task<bool> RegisterAllEventSubSubscriptions(WelcomeMessage message)
        {
            IEnumerable<EventSubSubscriptionModel> allSubs = await this.GetExistingEventSubSubscriptions();
            if (allSubs == null)
            {
                // Registering blind against an unknown set of existing subscriptions would double them up,
                // so report a failed connection and let the reconnect loop come back around
                Logger.Log(LogLevel.Error, "Could not read the existing Twitch EventSub subscriptions, treating EventSub as not connected");
                return false;
            }

            List<Task> subscriptionDeletionTasks = new List<Task>();

            HashSet<string> missingSubs = new HashSet<string>(DesiredSubscriptionsAndVersions.Keys, StringComparer.OrdinalIgnoreCase);
            foreach (EventSubSubscriptionModel sub in allSubs)
            {
                if (DesiredSubscriptionsAndVersions.ContainsKey(sub.type) && string.Equals(sub.status, "connected", StringComparison.OrdinalIgnoreCase))
                {
                    // Sub exists and is connected, remove from missing
                    missingSubs.Remove(sub.type);
                }
                else
                {
                    // Got a sub we don't want, delete
                    subscriptionDeletionTasks.Add(ServiceManager.Get<TwitchSession>().StreamerService.DeleteEventSubSubscription(sub.id));
                }
            }
            await Task.WhenAll(subscriptionDeletionTasks);

            int registrationsAttempted = 0;
            int registrationsFailed = 0;
            foreach (string missingSub in missingSubs)
            {
                if (missingSub.Equals("channel.raid", StringComparison.OrdinalIgnoreCase))
                {
                    registrationsAttempted += 2;
                    if (!await this.RegisterEventSubSubscription(missingSub, message, DesiredSubscriptionsAndVersions[missingSub],
                        new Dictionary<string, string> { { "from_broadcaster_user_id", ServiceManager.Get<TwitchSession>().StreamerID } }))
                    {
                        registrationsFailed++;
                    }
                    if (!await this.RegisterEventSubSubscription(missingSub, message, DesiredSubscriptionsAndVersions[missingSub],
                        new Dictionary<string, string> { { "to_broadcaster_user_id", ServiceManager.Get<TwitchSession>().StreamerID } }))
                    {
                        registrationsFailed++;
                    }
                }
                else
                {
                    Dictionary<string, string> conditions = new Dictionary<string, string>
                    {
                        { "broadcaster_user_id", ServiceManager.Get<TwitchSession>().StreamerID }
                    };

                    switch (missingSub)
                    {
                        case "channel.follow":
                        case "channel.moderate":
                        case "channel.unban_request.create":
                        case "channel.unban_request.resolve":
                        case "channel.shoutout.receive":
                        case "channel.suspicious_user.message":
                        case "channel.suspicious_user.update":
                        case "channel.shield_mode.begin":
                        case "channel.shield_mode.end":
                            conditions["moderator_user_id"] = ServiceManager.Get<TwitchSession>().StreamerID;
                            break;

                        case "channel.chat.message":
                        case "channel.chat.message_delete":
                        case "channel.chat.notification":
                        case "channel.chat.user_message_hold":
                        case "channel.chat.user_message_update":
                        case "channel.chat.clear":
                        case "channel.chat.clear_user_messages":
                        case "user.whisper.message":
                            conditions["user_id"] = ServiceManager.Get<TwitchSession>().StreamerID;
                            break;
                    }

                    registrationsAttempted++;
                    if (!await this.RegisterEventSubSubscription(missingSub, message, DesiredSubscriptionsAndVersions[missingSub], conditions))
                    {
                        registrationsFailed++;
                    }
                }
            }

            // Individual failures stay tolerated and logged, the same as before, but registering nothing
            // at all means no events can arrive and must not be reported as a healthy connection
            if (registrationsAttempted > 0 && registrationsFailed == registrationsAttempted)
            {
                Logger.Log(LogLevel.Error, $"All {registrationsAttempted} Twitch EventSub subscription registrations failed, treating EventSub as not connected");
                return false;
            }

            IEnumerable<ChannelFollowerModel> followers = await ServiceManager.Get<TwitchSession>().StreamerService.GetNewAPIFollowers(ServiceManager.Get<TwitchSession>().StreamerModel, maxResults: 100);
            if (followers != null)
            {
                this.followCache.Clear();
                foreach (ChannelFollowerModel follow in followers)
                {
                    this.followCache.Add(follow.user_id);
                }
            }

            return true;
        }

        private async Task<IEnumerable<EventSubSubscriptionModel>> GetExistingEventSubSubscriptions()
        {
            // A stale access token comes back as a 401, which refreshes the token but does not re-send the
            // call, so the first attempt after a resume from sleep comes back empty handed
            for (int attempt = 1; attempt <= EventSubSubscriptionListAttempts; attempt++)
            {
                IEnumerable<EventSubSubscriptionModel> subs = await ServiceManager.Get<TwitchSession>().StreamerService.GetEventSubSubscriptions();
                if (subs != null)
                {
                    return subs;
                }

                Logger.Log(LogLevel.Warning, $"Failed to list Twitch EventSub subscriptions, attempt {attempt} of {EventSubSubscriptionListAttempts}");

                if (attempt < EventSubSubscriptionListAttempts)
                {
                    await Task.Delay(EventSubSubscriptionListRetryDelay);
                }
            }
            return null;
        }

        private async Task<bool> RegisterEventSubSubscription(string type, WelcomeMessage message, string version = null, Dictionary<string, string> conditions = null)
        {
            try
            {
                if (conditions == null)
                {
                    conditions = new Dictionary<string, string> { { "broadcaster_user_id", ServiceManager.Get<TwitchSession>().StreamerID } };
                }

                // The create call logs and swallows its own failures, handing back null, so the result has
                // to be checked rather than waiting for an exception that will not come
                EventSubSubscriptionModel subscription = await ServiceManager.Get<TwitchSession>().StreamerService.CreateEventSubSubscription(type, "websocket", conditions, message.Payload.Session.Id, version: version);
                if (subscription == null)
                {
                    Logger.Log(LogLevel.Error, $"Failed to connect EventSub for {type}");
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                Logger.Log(LogLevel.Error, $"Failed to connect EventSub for {type}");

                // Note: Do not re-throw, but log and move on, better to miss some events than to cause a retry loop
                return false;
            }
        }

        private bool IsSharedChatEvent(JObject payload)
        {
            string streamerID = ServiceManager.Get<TwitchSession>().StreamerID;
            string sourceBroadcasterID = payload.GetValueOrDefault<string>("source_broadcaster_user_id", null);

            // If there's a source broadcaster ID and it's not ours, this is a shared chat event from another channel
            if (!string.IsNullOrEmpty(sourceBroadcasterID) &&
                !string.Equals(sourceBroadcasterID, streamerID, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string broadcasterID = payload.GetValueOrDefault<string>("broadcaster_user_id", null);
            return !string.IsNullOrEmpty(broadcasterID) &&
                   !string.Equals(broadcasterID, streamerID, StringComparison.OrdinalIgnoreCase);
        }

        private async Task ProcessNotification(NotificationMessage message)
        {
            try
            {
                // Shared chat event check
                if (!ChannelSession.Settings.TwitchAllowSharedChatEvents && IsSharedChatEvent(message.Payload.Event))
                {
                    Logger.Log(LogLevel.Debug, $"Ignoring shared chat event from other channel: {message.Metadata.SubscriptionType}");
                    return;
                }
                switch (message.Metadata.SubscriptionType)
                {
                    case "stream.online":
                        await HandleOnline(message.Payload.Event);
                        break;
                    case "stream.offline":
                        await HandleOffline(message.Payload.Event);
                        break;

                    case "channel.update":
                        await HandleChannelUpdate(message.Payload.Event);
                        break;

                    case "channel.follow":
                        await HandleFollow(message.Payload.Event);
                        break;
                    case "channel.raid":
                        await HandleRaid(message.Payload.Event);
                        break;

                    case "channel.poll.begin":
                        await HandlePollStart(message.Payload.Event);
                        break;
                    case "channel.poll.progress":
                        await HandlePollProgress(message.Payload.Event);
                        break;
                    case "channel.poll.end":
                        await HandlePollEnd(message.Payload.Event);
                        break;

                    case "channel.prediction.begin":
                        await HandlePredictionStart(message.Payload.Event);
                        break;
                    case "channel.prediction.progress":
                        await HandlePredictionProgress(message.Payload.Event);
                        break;
                    case "channel.prediction.lock":
                        await HandlePredictionLock(message.Payload.Event);
                        break;
                    case "channel.prediction.end":
                        await HandlePredictionEnd(message.Payload.Event);
                        break;

                    case "channel.ad_break.begin":
                        await HandleChannelAdBreakBegin(message.Payload.Event);
                        break;

                    case "channel.hype_train.begin":
                        await HandleHypeTrainBegin(message.Payload.Event);
                        break;
                    case "channel.hype_train.progress":
                        await HandleHypeTrainProgress(message.Payload.Event);
                        break;
                    case "channel.hype_train.end":
                        await HandleHypeTrainEnd(message.Payload.Event);
                        break;

                    case "channel.charity_campaign.donate":
                        await HandleCharityCampaignDonation(message.Payload.Event);
                        break;

                    case "user.whisper.message":
                        await HandleWhisper(message.Payload.Event);
                        break;

                    //case "channel.subscribe":
                    //    await HandleSubscription(message.Payload.Event);
                    //    break;
                    //case "channel.subscription.message":
                    //    await HandleSubscriptionMessage(message.Payload.Event);
                    //    break;
                    //case "channel.subscription.gift":
                    //    await HandleSubscriptionGift(message.Payload.Event);
                    //    break;

                    case "channel.channel_points_automatic_reward_redemption.add":
                        await HandleChannelPointAutomaticRewardRedemptionAdd(message.Payload.Event);
                        break;
                    case "channel.channel_points_custom_reward_redemption.add":
                        await HandleChannelPointRewardAddCustomRedemption(message.Payload.Event);
                        break;
                    case "channel.custom_power_up_redemption.add":
                        await HandleChannelCustomPowerUpRedemptionAdd(message.Payload.Event);
                        break;

                    case "channel.chat.message":
                        await HandleChatMessage(message.Payload.Event);
                        break;
                    case "channel.chat.message_delete":
                        await HandleChatMessageDeleted(message.Payload.Event);
                        break;
                    case "channel.chat.notification":
                        await HandleChatNotification(message.Payload.Event);
                        break;
                    case "channel.chat.user_message_hold":
                        await HandleChatMessageHeld(message.Payload.Event);
                        break;
                    case "channel.chat.user_message_update":
                        await HandleChatMessageHoldUpdate(message.Payload.Event);
                        break;
                    case "channel.chat.clear":
                        await HandleChatClear(message.Payload.Event);
                        break;
                    case "channel.chat.clear_user_messages":
                        await HandleChatUserClear(message.Payload.Event);
                        break;

                    case "channel.shared_chat.begin":
                        await HandleSharedChatBegin(message.Payload.Event);
                        break;
                    case "channel.shared_chat.update":
                        await HandleSharedChatUpdate(message.Payload.Event);
                        break;
                    case "channel.shared_chat.end":
                        await HandleSharedChatEnd(message.Payload.Event);
                        break;

                    case "channel.moderate":
                        await HandleModeration(message.Payload.Event);
                        break;

                    case "channel.unban_request.create":
                        await HandleUnbanRequestCreate(message.Payload.Event);
                        break;
                    case "channel.unban_request.resolve":
                        await HandleUnbanRequestResolve(message.Payload.Event);
                        break;

                    case "channel.shoutout.receive":
                        await HandleShoutoutReceived(message.Payload.Event);
                        break;

                    case "channel.suspicious_user.message":
                        await HandleSuspiciousUserMessage(message.Payload.Event);
                        break;
                    case "channel.suspicious_user.update":
                        await HandleSuspiciousUserUpdate(message.Payload.Event);
                        break;

                    case "channel.shield_mode.begin":
                        await HandleShieldModeBegin(message.Payload.Event);
                        break;
                    case "channel.shield_mode.end":
                        await HandleShieldModeEnd(message.Payload.Event);
                        break;

                    case "channel.goal.begin":
                        await HandleGoalBegin(message.Payload.Event);
                        break;
                    case "channel.goal.progress":
                        await HandleGoalProgress(message.Payload.Event);
                        break;
                    case "channel.goal.end":
                        await HandleGoalEnd(message.Payload.Event);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                Logger.ForceLog(LogLevel.Error, JSONSerializerHelper.SerializeToString(message.Payload));
            }
        }

        private async Task HandleOnline(JObject payload)
        {
            await ServiceManager.Get<TwitchSession>().StreamOnline();
        }

        private async Task HandleOffline(JObject payload)
        {
            await ServiceManager.Get<TwitchSession>().StreamOffline();
        }

        private async Task HandleChannelUpdate(JObject payload)
        {
            await ServiceManager.Get<TwitchSession>().ChannelUpdated(payload.ToObject<ChannelUpdateNotification>());
        }

        private async Task HandleFollow(JObject payload)
        {
            string followerId = payload["user_id"].Value<string>();
            string followerUsername = payload["user_login"].Value<string>();
            string followerDisplayName = payload["user_name"].Value<string>();

            UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Twitch, platformID: followerId, platformUsername: followerUsername);
            if (user == null)
            {
                user = await ServiceManager.Get<UserService>().CreateUser(new TwitchUserPlatformV2Model(followerId, followerUsername, followerDisplayName));
            }

            await this.AddFollow(user);
        }

        private async Task AddFollow(UserV2ViewModel user)
        {
            if (!this.followCache.Contains(user.PlatformID))
            {
                this.followCache.Add(user.PlatformID);

#pragma warning disable CS0612 // Type or member is obsolete
                if (user.HasRole(UserRoleEnum.Banned))
#pragma warning restore CS0612 // Type or member is obsolete
                {
                    return;
                }

                user.Roles.Add(UserRoleEnum.Follower);
                user.FollowDate = DateTimeOffset.Now;

                CommandParametersModel parameters = new CommandParametersModel(user, StreamingPlatformTypeEnum.Twitch);
                if (await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TwitchChannelFollowed, parameters))
                {
                    ChannelSession.Settings.LatestSpecialIdentifiersData[SpecialIdentifierStringBuilder.LatestFollowerUserData] = user.ID;

                    foreach (CurrencyModel currency in ChannelSession.Settings.Currency.Values)
                    {
                        currency.AddAmount(user, currency.OnFollowBonus);
                    }

                    foreach (StreamPassModel streamPass in ChannelSession.Settings.StreamPass.Values)
                    {
                        if (user.MeetsRole(streamPass.UserPermission))
                        {
                            streamPass.AddAmount(user, streamPass.FollowBonus);
                        }
                    }

                    await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TwitchChannelFollowed, parameters);

                    EventService.FollowOccurred(user);

                    await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format(MixItUp.Base.Resources.AlertFollow, user.FullDisplayName), ChannelSession.Settings.AlertFollowColor));
                }
            }
        }

        private async Task HandleRaid(JObject payload)
        {
            string fromId = payload.GetValueOrDefault<string>("from_broadcaster_user_id", string.Empty);
            string fromUsername = payload.GetValueOrDefault<string>("from_broadcaster_user_login", string.Empty);
            string fromDisplayName = payload.GetValueOrDefault<string>("from_broadcaster_user_name", string.Empty);

            string toId = payload.GetValueOrDefault<string>("to_broadcaster_user_id", string.Empty);
            string toUsername = payload.GetValueOrDefault<string>("to_broadcaster_user_login", string.Empty);

            int viewers = payload.GetValueOrDefault<int>("viewers", 0);

            if (string.IsNullOrEmpty(fromId) || string.IsNullOrEmpty(toId))
            {
                // Invalid raid event, ignore
                return;
            }

            // The streamer was raided by a channel
            if (string.Equals(toId, ServiceManager.Get<TwitchSession>().StreamerID, StringComparison.OrdinalIgnoreCase))
            {
                UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Twitch, platformID: fromId, platformUsername: fromUsername);
                if (user == null)
                {
                    user = await ServiceManager.Get<UserService>().CreateUser(new TwitchUserPlatformV2Model(fromId, fromUsername, fromDisplayName));
                }

                CommandParametersModel parameters = new CommandParametersModel(user, StreamingPlatformTypeEnum.Twitch);
                parameters.SpecialIdentifiers["hostviewercount"] = viewers.ToString();
                parameters.SpecialIdentifiers["raidviewercount"] = viewers.ToString();

                if (await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TwitchChannelRaided, parameters))
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
            // The streamer is raiding another channel
            else if (string.Equals(fromId, ServiceManager.Get<TwitchSession>().StreamerID, StringComparison.OrdinalIgnoreCase))
            {
                CommandParametersModel parameters = new CommandParametersModel(StreamingPlatformTypeEnum.Twitch, new List<string>() { toUsername });
                parameters.SpecialIdentifiers["hostviewercount"] = viewers.ToString();
                parameters.SpecialIdentifiers["raidviewercount"] = viewers.ToString();

                await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TwitchChannelOutgoingRaidCompleted, parameters);
            }
        }

        private async Task HandlePollStart(JObject payload)
        {
            IEnumerable<OverlayPollV3Model> widgets = OverlayPollV3Model.GetPollOverlayWidgets(forPolls: true);
            if (widgets.Count() > 0)
            {
                PollNotification poll = new PollNotification(payload);
                foreach (OverlayPollV3Model widget in widgets)
                {
                    await widget.NewTwitchPoll(poll);
                }
            }
        }

        private async Task HandlePollProgress(JObject payload)
        {
            IEnumerable<OverlayPollV3Model> widgets = OverlayPollV3Model.GetPollOverlayWidgets(forPolls: true);
            if (widgets.Count() > 0)
            {
                PollNotification poll = new PollNotification(payload);
                foreach (OverlayPollV3Model widget in widgets)
                {
                    await widget.UpdateTwitchPoll(poll);
                }
            }
        }

        private async Task HandlePollEnd(JObject payload)
        {
            IEnumerable<OverlayPollV3Model> widgets = OverlayPollV3Model.GetPollOverlayWidgets(forPolls: true);
            if (widgets.Count() > 0)
            {
                PollNotification poll = new PollNotification(payload);
                foreach (OverlayPollV3Model widget in widgets)
                {
                    await widget.End(poll.Choices.OrderByDescending(c => c.Votes).First().ID);
                }
            }
        }

        private async Task HandlePredictionStart(JObject payload)
        {
            IEnumerable<OverlayPollV3Model> widgets = OverlayPollV3Model.GetPollOverlayWidgets(forPredictions: true);
            if (widgets.Count() > 0)
            {
                PredictionNotification prediction = new PredictionNotification(payload);
                foreach (OverlayPollV3Model widget in widgets)
                {
                    await widget.NewTwitchPrediction(prediction);
                }
            }
        }

        private async Task HandlePredictionProgress(JObject payload)
        {
            IEnumerable<OverlayPollV3Model> widgets = OverlayPollV3Model.GetPollOverlayWidgets(forPredictions: true);
            if (widgets.Count() > 0)
            {
                PredictionNotification prediction = new PredictionNotification(payload);
                foreach (OverlayPollV3Model widget in widgets)
                {
                    await widget.UpdateTwitchPrediction(prediction);
                }
            }
        }

        private Task HandlePredictionLock(JObject payload)
        {
            return Task.CompletedTask;
        }

        private async Task HandlePredictionEnd(JObject payload)
        {
            IEnumerable<OverlayPollV3Model> widgets = OverlayPollV3Model.GetPollOverlayWidgets(forPredictions: true);
            if (widgets.Count() > 0)
            {
                PredictionNotification prediction = new PredictionNotification(payload);
                foreach (OverlayPollV3Model widget in widgets)
                {
                    await widget.End(prediction.WinningOutcomeID);
                }
            }
        }

        private async Task HandleChannelAdBreakBegin(JObject payload)
        {
            int.TryParse(payload["duration_seconds"].Value<string>(), out int duration);
            bool.TryParse(payload["is_automatic"].Value<string>(), out bool isAutomatic);

            Dictionary<string, string> eventCommandSpecialIdentifiers = new Dictionary<string, string>();
            eventCommandSpecialIdentifiers["adduration"] = duration.ToString();
            eventCommandSpecialIdentifiers["adisautomatic"] = isAutomatic.ToString();
            await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TwitchChannelAdStarted, new CommandParametersModel(ChannelSession.User, StreamingPlatformTypeEnum.Twitch, eventCommandSpecialIdentifiers));

            await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(StreamingPlatformTypeEnum.Twitch, string.Format(MixItUp.Base.Resources.AlertTwitchAdStarted, duration), ChannelSession.Settings.AlertTwitchAdsColor));

            if (duration > 0)
            {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                AsyncRunner.RunAsyncBackground(async (token) =>
                {
                    await Task.Delay(duration * 1000);

                    eventCommandSpecialIdentifiers = new Dictionary<string, string>();
                    eventCommandSpecialIdentifiers["adduration"] = duration.ToString();
                    eventCommandSpecialIdentifiers["adisautomatic"] = isAutomatic.ToString();
                    await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TwitchChannelAdEnded, new CommandParametersModel(ChannelSession.User, StreamingPlatformTypeEnum.Twitch, eventCommandSpecialIdentifiers));

                }, CancellationToken.None);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            }
        }

        private static Dictionary<string, string> GetHypeTrainIdentifiers(JObject payload)
        {
            string type = payload["type"]?.ToString() ?? ((bool?)payload["is_golden_kappa_train"] == true ? "golden_kappa" : "regular");
            return new Dictionary<string, string>
            {
                ["hypetraintype"] = type,
                ["hypetrainisgoldenkappa"] = (type == "golden_kappa").ToString().ToLowerInvariant(),
                ["hypetrainistreasure"] = (type == "treasure").ToString().ToLowerInvariant(),
                ["hypetrainisshared"] = ((bool?)payload["is_shared_train"] == true).ToString().ToLowerInvariant(),
            };
        }
        private async Task HandleHypeTrainBegin(JObject payload)
        {
            int totalPoints = payload["total"].Value<int>();
            int levelPoints = payload["progress"].Value<int>();
            int levelGoal = payload["goal"].Value<int>();
            int level = payload["level"].Value<int>();

            this.lastHypeTrainLevel = level;

            Dictionary<string, string> eventCommandSpecialIdentifiers = GetHypeTrainIdentifiers(payload);
            eventCommandSpecialIdentifiers["hypetraintotalpoints"] = totalPoints.ToString();
            eventCommandSpecialIdentifiers["hypetrainlevelpoints"] = levelPoints.ToString();
            eventCommandSpecialIdentifiers["hypetrainlevelgoal"] = levelGoal.ToString();
            eventCommandSpecialIdentifiers["hypetrainlevel"] = level.ToString();
            await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TwitchChannelHypeTrainBegin, new CommandParametersModel(ChannelSession.User, StreamingPlatformTypeEnum.Twitch, eventCommandSpecialIdentifiers));

            await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(StreamingPlatformTypeEnum.Twitch, MixItUp.Base.Resources.HypeTrainStarted, ChannelSession.Settings.AlertTwitchHypeTrainColor));
        }

        private async Task HandleHypeTrainProgress(JObject payload)
        {
            int level = payload["level"].Value<int>();
            int totalPoints = payload["total"].Value<int>();
            int levelPoints = payload["progress"].Value<int>();
            int levelGoal = payload["goal"].Value<int>();

            Dictionary<string, string> eventCommandSpecialIdentifiers = GetHypeTrainIdentifiers(payload);
            eventCommandSpecialIdentifiers["hypetraintotalpoints"] = totalPoints.ToString();
            eventCommandSpecialIdentifiers["hypetrainlevelpoints"] = levelPoints.ToString();
            eventCommandSpecialIdentifiers["hypetrainlevelgoal"] = levelGoal.ToString();
            eventCommandSpecialIdentifiers["hypetrainlevel"] = level.ToString();
            await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TwitchChannelHypeTrainProgress, new CommandParametersModel(ChannelSession.User, StreamingPlatformTypeEnum.Twitch, eventCommandSpecialIdentifiers));

            if (level > this.lastHypeTrainLevel)
            {
                this.lastHypeTrainLevel = level;

                eventCommandSpecialIdentifiers = GetHypeTrainIdentifiers(payload);
                eventCommandSpecialIdentifiers["hypetraintotalpoints"] = totalPoints.ToString();
                eventCommandSpecialIdentifiers["hypetrainlevelpoints"] = levelPoints.ToString();
                eventCommandSpecialIdentifiers["hypetrainlevelgoal"] = levelGoal.ToString();
                eventCommandSpecialIdentifiers["hypetrainlevel"] = level.ToString();
                await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TwitchChannelHypeTrainLevelUp, new CommandParametersModel(ChannelSession.User, StreamingPlatformTypeEnum.Twitch, eventCommandSpecialIdentifiers));

                await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(StreamingPlatformTypeEnum.Twitch, string.Format(MixItUp.Base.Resources.HypeTrainLevelUp, level.ToString()), ChannelSession.Settings.AlertTwitchHypeTrainColor));
            }
        }

        private async Task HandleHypeTrainEnd(JObject payload)
        {
            int level = payload["level"].Value<int>();
            int totalPoints = payload["total"].Value<int>();

            Dictionary<string, string> eventCommandSpecialIdentifiers = GetHypeTrainIdentifiers(payload);
            eventCommandSpecialIdentifiers["hypetraintotallevel"] = level.ToString();
            eventCommandSpecialIdentifiers["hypetraintotalpoints"] = totalPoints.ToString();
            await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TwitchChannelHypeTrainEnd, new CommandParametersModel(ChannelSession.User, StreamingPlatformTypeEnum.Twitch, eventCommandSpecialIdentifiers));

            await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(StreamingPlatformTypeEnum.Twitch, string.Format(MixItUp.Base.Resources.HypeTrainEndedReachedLevel, level.ToString()), ChannelSession.Settings.AlertTwitchHypeTrainColor));
        }

        private async Task HandleCharityCampaignDonation(JObject payload)
        {
            CharityDonationNotification donation = new CharityDonationNotification(payload);

            Dictionary<string, string> additionalSpecialIdentifiers = new Dictionary<string, string>();
            additionalSpecialIdentifiers["charityname"] = donation.CharityName;
            additionalSpecialIdentifiers["charityimage"] = donation.CharityImage;

            await EventService.ProcessDonationEvent(EventTypeEnum.TwitchChannelCharityDonation, donation.ToGenericDonation(), additionalSpecialIdentifiers: additionalSpecialIdentifiers);
        }

        private async Task HandleWhisper(JObject payload)
        {
            string from_id = payload["from_user_id"].Value<string>();

            UserV2ViewModel from = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Twitch, platformID: from_id);
            if (from == null)
            {
                string from_username = payload["from_user_login"].Value<string>();
                string from_displayname = payload["from_user_name"].Value<string>();

                from = await ServiceManager.Get<UserService>().CreateUser(new TwitchUserPlatformV2Model(from_id, from_username, from_displayname));
            }

            string to_id = payload["to_user_id"].Value<string>();
            UserV2ViewModel to = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Twitch, platformID: to_id);

            string message = payload["whisper"]["text"].Value<string>();

            await ServiceManager.Get<ChatService>().AddMessage(new TwitchChatMessageViewModel(from, message, recipient: to));
        }

        //private async Task HandleSubscription(JObject payload)
        //{
        //    UserSubscriptionNotification subscription = payload.ToObject<UserSubscriptionNotification>();
        //    UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Twitch, platformID: subscription.user_id, platformUsername: subscription.user_name);
        //    if (user == null)
        //    {
        //        user = await ServiceManager.Get<UserService>().CreateUser(new TwitchUserPlatformV2Model(subscription));
        //    }
        //    await this.ProcessSub(new TwitchSubEventModel(user, subscription));
        //}

        //private async Task HandleSubscriptionMessage(JObject payload)
        //{
        //    UserSubscriptionMessageNotification subscriptionMessage = payload.ToObject<UserSubscriptionMessageNotification>();
        //}

        //private async Task HandleSubscriptionGift(JObject payload)
        //{
        //    UserSubscriptionGiftNotification subscriptionGift = payload.ToObject<UserSubscriptionGiftNotification>();
        //    UserV2ViewModel gifter = subscriptionGift.is_anonymous ? UserV2ViewModel.CreateUnassociated() : await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Twitch, platformID: subscriptionGift.user_id);
        //    if (gifter == null)
        //    {
        //        gifter = await ServiceManager.Get<UserService>().CreateUser(new TwitchUserPlatformV2Model(subscriptionGift));
        //    }
        //}

        private async Task HandleChannelPointAutomaticRewardRedemptionAdd(JObject payload)
        {
            ChannelPointAutomaticRewardRedemptionNotification redemption = payload.ToObject<ChannelPointAutomaticRewardRedemptionNotification>();

            UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Twitch, platformID: redemption.user_id);
            if (user == null)
            {
                user = await ServiceManager.Get<UserService>().CreateUser(new TwitchUserPlatformV2Model(redemption));
            }

            if (redemption.reward.Type == ChannelPointAutomaticRewardType.celebration)
            {
                CommandParametersModel parameters = new CommandParametersModel(user, StreamingPlatformTypeEnum.Twitch);
                await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TwitchChannelPowerUpCelebration, parameters);

                EventService.TwitchPowerUpOccurred(user);
            }
        }

        private async Task HandleChannelPointRewardAddCustomRedemption(JObject payload)
        {
            ChannelPointRewardCustomRedemptionNotification redemption = payload.ToObject<ChannelPointRewardCustomRedemptionNotification>();

            if (channelPointRewardRedeemsCache.Contains(redemption.id))
            {
                return;
            }
            channelPointRewardRedeemsCache.Add(redemption.id);

            UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Twitch, platformID: redemption.user_id);
            if (user == null)
            {
                user = await ServiceManager.Get<UserService>().CreateUser(new TwitchUserPlatformV2Model(redemption));
            }

            List<string> arguments = null;
            Dictionary<string, string> eventCommandSpecialIdentifiers = new Dictionary<string, string>();
            eventCommandSpecialIdentifiers["rewardname"] = redemption.reward.title;
            eventCommandSpecialIdentifiers["rewardcost"] = redemption.reward.cost.ToString();
            if (!string.IsNullOrEmpty(redemption.user_input))
            {
                TwitchChatMessageViewModel message = new TwitchChatMessageViewModel(user, redemption.user_input);
                eventCommandSpecialIdentifiers["message"] = redemption.user_input;
                eventCommandSpecialIdentifiers["messagenoemotes"] = message.TextOnlyMessageContents;
                eventCommandSpecialIdentifiers["messageemotecount"] = message.EmotesOnlyContents.Count().ToString();
                arguments = new List<string>(redemption.user_input.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
            }

            if (string.IsNullOrEmpty(await ServiceManager.Get<ModerationService>().ShouldTextBeModerated(user, redemption.user_input)))
            {
                await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TwitchChannelPointsRedeemed, new CommandParametersModel(user, StreamingPlatformTypeEnum.Twitch, arguments, eventCommandSpecialIdentifiers));

                EventService.ChannelPointsRedeemedOccurred(user, redemption.reward.cost);

                TwitchChannelPointsCommandModel command = ServiceManager.Get<CommandService>().TwitchChannelPointsCommands.FirstOrDefault(c => c.IsEnabled && string.Equals(c.ChannelPointRewardID.ToString(), redemption.reward.id, StringComparison.CurrentCultureIgnoreCase));
                if (command == null)
                {
                    command = ServiceManager.Get<CommandService>().TwitchChannelPointsCommands.FirstOrDefault(c => c.IsEnabled && string.Equals(c.Name, redemption.reward.title, StringComparison.CurrentCultureIgnoreCase));
                }

                if (command != null)
                {
                    Dictionary<string, string> channelPointSpecialIdentifiers = new Dictionary<string, string>(eventCommandSpecialIdentifiers);
                    CommandParametersModel commandParameters = new CommandParametersModel(user, platform: StreamingPlatformTypeEnum.Twitch, arguments: arguments, specialIdentifiers: channelPointSpecialIdentifiers);
                    commandParameters.TwitchRedemption = new TwitchRedemptionContext
                    {
                        RedemptionID = redemption.id,
                        RewardID = redemption.reward.id
                    };
                    await ServiceManager.Get<CommandService>().Queue(command, commandParameters);
                }
            }
            await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format(MixItUp.Base.Resources.AlertTwitchChannelPointRedeemed, user.FullDisplayName, redemption.reward.title), ChannelSession.Settings.AlertTwitchChannelPointsColor));
        }

        private async Task HandleChannelCustomPowerUpRedemptionAdd(JObject payload)
        {
            ChannelCustomPowerUpRedemptionNotification redemption = payload.ToObject<ChannelCustomPowerUpRedemptionNotification>();

            if (customPowerUpRedeemsCache.Contains(redemption.id))
            {
                return;
            }
            customPowerUpRedeemsCache.Add(redemption.id);

            UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Twitch, platformID: redemption.user_id);
            if (user == null)
            {
                user = await ServiceManager.Get<UserService>().CreateUser(new TwitchUserPlatformV2Model(redemption));
            }

            List<string> arguments = null;
            Dictionary<string, string> eventCommandSpecialIdentifiers = new Dictionary<string, string>();
            eventCommandSpecialIdentifiers["rewardname"] = redemption.custom_power_up.title;
            eventCommandSpecialIdentifiers["powerupname"] = redemption.custom_power_up.title;
            eventCommandSpecialIdentifiers["rewardcost"] = redemption.custom_power_up.bits.ToString();
            eventCommandSpecialIdentifiers["powerupcost"] = redemption.custom_power_up.bits.ToString();
            eventCommandSpecialIdentifiers["bitsamount"] = redemption.custom_power_up.bits.ToString();

            if (!string.IsNullOrEmpty(redemption.user_input))
            {
                TwitchChatMessageViewModel message = new TwitchChatMessageViewModel(user, redemption.user_input);
                eventCommandSpecialIdentifiers["message"] = redemption.user_input;
                eventCommandSpecialIdentifiers["messagenoemotes"] = message.TextOnlyMessageContents;
                eventCommandSpecialIdentifiers["messageemotecount"] = message.EmotesOnlyContents.Count().ToString();
                arguments = new List<string>(redemption.user_input.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
            }

            if (string.IsNullOrEmpty(await ServiceManager.Get<ModerationService>().ShouldTextBeModerated(user, redemption.user_input)))
            {
                await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TwitchChannelCustomPowerUpRedeemed, new CommandParametersModel(user, StreamingPlatformTypeEnum.Twitch, arguments, eventCommandSpecialIdentifiers));

                EventService.TwitchPowerUpOccurred(user);

                TwitchCustomPowerUpCommandModel command = ServiceManager.Get<CommandService>().TwitchCustomPowerUpCommands.FirstOrDefault(c => c.IsEnabled && string.Equals(c.CustomPowerUpID, redemption.custom_power_up.id, StringComparison.CurrentCultureIgnoreCase));
                if (command == null)
                {
                    command = ServiceManager.Get<CommandService>().TwitchCustomPowerUpCommands.FirstOrDefault(c => c.IsEnabled && string.Equals(c.Name, redemption.custom_power_up.title, StringComparison.CurrentCultureIgnoreCase));
                }

                if (command != null)
                {
                    Dictionary<string, string> customPowerUpSpecialIdentifiers = new Dictionary<string, string>(eventCommandSpecialIdentifiers);
                    await ServiceManager.Get<CommandService>().Queue(command, new CommandParametersModel(user, platform: StreamingPlatformTypeEnum.Twitch, arguments: arguments, specialIdentifiers: customPowerUpSpecialIdentifiers));
                }
            }
            await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format(MixItUp.Base.Resources.AlertTwitchCustomPowerUpRedeemed, user.FullDisplayName, redemption.custom_power_up.title), ChannelSession.Settings.AlertTwitchCustomPowerUpsColor));
        }

        private async Task HandleChatMessage(JObject payload)
        {
            ChatMessageNotification messageNotification = payload.ToObject<ChatMessageNotification>();

            Logger.Log(LogLevel.Debug, "Twitch Chat Message Received: " + JSONSerializerHelper.SerializeToString(messageNotification));

            UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Twitch, platformID: messageNotification.chatter_user_id);
            if (user == null)
            {
                user = await ServiceManager.Get<UserService>().CreateUser(new TwitchUserPlatformV2Model(messageNotification));
            }
            user.GetPlatformData<TwitchUserPlatformV2Model>(StreamingPlatformTypeEnum.Twitch).SetUserProperties(messageNotification);

            TwitchChatMessageViewModel message = new TwitchChatMessageViewModel(messageNotification, user);
            await ServiceManager.Get<ChatService>().AddMessage(message);

            if (message.HasBits)
            {
                int bits = messageNotification.cheer.bits;

                foreach (CurrencyModel bitsCurrency in ChannelSession.Settings.Currency.Values.Where(c => c.SpecialTracking == CurrencySpecialTrackingEnum.Bits))
                {
                    bitsCurrency.AddAmount(user, bits);
                }

                foreach (StreamPassModel streamPass in ChannelSession.Settings.StreamPass.Values)
                {
                    if (user.MeetsRole(streamPass.UserPermission))
                    {
                        streamPass.AddAmount(user, (int)Math.Ceiling(streamPass.BitsBonus * bits));
                    }
                }

                if (user.HasPlatformData(StreamingPlatformTypeEnum.Twitch))
                {
                    ((TwitchUserPlatformV2Model)user.PlatformModel).TotalBitsCheered += bits;
                }

                ChannelSession.Settings.LatestSpecialIdentifiersData[SpecialIdentifierStringBuilder.LatestBitsCheeredUserData] = user.ID;
                ChannelSession.Settings.LatestSpecialIdentifiersData[SpecialIdentifierStringBuilder.LatestBitsCheeredAmountData] = bits;

                if (string.IsNullOrEmpty(await ServiceManager.Get<ModerationService>().ShouldTextBeModerated(user, message.PlainTextMessageNoCheermotes)))
                {
                    CommandParametersModel parameters = new CommandParametersModel(user, StreamingPlatformTypeEnum.Twitch, message.ToArguments());
                    parameters.SpecialIdentifiers["bitsamount"] = bits.ToString();
                    parameters.SpecialIdentifiers["messagenocheermotes"] = message.PlainTextMessageNoCheermotes;
                    parameters.SpecialIdentifiers["message"] = message.PlainTextMessage;
                    parameters.SpecialIdentifiers["messagenoemotes"] = message.TextOnlyMessageContents;
                    parameters.SpecialIdentifiers["messageemotecount"] = message.EmotesOnlyContents.Count().ToString();
                    await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TwitchChannelBitsCheered, parameters);

                    TwitchBitsCommandModel command = ServiceManager.Get<CommandService>().TwitchBitsCommands.FirstOrDefault(c => c.IsEnabled && c.IsSingle && c.StartingAmount == bits);
                    if (command == null)
                    {
                        command = ServiceManager.Get<CommandService>().TwitchBitsCommands.Where(c => c.IsEnabled && c.IsRange).OrderBy(c => c.Range).FirstOrDefault(c => c.IsInRange(bits));
                    }

                    if (command != null)
                    {
                        await ServiceManager.Get<CommandService>().Queue(command, parameters);
                    }
                }
                await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format(MixItUp.Base.Resources.AlertTwitchBitsCheered, user.FullDisplayName, bits), ChannelSession.Settings.AlertTwitchBitsCheeredColor));
                EventService.TwitchBitsCheeredOccurred(new TwitchBitsCheeredEventModel(user, bits, message));
            }

            if (messageNotification.MessageType != ChatNotificationMessageType.text)
            {
                CommandParametersModel parameters = new CommandParametersModel(user, StreamingPlatformTypeEnum.Twitch, message.ToArguments());
                parameters.SpecialIdentifiers["messagenocheermotes"] = message.PlainTextMessageNoCheermotes;
                parameters.SpecialIdentifiers["message"] = message.PlainTextMessage;
                parameters.SpecialIdentifiers["messagenoemotes"] = message.TextOnlyMessageContents;
                parameters.SpecialIdentifiers["messageemotecount"] = message.EmotesOnlyContents.Count().ToString();

                if (messageNotification.MessageType == ChatNotificationMessageType.user_intro)
                {
                    await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TwitchChannelUserIntro, parameters);
                }
                else if (messageNotification.MessageType == ChatNotificationMessageType.power_ups_message_effect)
                {
                    await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TwitchChannelPowerUpMessageEffect, parameters);

                    EventService.TwitchPowerUpOccurred(user);
                }
                else if (messageNotification.MessageType == ChatNotificationMessageType.power_ups_gigantified_emote)
                {
                    ChatEmoteViewModelBase emote = message.EmotesOnlyContents.FirstOrDefault();
                    if (emote != null)
                    {
                        parameters.SpecialIdentifiers["emotename"] = emote.Name;
                        parameters.SpecialIdentifiers["emoteurl"] = emote.OverlayAnimatedOrStaticImageURL;

                        await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TwitchChannelPowerUpGigantifiedEmote, parameters);

                        EventService.TwitchPowerUpOccurred(user);
                    }
                }
                else if (messageNotification.MessageType == ChatNotificationMessageType.channel_points_highlighted)
                {
                    await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TwitchChannelHighlightedMessage, parameters);
                }
            }
        }

        private async Task HandleChatMessageDeleted(JObject payload)
        {
            ChatMessageDeletedNotification messageDeleted = payload.ToObject<ChatMessageDeletedNotification>();

            UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Twitch, platformID: messageDeleted.target_user_id);
            if (user == null)
            {
                user = await ServiceManager.Get<UserService>().CreateUser(new TwitchUserPlatformV2Model(messageDeleted));
            }

            await ServiceManager.Get<ChatService>().DeleteMessage(messageDeleted.message_id);
        }

        private async Task HandleWatchStreak(UserV2ViewModel user, ChatNotification notification)
        {
            TwitchChatMessageViewModel message = new TwitchChatMessageViewModel(notification, user);

            CommandParametersModel parameters = new CommandParametersModel(user, StreamingPlatformTypeEnum.Twitch);
            parameters.SpecialIdentifiers["userwatchstreak"] = notification.watch_streak.streak_count.GetValueOrDefault().ToString();
            parameters.SpecialIdentifiers["watchstreakchannelpointsawarded"] = notification.watch_streak.channel_points_awarded.GetValueOrDefault().ToString();
            parameters.SpecialIdentifiers["message"] = notification.message?.text;
            parameters.SpecialIdentifiers["messagenoemotes"] = message.TextOnlyMessageContents;
            parameters.SpecialIdentifiers["messageemotecount"] = message.EmotesOnlyContents.Count().ToString();
            await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TwitchChannelWatchStreak, parameters);

            await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format(MixItUp.Base.Resources.AlertTwitchWatchStreak, user.FullDisplayName, notification.watch_streak.streak_count.GetValueOrDefault()), ChannelSession.Settings.AlertTwitchWatchStreakColor));
        }

        private async Task HandleModiversary(UserV2ViewModel user, ChatNotification notification)
        {
            TwitchChatMessageViewModel message = new TwitchChatMessageViewModel(notification, user);

            CommandParametersModel parameters = new CommandParametersModel(user, StreamingPlatformTypeEnum.Twitch);
            parameters.SpecialIdentifiers["usermodiversarymonths"] = notification.modiversary.months.GetValueOrDefault().ToString();
            parameters.SpecialIdentifiers["message"] = notification.message?.text;
            parameters.SpecialIdentifiers["messagenoemotes"] = message.TextOnlyMessageContents;
            parameters.SpecialIdentifiers["messageemotecount"] = message.EmotesOnlyContents.Count().ToString();
            await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TwitchChannelModiversary, parameters);

            await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format(MixItUp.Base.Resources.AlertTwitchModiversary, user.FullDisplayName, notification.modiversary.months.GetValueOrDefault()), ChannelSession.Settings.AlertTwitchModiversaryColor));
        }

        private async Task HandleChatNotification(JObject payload)
        {
            ChatNotification notification = payload.ToObject<ChatNotification>();

            Logger.Log(LogLevel.Debug, "Twitch Chat Message Received: " + JSONSerializerHelper.SerializeToString(notification));

            UserV2ViewModel user;
            if (notification.chatter_is_anonymous)
            {
                user = UserV2ViewModel.CreateUnassociated(Resources.AnAnonymousGifter);
            }
            else
            {
                user = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Twitch, platformID: notification.chatter_user_id);
                if (user == null)
                {
                    user = await ServiceManager.Get<UserService>().CreateUser(new TwitchUserPlatformV2Model(notification));
                }
                user.GetPlatformData<TwitchUserPlatformV2Model>(StreamingPlatformTypeEnum.Twitch).SetUserProperties(notification);
            }

            if (notification.NoticeType == ChatNotificationType.announcement)
            {
                TwitchChatMessageViewModel message = new TwitchChatMessageViewModel(notification, user);
                await ServiceManager.Get<ChatService>().AddMessage(message);
            }
            else if (notification.NoticeType == ChatNotificationType.watch_streak)
            {
                await this.HandleWatchStreak(user, notification);
            }
            else if (notification.NoticeType == ChatNotificationType.modiversary)// && MixItUpService.Options.TwitchModiversaryEnabled)
            {
                await this.HandleModiversary(user, notification);
            }

            // Subs
            else if (notification.NoticeType == ChatNotificationType.sub)
            {
                await this.ProcessSub(new TwitchSubcriptionEventModel(user, notification));
            }
            else if (notification.NoticeType == ChatNotificationType.resub)
            {
                if (notification.resub.is_gift.GetValueOrDefault())
                {
                    UserV2ViewModel gifter;
                    if (notification.resub.gifter_is_anonymous.GetValueOrDefault())
                    {
                        gifter = UserV2ViewModel.CreateUnassociated(Resources.AnAnonymousGifter);
                    }
                    else
                    {
                        gifter = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Twitch, platformID: notification.resub.gifter_user_id);
                        if (gifter == null)
                        {
                            gifter = await ServiceManager.Get<UserService>().CreateUser(new TwitchUserPlatformV2Model(notification.resub));
                        }
                    }

                    await this.ProcessSub(new TwitchSubcriptionEventModel(user, notification, gifter));
                }
                else
                {
                    await this.ProcessSub(new TwitchSubcriptionEventModel(user, notification));
                }
            }
            else if (notification.NoticeType == ChatNotificationType.sub_gift)
            {
                UserV2ViewModel gifter = user;

                user = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Twitch, platformID: notification.sub_gift.recipient_user_id);
                if (user == null)
                {
                    user = await ServiceManager.Get<UserService>().CreateUser(new TwitchUserPlatformV2Model(notification.sub_gift));
                }

                if (ChannelSession.Settings.MassGiftedSubsFilterAmount > 0)
                {
                    ServiceManager.Get<TwitchSession>().AddGiftedSub(new TwitchSubcriptionEventModel(user, notification, gifter));
                }
                else
                {
                    await this.ProcessSub(new TwitchSubcriptionEventModel(user, notification, gifter));
                }
            }
            else if (notification.NoticeType == ChatNotificationType.gift_paid_upgrade)
            {
                await this.ProcessSub(new TwitchSubcriptionEventModel(user, notification));
            }
            else if (notification.NoticeType == ChatNotificationType.prime_paid_upgrade)
            {
                await this.ProcessSub(new TwitchSubcriptionEventModel(user, notification));
            }
            else if (notification.NoticeType == ChatNotificationType.community_sub_gift)
            {
                await ServiceManager.Get<TwitchSession>().AddMassGiftedSub(new TwitchMassGiftedSubcriptionsEventModel(notification.community_sub_gift, user, notification.chatter_is_anonymous));
            }

            // Shared
            else if (notification.NoticeType == ChatNotificationType.shared_chat_announcement)
            {

            }


            else if (notification.NoticeType == ChatNotificationType.shared_chat_raid)
            {

            }
            else if (notification.NoticeType == ChatNotificationType.shared_chat_sub)
            {

            }
            else if (notification.NoticeType == ChatNotificationType.shared_chat_resub)
            {

            }
            else if (notification.NoticeType == ChatNotificationType.shared_chat_sub_gift)
            {

            }
            else if (notification.NoticeType == ChatNotificationType.shared_chat_gift_paid_upgrade)
            {

            }
            else if (notification.NoticeType == ChatNotificationType.shared_chat_prime_paid_upgrade)
            {

            }
            else if (notification.NoticeType == ChatNotificationType.shared_chat_community_sub_gift)
            {

            }
        }

        private Task HandleChatMessageHeld(JObject payload)
        {
            Logger.Log(LogLevel.Debug, "Packet Received: " + JSONSerializerHelper.SerializeToString(payload));
            return Task.CompletedTask;
        }

        private Task HandleChatMessageHoldUpdate(JObject payload)
        {
            Logger.Log(LogLevel.Debug, "Packet Received: " + JSONSerializerHelper.SerializeToString(payload));
            return Task.CompletedTask;
        }

        private async Task HandleChatClear(JObject payload)
        {
            await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(StreamingPlatformTypeEnum.Twitch, MixItUp.Base.Resources.ChatCleared, ChannelSession.Settings.AlertModerationColor));
            ChatService.ChatCleared();
        }

        private async Task HandleChatUserClear(JObject payload)
        {
            ChatUserClearNotification userClear = payload.ToObject<ChatUserClearNotification>();

            UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Twitch, platformID: userClear.target_user_id);
            if (user == null)
            {
                user = await ServiceManager.Get<UserService>().CreateUser(new TwitchUserPlatformV2Model(userClear));
            }

            if (user != null)
            {
                await ServiceManager.Get<ChatService>().MarkUserMessagesAsDeleted(user, reason: Resources.UserChatCleared);
            }
        }

        private Task HandleSharedChatBegin(JObject payload) => HandleSharedChatEvent(payload, EventTypeEnum.TwitchChannelSharedChatStarted);

        private Task HandleSharedChatUpdate(JObject payload)
        {
            Logger.Log(LogLevel.Debug, "Packet Received: " + JSONSerializerHelper.SerializeToString(payload));
            return Task.CompletedTask;
        }

        private Task HandleSharedChatEnd(JObject payload) => HandleSharedChatEvent(payload, EventTypeEnum.TwitchChannelSharedChatEnded);

        private Task HandleSharedChatEvent(JObject payload, EventTypeEnum eventType)
        {
            if (payload["broadcaster_user_id"]?.ToString() != ServiceManager.Get<TwitchSession>().StreamerID) { return Task.CompletedTask; }
            var identifiers = new Dictionary<string, string>
            {
                ["sharedchatsessionid"] = payload["session_id"]?.ToString() ?? string.Empty,
                ["sharedchathostid"] = payload["host_broadcaster_user_id"]?.ToString() ?? string.Empty,
                ["sharedchathost"] = payload["host_broadcaster_user_login"]?.ToString() ?? string.Empty,
            };
            return ServiceManager.Get<EventService>().PerformEvent(eventType, new CommandParametersModel(ChannelSession.User, StreamingPlatformTypeEnum.Twitch, identifiers));
        }

        private async Task HandleModeration(JObject payload)
        {
            ModerationNotification moderation = payload.ToObject<ModerationNotification>();

            UserV2ViewModel moderator = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Twitch, platformID: moderation.moderator_user_id);
            if (moderator == null)
            {
                moderator = await ServiceManager.Get<UserService>().CreateUser(new TwitchUserPlatformV2Model(moderation));
            }

            if (moderation.ActionType == ModerationNotificationActionType.ban)
            {
                UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Twitch, platformID: moderation.ban.user_id);
                if (user == null)
                {
                    user = await ServiceManager.Get<UserService>().CreateUser(new TwitchUserPlatformV2Model(moderation.ban));
                }

                CommandParametersModel parameters = new CommandParametersModel(StreamingPlatformTypeEnum.Twitch);
                parameters.Arguments.Add("@" + user.Username);
                parameters.TargetUser = user;
                parameters.SpecialIdentifiers["banreason"] = moderation.ban.reason ?? string.Empty;
                await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.ChatUserBan, parameters);

                await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format(MixItUp.Base.Resources.AlertBanned, user.FullDisplayName), ChannelSession.Settings.AlertModerationColor));

                await ServiceManager.Get<UserService>().RemoveActiveUser(user);

                ChatService.ChatUserBanned(user);
            }
            else if (moderation.ActionType == ModerationNotificationActionType.delete)
            {
                UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Twitch, platformID: moderation.delete.user_id);
                if (user == null)
                {
                    user = await ServiceManager.Get<UserService>().CreateUser(new TwitchUserPlatformV2Model(moderation.delete));
                }
                await ServiceManager.Get<ChatService>().DeleteMessage(moderation.delete.message_id);
            }
            else if (moderation.ActionType == ModerationNotificationActionType.timeout)
            {
                UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Twitch, platformID: moderation.timeout.user_id);
                if (user == null)
                {
                    user = await ServiceManager.Get<UserService>().CreateUser(new TwitchUserPlatformV2Model(moderation.timeout));
                }

                int timeoutLength = moderation.timeout.TotalSeconds;

                CommandParametersModel parameters = new CommandParametersModel(StreamingPlatformTypeEnum.Twitch);
                parameters.Arguments.Add("@" + user.Username);
                parameters.TargetUser = user;
                parameters.SpecialIdentifiers["timeoutlength"] = timeoutLength.ToString();
                parameters.SpecialIdentifiers["timeoutreason"] = moderation.timeout.reason;
                await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.ChatUserTimeout, parameters);

                await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format(MixItUp.Base.Resources.AlertTimedOut, user.FullDisplayName, timeoutLength), ChannelSession.Settings.AlertModerationColor));

                ChatService.ChatUserTimedOut(user);
            }
            else if (moderation.ActionType == ModerationNotificationActionType.warn)
            {
                UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Twitch, platformID: moderation.warn.user_id);
                if (user == null)
                {
                    user = await ServiceManager.Get<UserService>().CreateUser(new TwitchUserPlatformV2Model(moderation.warn));
                }

                CommandParametersModel parameters = new CommandParametersModel(moderator, StreamingPlatformTypeEnum.Twitch);
                parameters.Arguments.Add("@" + user.Username);
                parameters.TargetUser = user;
                parameters.SpecialIdentifiers["warnreason"] = moderation.warn.reason ?? string.Empty;
                await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TwitchChannelUserWarned, parameters);

                await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format(MixItUp.Base.Resources.AlertTwitchUserWarned, user.FullDisplayName), ChannelSession.Settings.AlertTwitchUserWarnedColor));
            }
            else if (moderation.ActionType == ModerationNotificationActionType.mod)
            {

            }
            else if (moderation.ActionType == ModerationNotificationActionType.unmod)
            {

            }
            else if (moderation.ActionType == ModerationNotificationActionType.vip)
            {

            }
            else if (moderation.ActionType == ModerationNotificationActionType.unvip)
            {

            }
            else if (moderation.ActionType == ModerationNotificationActionType.shared_chat_ban)
            {

            }
            else if (moderation.ActionType == ModerationNotificationActionType.shared_chat_delete)
            {

            }
            else if (moderation.ActionType == ModerationNotificationActionType.shared_chat_timeout)
            {

            }
        }

        private async Task HandleUnbanRequestCreate(JObject payload)
        {
            UnbanRequestNotification unbanRequest = payload.ToObject<UnbanRequestNotification>();

            UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Twitch, platformID: unbanRequest.user_id);
            if (user == null)
            {
                user = await ServiceManager.Get<UserService>().CreateUser(new TwitchUserPlatformV2Model(unbanRequest.user_id, unbanRequest.user_login, unbanRequest.user_name));
            }

            CommandParametersModel parameters = new CommandParametersModel(user, StreamingPlatformTypeEnum.Twitch);
            parameters.SpecialIdentifiers["message"] = unbanRequest.text ?? string.Empty;
            await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TwitchChannelUnbanRequestCreated, parameters);

            await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format(MixItUp.Base.Resources.AlertTwitchUnbanRequestCreated, user.FullDisplayName), ChannelSession.Settings.AlertModerationColor));
        }

        private async Task HandleUnbanRequestResolve(JObject payload)
        {
            UnbanRequestNotification unbanRequest = payload.ToObject<UnbanRequestNotification>();

            UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Twitch, platformID: unbanRequest.user_id);
            if (user == null)
            {
                user = await ServiceManager.Get<UserService>().CreateUser(new TwitchUserPlatformV2Model(unbanRequest.user_id, unbanRequest.user_login, unbanRequest.user_name));
            }

            CommandParametersModel parameters = new CommandParametersModel(user, StreamingPlatformTypeEnum.Twitch);
            parameters.SpecialIdentifiers["unbanrequeststatus"] = unbanRequest.status ?? string.Empty;
            parameters.SpecialIdentifiers["message"] = unbanRequest.resolution_text ?? string.Empty;
            await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TwitchChannelUnbanRequestResolved, parameters);

            await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format(MixItUp.Base.Resources.AlertTwitchUnbanRequestResolved, user.FullDisplayName, unbanRequest.status), ChannelSession.Settings.AlertModerationColor));
        }

        private async Task HandleShoutoutReceived(JObject payload)
        {
            ShoutoutReceiveNotification shoutout = payload.ToObject<ShoutoutReceiveNotification>();

            UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Twitch, platformID: shoutout.from_broadcaster_user_id);
            if (user == null)
            {
                user = await ServiceManager.Get<UserService>().CreateUser(new TwitchUserPlatformV2Model(shoutout));
            }

            CommandParametersModel parameters = new CommandParametersModel(user, StreamingPlatformTypeEnum.Twitch);
            parameters.SpecialIdentifiers["shoutoutviewercount"] = shoutout.viewer_count.ToString();
            await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TwitchChannelShoutoutReceived, parameters);

            await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format(MixItUp.Base.Resources.AlertTwitchShoutoutReceived, user.FullDisplayName, shoutout.viewer_count), ChannelSession.Settings.AlertTwitchShoutoutReceivedColor));
        }

        private async Task HandleSuspiciousUserMessage(JObject payload)
        {
            SuspiciousUserMessageNotification suspicious = payload.ToObject<SuspiciousUserMessageNotification>();

            UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Twitch, platformID: suspicious.user_id);
            if (user == null)
            {
                user = await ServiceManager.Get<UserService>().CreateUser(new TwitchUserPlatformV2Model(suspicious));
            }

            CommandParametersModel parameters = new CommandParametersModel(user, StreamingPlatformTypeEnum.Twitch);
            parameters.SpecialIdentifiers["message"] = suspicious.message?.text ?? string.Empty;
            parameters.SpecialIdentifiers["suspicioususertypes"] = string.Join(", ", suspicious.types ?? new List<string>());
            parameters.SpecialIdentifiers["suspicioususerstatus"] = suspicious.low_trust_status ?? string.Empty;
            parameters.SpecialIdentifiers["suspicioususerbanevasion"] = suspicious.ban_evasion_evaluation ?? string.Empty;
            await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TwitchChannelSuspiciousUserMessage, parameters);

            await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format(MixItUp.Base.Resources.AlertTwitchSuspiciousUserMessage, user.FullDisplayName), ChannelSession.Settings.AlertTwitchSuspiciousUserMessageColor));
        }

        private async Task HandleSuspiciousUserUpdate(JObject payload)
        {
            SuspiciousUserUpdateNotification suspicious = payload.ToObject<SuspiciousUserUpdateNotification>();

            UserV2ViewModel moderator = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Twitch, platformID: suspicious.moderator_user_id);
            if (moderator == null)
            {
                moderator = await ServiceManager.Get<UserService>().CreateUser(new TwitchUserPlatformV2Model(suspicious));
            }

            UserV2ViewModel targetUser = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Twitch, platformID: suspicious.user_id);
            if (targetUser == null)
            {
                targetUser = await ServiceManager.Get<UserService>().CreateUser(new TwitchUserPlatformV2Model(suspicious.user_id, suspicious.user_login, suspicious.user_name));
            }

            CommandParametersModel parameters = new CommandParametersModel(moderator, StreamingPlatformTypeEnum.Twitch);
            parameters.Arguments.Add("@" + targetUser.Username);
            parameters.TargetUser = targetUser;
            parameters.SpecialIdentifiers["suspicioususerstatus"] = suspicious.low_trust_status ?? string.Empty;
            await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TwitchChannelSuspiciousUserUpdated, parameters);

            await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(targetUser, string.Format(MixItUp.Base.Resources.AlertTwitchSuspiciousUserUpdated, targetUser.FullDisplayName, suspicious.low_trust_status), ChannelSession.Settings.AlertTwitchSuspiciousUserUpdatedColor));
        }

        private async Task HandleShieldModeBegin(JObject payload)
        {
            ShieldModeNotification shieldMode = payload.ToObject<ShieldModeNotification>();

            UserV2ViewModel moderator = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Twitch, platformID: shieldMode.moderator_user_id);
            if (moderator == null)
            {
                moderator = await ServiceManager.Get<UserService>().CreateUser(new TwitchUserPlatformV2Model(shieldMode));
            }

            CommandParametersModel parameters = new CommandParametersModel(moderator, StreamingPlatformTypeEnum.Twitch);
            await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TwitchChannelShieldModeStarted, parameters);

            await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(moderator, string.Format(MixItUp.Base.Resources.AlertTwitchShieldModeStarted, moderator.FullDisplayName), ChannelSession.Settings.AlertTwitchShieldModeStartedColor));
        }

        private async Task HandleShieldModeEnd(JObject payload)
        {
            ShieldModeNotification shieldMode = payload.ToObject<ShieldModeNotification>();

            UserV2ViewModel moderator = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Twitch, platformID: shieldMode.moderator_user_id);
            if (moderator == null)
            {
                moderator = await ServiceManager.Get<UserService>().CreateUser(new TwitchUserPlatformV2Model(shieldMode));
            }

            CommandParametersModel parameters = new CommandParametersModel(moderator, StreamingPlatformTypeEnum.Twitch);
            await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TwitchChannelShieldModeEnded, parameters);

            await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(moderator, string.Format(MixItUp.Base.Resources.AlertTwitchShieldModeEnded, moderator.FullDisplayName), ChannelSession.Settings.AlertTwitchShieldModeEndedColor));
        }

        private async Task HandleGoalBegin(JObject payload)
        {
            GoalNotification goal = payload.ToObject<GoalNotification>();

            CommandParametersModel parameters = new CommandParametersModel(ChannelSession.User, StreamingPlatformTypeEnum.Twitch);
            parameters.SpecialIdentifiers["goaltype"] = goal.type ?? string.Empty;
            parameters.SpecialIdentifiers["goaldescription"] = goal.description ?? string.Empty;
            parameters.SpecialIdentifiers["goalcurrentamount"] = goal.current_amount.ToString();
            parameters.SpecialIdentifiers["goaltargetamount"] = goal.target_amount.ToString();
            await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TwitchChannelGoalStarted, parameters);

            await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(StreamingPlatformTypeEnum.Twitch, string.Format(MixItUp.Base.Resources.AlertTwitchGoalStarted, goal.type, goal.current_amount, goal.target_amount), ChannelSession.Settings.AlertTwitchGoalStartedColor));
        }

        private async Task HandleGoalProgress(JObject payload)
        {
            GoalNotification goal = payload.ToObject<GoalNotification>();

            CommandParametersModel parameters = new CommandParametersModel(ChannelSession.User, StreamingPlatformTypeEnum.Twitch);
            parameters.SpecialIdentifiers["goaltype"] = goal.type ?? string.Empty;
            parameters.SpecialIdentifiers["goaldescription"] = goal.description ?? string.Empty;
            parameters.SpecialIdentifiers["goalcurrentamount"] = goal.current_amount.ToString();
            parameters.SpecialIdentifiers["goaltargetamount"] = goal.target_amount.ToString();
            await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TwitchChannelGoalProgress, parameters);
        }

        private async Task HandleGoalEnd(JObject payload)
        {
            GoalNotification goal = payload.ToObject<GoalNotification>();

            CommandParametersModel parameters = new CommandParametersModel(ChannelSession.User, StreamingPlatformTypeEnum.Twitch);
            parameters.SpecialIdentifiers["goaltype"] = goal.type ?? string.Empty;
            parameters.SpecialIdentifiers["goaldescription"] = goal.description ?? string.Empty;
            parameters.SpecialIdentifiers["goalcurrentamount"] = goal.current_amount.ToString();
            parameters.SpecialIdentifiers["goaltargetamount"] = goal.target_amount.ToString();
            parameters.SpecialIdentifiers["goalachieved"] = goal.is_achieved.ToString();
            await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TwitchChannelGoalEnded, parameters);

            await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(StreamingPlatformTypeEnum.Twitch, string.Format(MixItUp.Base.Resources.AlertTwitchGoalEnded, goal.type, goal.current_amount, goal.target_amount), ChannelSession.Settings.AlertTwitchGoalEndedColor));
        }

        private async Task ProcessSub(TwitchSubcriptionEventModel subscription)
        {
            if (subscription.Duration > 0 && subscription.NoticeType != ChatNotificationType.sub && subscription.NoticeType != ChatNotificationType.shared_chat_sub)
            {
                subscription.User.Roles.Add(UserRoleEnum.Subscriber);
                subscription.User.SubscribeDate = DateTimeOffset.Now.SubtractMonths(subscription.Cumulative - 1);
                subscription.User.SubscriberTier = subscription.Tier;

                CommandParametersModel parameters = new CommandParametersModel(subscription.User, subscription.Message.ToArguments());
                parameters.SpecialIdentifiers["message"] = subscription.Message.PlainTextMessage;
                parameters.SpecialIdentifiers["messagenoemotes"] = subscription.Message.TextOnlyMessageContents;
                parameters.SpecialIdentifiers["messageemotecount"] = subscription.Message.EmotesOnlyContents.Count().ToString();
                parameters.SpecialIdentifiers["usersubmonths"] = subscription.Cumulative.ToString();
                parameters.SpecialIdentifiers["usersubplanname"] = subscription.TierName;
                parameters.SpecialIdentifiers["usersubplan"] = subscription.TierName;
                parameters.SpecialIdentifiers["usersubpoints"] = subscription.SubPoints.ToString();
                parameters.SpecialIdentifiers["usersubstreak"] = subscription.Streak.ToString();
                parameters.SpecialIdentifiers["usersubdurationmonths"] = Math.Max(subscription.Duration, 1).ToString();

                string moderation = await ServiceManager.Get<ModerationService>().ShouldTextBeModerated(subscription.User, subscription.Message.PlainTextMessage);
                if (!string.IsNullOrEmpty(moderation))
                {
                    parameters.SpecialIdentifiers["message"] = moderation;
                }

                if (await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TwitchChannelResubscribed, parameters))
                {
                    ChannelSession.Settings.LatestSpecialIdentifiersData[SpecialIdentifierStringBuilder.LatestSubscriberUserData] = subscription.User.ID;
                    ChannelSession.Settings.LatestSpecialIdentifiersData[SpecialIdentifierStringBuilder.LatestSubscriberSubMonthsData] = subscription.Cumulative;

                    if (subscription.Cumulative >= subscription.User.TotalMonthsSubbed)
                    {
                        subscription.User.TotalMonthsSubbed = subscription.Cumulative;
                    }
                    else
                    {
                        subscription.User.TotalMonthsSubbed++;
                    }

                    foreach (CurrencyModel currency in ChannelSession.Settings.Currency.Values)
                    {
                        currency.AddAmount(subscription.User, currency.OnSubscribeBonus);
                    }

                    foreach (StreamPassModel streamPass in ChannelSession.Settings.StreamPass.Values)
                    {
                        if (parameters.User.MeetsRole(streamPass.UserPermission))
                        {
                            streamPass.AddAmount(subscription.User, streamPass.SubscribeBonus);
                        }
                    }
                }

                EventService.ResubscribeOccurred(new SubscriptionDetailsModel(StreamingPlatformTypeEnum.Twitch, subscription.User, months: subscription.Cumulative, tier: subscription.Tier));
                await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(subscription.User, string.Format(MixItUp.Base.Resources.AlertResubscribedTier, subscription.User.FullDisplayName, subscription.Cumulative, subscription.TierName), ChannelSession.Settings.AlertSubColor));
            }
            else
            {
                CommandParametersModel parameters = new CommandParametersModel(subscription.User, StreamingPlatformTypeEnum.Twitch);

                if (subscription.IsPrimeUpgrade || subscription.IsGiftedUpgrade)
                {
                    var subData = await ServiceManager.Get<TwitchSession>().StreamerService.GetBroadcasterSubscription(ServiceManager.Get<TwitchSession>().StreamerModel, subscription.User.PlatformID);
                    if (subData != null)
                    {
                        subscription.SetSubData(subData);
                    }
                }

                subscription.User.Roles.Add(UserRoleEnum.Subscriber);
                subscription.User.SubscribeDate = DateTimeOffset.Now;
                subscription.User.SubscriberTier = subscription.Tier;

                parameters.SpecialIdentifiers["message"] = subscription.Message.PlainTextMessage;
                parameters.SpecialIdentifiers["messagenoemotes"] = subscription.Message.TextOnlyMessageContents;
                parameters.SpecialIdentifiers["messageemotecount"] = subscription.Message.EmotesOnlyContents.Count().ToString();
                parameters.SpecialIdentifiers["usersubplanname"] = subscription.PlanName;
                parameters.SpecialIdentifiers["usersubplan"] = subscription.TierName;
                parameters.SpecialIdentifiers["usersubpoints"] = subscription.SubPoints.ToString();
                parameters.SpecialIdentifiers["usersubdurationmonths"] = Math.Max(subscription.Duration, 1).ToString();
                parameters.SpecialIdentifiers["isprimeupgrade"] = subscription.IsPrimeUpgrade.ToString();
                parameters.SpecialIdentifiers["isgiftupgrade"] = subscription.IsGiftedUpgrade.ToString();

                if (await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TwitchChannelSubscribed, parameters))
                {
                    ChannelSession.Settings.LatestSpecialIdentifiersData[SpecialIdentifierStringBuilder.LatestSubscriberUserData] = subscription.User.ID;
                    ChannelSession.Settings.LatestSpecialIdentifiersData[SpecialIdentifierStringBuilder.LatestSubscriberSubMonthsData] = 1;

                    subscription.User.TotalMonthsSubbed++;

                    foreach (CurrencyModel currency in ChannelSession.Settings.Currency.Values)
                    {
                        currency.AddAmount(subscription.User, currency.OnSubscribeBonus);
                    }

                    foreach (StreamPassModel streamPass in ChannelSession.Settings.StreamPass.Values)
                    {
                        if (parameters.User.MeetsRole(streamPass.UserPermission))
                        {
                            streamPass.AddAmount(subscription.User, streamPass.SubscribeBonus);
                        }
                    }
                }

                EventService.SubscribeOccurred(new SubscriptionDetailsModel(StreamingPlatformTypeEnum.Twitch, subscription.User, tier: subscription.Tier));

                if (subscription.IsPrimeUpgrade)
                {
                    await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(subscription.User, string.Format(MixItUp.Base.Resources.AlertContinuedPrimeSubscriptionTier, subscription.User.FullDisplayName, subscription.PlanName), ChannelSession.Settings.AlertSubColor));
                }
                else if (subscription.IsGiftedUpgrade)
                {
                    await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(subscription.User, string.Format(MixItUp.Base.Resources.AlertContinuedGiftedSubscriptionTier, subscription.User.FullDisplayName, subscription.PlanName), ChannelSession.Settings.AlertSubColor));
                }
                else
                {
                    await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(subscription.User, string.Format(MixItUp.Base.Resources.AlertSubscribedTier, subscription.User.FullDisplayName, subscription.PlanName), ChannelSession.Settings.AlertSubColor));
                }
            }
            if (subscription.Gifter != null && !subscription.IsAnonymous)
            {
                if (subscription.CumulativeGifts.HasValue)
                {
                    subscription.Gifter.TotalSubsGifted = (uint)subscription.CumulativeGifts.Value;
                }
                else
                {
                    subscription.Gifter.TotalSubsGifted++;
                }
            }
        }

        private async void UserWebSocket_PacketReceived(object sender, string packet)
        {
            try
            {
                Logger.Log(LogLevel.Debug, "Twitch EventSub Packet Received: " + packet);

                if (!string.IsNullOrEmpty(packet))
                {
                    JObject jsonData = JObject.Parse(packet);
                    string messageTypeString = jsonData["metadata"]?["message_type"]?.Value<string>();
                    if (string.Equals(messageTypeString, "session_welcome", StringComparison.OrdinalIgnoreCase))
                    {
                        await ProcessSessionWelcome(jsonData.ToObject<WelcomeMessage>());
                    }
                    else if (string.Equals(messageTypeString, "session_keepalive", StringComparison.OrdinalIgnoreCase))
                    {
                        // TODO: If not received in 10 seconds, reconnect
                    }
                    else if (string.Equals(messageTypeString, "session_reconnect", StringComparison.OrdinalIgnoreCase))
                    {
                        // NOTE: This SHOULD auto-disconnect

                        // The URL of the reconnection message bight have encoded characters in it (EX: "cell-c.eventsub.wss.twitch.tv/ws?challenge=XXX\u0026id=XXX" where "\u0026" is "&").
                        // There have also been issues noted with being able to re-connect properly to the URL specified.
                    }
                    else if (string.Equals(messageTypeString, "revocation", StringComparison.OrdinalIgnoreCase))
                    {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        Task.Run(this.Reconnect);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    }
                    else if (string.Equals(messageTypeString, "notification", StringComparison.OrdinalIgnoreCase))
                    {
                        await ProcessNotification(jsonData.ToObject<NotificationMessage>());
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        private void WebSocket_PacketSent(object sender, string packet)
        {
            Logger.Log(LogLevel.Debug, string.Format("Twitch EventSub Packet Sent: {0}", packet));
        }

        private void WebSocket_Disconnected(object sender, WebSocketCloseStatus closeStatus)
        {
            ChannelSession.DisconnectionOccurred(Resources.TwitchClient);
            this.eventSubSubscriptionsConnected = false;

            if (!isReconnecting)
            {
                isReconnecting = true;
                Task.Run(async () =>
                {
                    int delayInMs = 5000;
                    try
                    {
                        while (true)
                        {
                            try
                            {
                                var newWebSocket = new AdvancedClientWebSocket();
                                newWebSocket.PacketSent += WebSocket_PacketSent;
                                newWebSocket.PacketReceived += UserWebSocket_PacketReceived;
                                newWebSocket.Disconnected += WebSocket_Disconnected;
                                this.webSocket = newWebSocket;

                                await this.Disconnect();
                                var result = await this.Connect();
                                if (result.Success && this.IsConnected)
                                {
                                    ChannelSession.ReconnectionOccurred(Resources.TwitchClient);
                                    break;
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.Log(ex);
                            }
                            await Task.Delay(delayInMs);
                            delayInMs = Math.Min(delayInMs * 2, 60000);
                        }
                    }
                    finally
                    {
                        isReconnecting = false;
                    }
                });
            }
        }
    }
}
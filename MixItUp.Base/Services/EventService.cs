using MixItUp.Base.Model;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.Currency;
using MixItUp.Base.Model.Kick.Kicks;
using MixItUp.Base.Model.Twitch.Bits;
using MixItUp.Base.Model.Velora;
using MixItUp.Base.Model.VPZone;
using MixItUp.Base.Model.User;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Chat;
using MixItUp.Base.ViewModel.Chat.YouTube;
using MixItUp.Base.ViewModel.User;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MixItUp.Base.Services
{
    public enum EventTypeEnum
    {
        None = 0,

        // Platform-agnostic = 1

        ChannelStreamStart = 1,
        ChannelStreamStop = 2,
        ChannelHosted = 3,
        ChannelRaided = 4,

        ChannelFollowed = 10,
        ChannelUnfollowed = 11,

        ChannelSubscribed = 20,
        ChannelResubscribed = 21,
        ChannelSubscriptionGifted = 22,
        ChannelMassSubscriptionsGifted = 23,

        ChatUserFirstJoin = 50,
        ChatUserPurge = 51,
        ChatUserBan = 52,
        ChatMessageReceived = 53,
        ChatUserJoined = 54,
        ChatUserLeft = 55,
        ChatMessageDeleted = 56,
        ChatUserTimeout = 57,
        ChatWhisperReceived = 58,
        ChatUserEntranceCommand = 59,
        ChatUserFirstMessage = 60,

        // Application = 100

        ApplicationLaunch = 100,
        ApplicationExit = 101,

        // Twitch = 200

        TwitchChannelStreamStart = 200,
        TwitchChannelStreamStop = 201,
        [Obsolete]
        TwitchChannelHosted = 202,
        TwitchChannelRaided = 203,
        TwitchChannelOutgoingRaidCompleted = 204,
        TwitchChannelUpdated = 205,

        TwitchChannelFollowed = 210,
        [Obsolete]
        TwitchChannelUnfollowed = 211,

        TwitchChannelSubscribed = 220,
        TwitchChannelResubscribed = 221,
        TwitchChannelSubscriptionGifted = 222,
        TwitchChannelMassSubscriptionsGifted = 223,

        TwitchChannelWatchStreak = 230,
        TwitchChannelShoutoutReceived = 231,
        TwitchChannelModiversary = 232,

        TwitchChannelHighlightedMessage = 240,
        TwitchChannelUserIntro = 241,
        TwitchChannelPowerUpMessageEffect = 242,
        TwitchChannelPowerUpGigantifiedEmote = 243,
        TwitchChannelPowerUpCelebration = 244,

        TwitchChannelGoalStarted = 245,
        TwitchChannelGoalEnded = 246,
        TwitchChannelGoalProgress = 247,

        TwitchChannelAdUpcoming = 250,
        TwitchChannelAdStarted = 251,
        TwitchChannelAdEnded = 252,

        TwitchChannelBitsCheered = 270,
        TwitchChannelPointsRedeemed = 271,
        TwitchChannelCharityDonation = 272,
        [Obsolete]
        TwitchChannelHypeChat = 273,
        TwitchChannelCustomPowerUpRedeemed = 274,

        TwitchChannelHypeTrainBegin = 280,
        TwitchChannelHypeTrainProgress = 281,
        TwitchChannelHypeTrainEnd = 282,
        TwitchChannelHypeTrainLevelUp = 283,
        TwitchChannelSharedChatStarted = 284,
        TwitchChannelSharedChatEnded = 285,

        TwitchChannelUserWarned = 290,
        TwitchChannelSuspiciousUserMessage = 291,
        TwitchChannelSuspiciousUserUpdated = 292,
        TwitchChannelShieldModeStarted = 293,
        TwitchChannelShieldModeEnded = 294,
        TwitchChannelUnbanRequestCreated = 295,
        TwitchChannelUnbanRequestResolved = 296,

        // 300 = YouTube

        YouTubeChannelStreamStart = 300,
        YouTubeChannelStreamStop = 301,

        YouTubeChannelNewMember = 320,
        YouTubeChannelMemberMilestone = 321,
        YouTubeChannelMembershipGifted = 322,
        YouTubeChannelMassMembershipGifted = 323,

        YouTubeChannelSuperChat = 370,
        YouTubeChannelJewelsGift = 371,

        // 400 = Trovo

        [Obsolete]
        TrovoChannelStreamStart = 400,
        [Obsolete]
        TrovoChannelStreamStop = 401,
        [Obsolete]
        TrovoChannelRaided = 403,

        [Obsolete]
        TrovoChannelFollowed = 410,

        [Obsolete]
        TrovoChannelSubscribed = 420,
        [Obsolete]
        TrovoChannelResubscribed = 421,
        [Obsolete]
        TrovoChannelSubscriptionGifted = 422,
        [Obsolete]
        TrovoChannelMassSubscriptionsGifted = 423,

        [Obsolete]
        TrovoChannelSpellCast = 470,
        [Obsolete]
        TrovoChannelMagicChat = 471,

        // 500 = Glimesh
        [Obsolete]
        GlimeshChannelStreamStart = 500,
        [Obsolete]
        GlimeshChannelStreamStop = 501,
        [Obsolete]
        GlimeshChannelFollowed = 510,
        [Obsolete]
        GlimeshChannelSubscribed = 520,
        [Obsolete]
        GlimeshChannelResubscribed = 521,
        [Obsolete]
        GlimeshChannelSubscriptionGifted = 522,
        [Obsolete]
        GlimeshChannelDonation = 550,

        // 600 = Kick
        KickChannelStreamStart = 600,
        KickChannelStreamStop = 601,
        KickChannelUpdated = 602,

        KickChannelFollowed = 610,

        KickChannelSubscribed = 620,
        KickChannelResubscribed = 621,
        KickChannelSubscriptionGifted = 622,
        KickChannelMassSubscriptionsGifted = 623,

        KickChannelPointsRedeemed = 670,
        KickChannelKicksGifted = 671,

        // 700 = Velora
        VeloraChannelStreamStart = 700,
        VeloraChannelStreamStop = 701,
        VeloraChannelUpdated = 702,

        VeloraChannelFollowed = 710,

        VeloraChannelSubscribed = 720,
        VeloraChannelResubscribed = 721,
        VeloraChannelSubscriptionGifted = 722,
        VeloraChannelMassSubscriptionsGifted = 723,

        VeloraChannelRaided = 730,

        VeloraChannelPointsRedeemed = 770,
        VeloraChannelCheered = 771,

        // 800 = VPZone
        VPZoneChannelStreamStart = 800,
        VPZoneChannelStreamStop = 801,
        VPZoneChannelUpdated = 802,

        VPZoneChannelFollowed = 810,

        VPZoneChannelSubscribed = 820,
        VPZoneChannelResubscribed = 821,
        VPZoneChannelSubscriptionGifted = 822,
        VPZoneChannelMassSubscriptionsGifted = 823,

        VPZoneChannelRaided = 830,

        VPZoneChannelClipCreated = 840,
        VPZoneChannelShoutout = 841,

        VPZoneChannelPointsRedeemed = 870,
        VPZoneChannelCheered = 871,

        // Donation Services = 1000

        GenericDonation = 1999,

        StreamlabsDonation = 1000,

        TiltifyDonation = 1020,

        DonorDriveDonation = 1030,
        DonorDriveDonationIncentive = 1031,
        DonorDriveDonationMilestone = 1032,
        DonorDriveDonationTeamIncentive = 1033,
        DonorDriveDonationTeamMilestone = 1034,

        TipeeeStreamDonation = 1040,

        TreatStreamDonation = 1050,

        PatreonSubscribed = 1060,

        RainmakerDonation = 1070,

        JustGivingDonation = 1080,

        StreamlootsCardRedeemed = 1090,
        StreamlootsPackPurchased = 1091,
        StreamlootsPackGifted = 1092,
        StreamlootsPackCommunityGifted = 1093,

        StreamElementsDonation = 1100,
        [Obsolete]
        StreamElementsMerchPurchase = 1101,

        CrowdControlEffectRedeemed = 1110,

        PulsoidHeartRateChanged = 1120,

        ThroneGiftPurchased = 1130,
        ThroneContribution = 1131,
        ThroneGiftCrowdfunded = 1132,

        FourthwallDonation = 1140,
        FourthwallOrderPlaced = 1141,
        FourthwallGiftPurchase = 1142,

        KoFiTip = 1150,
        KoFiMembership = 1151,
        KoFiShopOrder = 1152,
        KoFiFirstMembership = 1153,
        KoFiCommission = 1154,

        PallyDonation = 1160,

        VeadotubeAvatarStateChanged = 1170,
        VeadotubePushToTalkChanged = 1171,

        VConnectTriggerActivated = 1180,
        VConnectTriggerEnded = 1181,
        VConnectAssetSpawned = 1182,
        VConnectAssetHit = 1183,
        VConnectAssetDespawned = 1184,
        VConnectMessageReceived = 1185,

        // Streaming Software = 1200

        OBSStudioStreamStarted = 1200,
        OBSStudioStreamStopped = 1201,
        OBSStudioRecordingStarted = 1202,
        OBSStudioRecordingStopped = 1203,
        OBSStudioSceneChanged = 1204,
        OBSStudioFilterVisibilityChanged = 1205,
        OBSStudioSceneTransitionStarted = 1206,
        OBSStudioSceneTransitionEnded = 1207,
        OBSStudioSourceVisibilityChanged = 1208,
        OBSStudioReplayBufferSaved = 1209,
    }

    public class SubscriptionDetailsModel
    {
        public UserV2ViewModel User { get; set; }
        public StreamingPlatformTypeEnum Platform { get; set; }
        public int Months { get; set; }

        public int Tier { get; set; } = 1;
        public string YouTubeMembershipTier { get; set; }

        public UserV2ViewModel Gifter { get; set; }

        public SubscriptionDetailsModel(StreamingPlatformTypeEnum platform, UserV2ViewModel user, int months = 1, int? tier = 1, string youTubeMembershipTier = null)
        {
            this.Platform = platform;
            this.User = user;
            this.Months = months;

            if (tier != null)
            {
                this.Tier = tier.GetValueOrDefault();
            }
            if (!string.IsNullOrEmpty(youTubeMembershipTier))
            {
                this.YouTubeMembershipTier = youTubeMembershipTier;
            }
        }

        public SubscriptionDetailsModel(StreamingPlatformTypeEnum platform, UserV2ViewModel user, UserV2ViewModel gifter, int months = 1, int? tier = null, string youTubeMembershipTier = null)
            : this(platform, user, months, tier, youTubeMembershipTier)
        {
            this.Gifter = gifter;
        }

        public override string ToString() { return $"{this.User?.Username} - {this.Gifter?.Username} - {this.Platform} - {this.Months} - {this.Tier} - {this.YouTubeMembershipTier}"; }
    }

    public class EventService
    {
        public static event EventHandler<UserV2ViewModel> OnFollowOccurred = delegate { };
        public static void FollowOccurred(UserV2ViewModel user) { OnFollowOccurred(null, user); }

        public static event EventHandler<Tuple<UserV2ViewModel, int>> OnRaidOccurred = delegate { };
        public static void RaidOccurred(UserV2ViewModel user, int viewers) { OnRaidOccurred(null, new Tuple<UserV2ViewModel, int>(user, viewers)); }

        public static event EventHandler<SubscriptionDetailsModel> OnSubscribeOccurred = delegate { };
        public static void SubscribeOccurred(SubscriptionDetailsModel subscription) { OnSubscribeOccurred(null, subscription); }

        public static event EventHandler<SubscriptionDetailsModel> OnResubscribeOccurred = delegate { };
        public static void ResubscribeOccurred(SubscriptionDetailsModel subscription) { OnResubscribeOccurred(null, subscription); }

        public static event EventHandler<SubscriptionDetailsModel> OnSubscriptionGiftedOccurred = delegate { };
        public static void SubscriptionGiftedOccurred(SubscriptionDetailsModel subscription) { OnSubscriptionGiftedOccurred(null, subscription); }

        public static event EventHandler<IEnumerable<SubscriptionDetailsModel>> OnMassSubscriptionsGiftedOccurred = delegate { };
        public static void MassSubscriptionsGiftedOccurred(IEnumerable<SubscriptionDetailsModel> subscriptions) { OnMassSubscriptionsGiftedOccurred(null, subscriptions); }

        public static event EventHandler<UserDonationModel> OnDonationOccurred = delegate { };
        public static void DonationOccurred(UserDonationModel donation) { OnDonationOccurred(null, donation); }

        public static event EventHandler<TwitchBitsCheeredEventModel> OnTwitchBitsCheeredOccurred = delegate { };
        public static void TwitchBitsCheeredOccurred(TwitchBitsCheeredEventModel bitsCheer) { OnTwitchBitsCheeredOccurred(null, bitsCheer); }

        public static event EventHandler<UserV2ViewModel> OnTwitchPowerUpOccurred = delegate { };
        public static void TwitchPowerUpOccurred(UserV2ViewModel user) { OnTwitchPowerUpOccurred(null, user); }

        public static event EventHandler<YouTubeSuperChatViewModel> OnYouTubeSuperChatOccurred = delegate { };
        public static void YouTubeSuperChatOccurred(YouTubeSuperChatViewModel superchat) { OnYouTubeSuperChatOccurred(null, superchat); }

        public static event EventHandler<KickKicksGiftedEventModel> OnKickKicksGiftedOccurred = delegate { };
        public static void KickKicksGiftedOccurred(KickKicksGiftedEventModel kicksGifted) { OnKickKicksGiftedOccurred(null, kicksGifted); }

        public static event EventHandler<VeloraCheeredEventModel> OnVeloraChannelCheeredOccurred = delegate { };
        public static void VeloraChannelCheeredOccurred(VeloraCheeredEventModel cheered) { OnVeloraChannelCheeredOccurred(null, cheered); }

        public static event EventHandler<VPZoneCheeredEventModel> OnVPZoneChannelCheeredOccurred = delegate { };
        public static void VPZoneChannelCheeredOccurred(VPZoneCheeredEventModel cheered) { OnVPZoneChannelCheeredOccurred(null, cheered); }

        public static event EventHandler<YouTubeJewelsGiftViewModel> OnYouTubeJewelsGiftOccurred = delegate { };
        public static void YouTubeJewelsGiftOccurred(YouTubeJewelsGiftViewModel jewelsGift) { OnYouTubeJewelsGiftOccurred(null, jewelsGift); }

        public static event EventHandler<Tuple<UserV2ViewModel, int>> OnChannelPointsRedeemedOccurred = delegate { };
        public static void ChannelPointsRedeemedOccurred(UserV2ViewModel user, int cost) { OnChannelPointsRedeemedOccurred(null, new Tuple<UserV2ViewModel, int>(user, cost)); }

        public static event EventHandler<Tuple<UserV2ViewModel, int>> OnStreamlootsOccurred = delegate { };
        public static void StreamlootsOccurred(UserV2ViewModel user, int quantity) { OnStreamlootsOccurred(null, new Tuple<UserV2ViewModel, int>(user, quantity)); }

        public static event EventHandler<UserV2ViewModel> OnPatreonSubscribedOccurred = delegate { };
        public static void PatreonSubscribedOccurred(UserV2ViewModel user) { OnPatreonSubscribedOccurred(null, user); }

        private static HashSet<EventTypeEnum> singleUseTracking = new HashSet<EventTypeEnum>()
        {
            EventTypeEnum.ChatUserFirstJoin, EventTypeEnum.ChatUserJoined, EventTypeEnum.ChatUserLeft,

            EventTypeEnum.ApplicationLaunch, EventTypeEnum.ApplicationExit,

            EventTypeEnum.TwitchChannelStreamStart, EventTypeEnum.TwitchChannelStreamStop, EventTypeEnum.TwitchChannelFollowed, EventTypeEnum.TwitchChannelRaided, EventTypeEnum.TwitchChannelOutgoingRaidCompleted, EventTypeEnum.TwitchChannelSubscribed, EventTypeEnum.TwitchChannelResubscribed,

            EventTypeEnum.YouTubeChannelStreamStart, EventTypeEnum.YouTubeChannelStreamStop, EventTypeEnum.YouTubeChannelNewMember, EventTypeEnum.YouTubeChannelMemberMilestone,

            EventTypeEnum.KickChannelStreamStart, EventTypeEnum.KickChannelStreamStop, EventTypeEnum.KickChannelFollowed, EventTypeEnum.KickChannelSubscribed, EventTypeEnum.KickChannelResubscribed,

            EventTypeEnum.VeloraChannelStreamStart, EventTypeEnum.VeloraChannelStreamStop, EventTypeEnum.VeloraChannelFollowed, EventTypeEnum.VeloraChannelRaided, EventTypeEnum.VeloraChannelSubscribed, EventTypeEnum.VeloraChannelResubscribed,

            EventTypeEnum.VPZoneChannelStreamStart, EventTypeEnum.VPZoneChannelStreamStop, EventTypeEnum.VPZoneChannelFollowed, EventTypeEnum.VPZoneChannelRaided, EventTypeEnum.VPZoneChannelSubscribed, EventTypeEnum.VPZoneChannelResubscribed,
        };

        private LockedDictionary<EventTypeEnum, HashSet<Guid>> userEventTracking = new LockedDictionary<EventTypeEnum, HashSet<Guid>>();

        public EventService()
        {
            foreach (EventTypeEnum type in singleUseTracking)
            {
                this.userEventTracking[type] = new HashSet<Guid>();
            }
        }

        public static async Task ProcessDonationEvent(EventTypeEnum type, UserDonationModel donation, List<string> arguments = null, Dictionary<string, string> additionalSpecialIdentifiers = null)
        {
            donation.AssignUser();

            CommandParametersModel parameters = new CommandParametersModel(donation.User, donation.Platform, arguments, donation.GetSpecialIdentifiers());

            if (additionalSpecialIdentifiers != null)
            {
                foreach (var kvp in additionalSpecialIdentifiers)
                {
                    parameters.SpecialIdentifiers[kvp.Key] = kvp.Value;
                }
            }

            foreach (StreamPassModel streamPass in ChannelSession.Settings.StreamPass.Values)
            {
                if (parameters.User.MeetsRole(streamPass.UserPermission))
                {
                    streamPass.AddAmount(donation.User, (int)Math.Ceiling(streamPass.DonationBonus * donation.Amount));
                }
            }

            parameters.User.TotalAmountDonated += donation.Amount;
            ChannelSession.Settings.LatestSpecialIdentifiersData[SpecialIdentifierStringBuilder.LatestDonationUserData] = parameters.User.ID;
            ChannelSession.Settings.LatestSpecialIdentifiersData[SpecialIdentifierStringBuilder.LatestDonationAmountData] = donation.AmountText;

            await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(parameters.User, string.Format(MixItUp.Base.Resources.AlertDonated, parameters.User.FullDisplayName, donation.AmountText), ChannelSession.Settings.AlertDonationColor));

            await ServiceManager.Get<EventService>().PerformEvent(type, parameters);
            await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.GenericDonation, parameters);

            try
            {
                EventService.DonationOccurred(donation);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        public Task Initialize()
        {
            return Task.CompletedTask;
        }

        public EventCommandModel GetEventCommand(EventTypeEnum type)
        {
            foreach (EventCommandModel command in ServiceManager.Get<CommandService>().EventCommands.ToList())
            {
                if (command.EventType == type)
                {
                    return command;
                }
            }
            return null;
        }

        public async Task<bool> PerformEvent(EventTypeEnum type, CommandParametersModel parameters)
        {
            try
            {
                switch (type)
                {
                    case EventTypeEnum.TwitchChannelFollowed:
                    case EventTypeEnum.KickChannelFollowed:
                    case EventTypeEnum.VeloraChannelFollowed:
                    case EventTypeEnum.VPZoneChannelFollowed:
                        ChannelSession.Settings.LastFollowerUserID = parameters.User.ID;
                        break;
                    case EventTypeEnum.TwitchChannelSubscribed:
                    case EventTypeEnum.KickChannelSubscribed:
                    case EventTypeEnum.VeloraChannelSubscribed:
                    case EventTypeEnum.VPZoneChannelSubscribed:
                    case EventTypeEnum.YouTubeChannelNewMember:
                    case EventTypeEnum.TwitchChannelResubscribed:
                    case EventTypeEnum.KickChannelResubscribed:
                    case EventTypeEnum.VeloraChannelResubscribed:
                    case EventTypeEnum.VPZoneChannelResubscribed:
                    case EventTypeEnum.YouTubeChannelMemberMilestone:
                        ChannelSession.Settings.LastSubscriberUserID = parameters.User.ID;
                        break;
                    case EventTypeEnum.TwitchChannelSubscriptionGifted:
                    case EventTypeEnum.KickChannelSubscriptionGifted:
                    case EventTypeEnum.VeloraChannelSubscriptionGifted:
                    case EventTypeEnum.VPZoneChannelSubscriptionGifted:
                    case EventTypeEnum.YouTubeChannelMembershipGifted:
                        if (parameters.TargetUser != null)
                        {
                            ChannelSession.Settings.LastSubscriberUserID = parameters.TargetUser.ID;
                        }
                        break;
                }

                if (this.CanPerformEvent(type, parameters))
                {
                    UserV2ViewModel user = parameters.User;
                    if (user == null)
                    {
                        user = ChannelSession.User;
                    }

                    if (this.userEventTracking.ContainsKey(type))
                    {
                        lock (this.userEventTracking)
                        {
                            this.userEventTracking[type].Add(user.ID);
                        }
                    }

                    user.UpdateLastActivity();
                    if (type != EventTypeEnum.ChatUserLeft)
                    {
                        await ServiceManager.Get<UserService>().AddOrUpdateActiveUser(user);
                    }

                    EventCommandModel command = this.GetEventCommand(type);
                    if (command != null)
                    {
                        Logger.Log(LogLevel.Debug, $"Performing platform event trigger: {type}");
                        await ServiceManager.Get<CommandService>().Queue(command, parameters);
                    }

                    EventCommandModel genericCommand = null;
                    switch (type)
                    {
                        case EventTypeEnum.TwitchChannelStreamStart:
                        case EventTypeEnum.YouTubeChannelStreamStart:
                        case EventTypeEnum.KickChannelStreamStart:
                        case EventTypeEnum.VeloraChannelStreamStart:
                        case EventTypeEnum.VPZoneChannelStreamStart:
                            genericCommand = this.GetEventCommand(EventTypeEnum.ChannelStreamStart);
                            break;
                        case EventTypeEnum.TwitchChannelStreamStop:
                        case EventTypeEnum.YouTubeChannelStreamStop:
                        case EventTypeEnum.KickChannelStreamStop:
                        case EventTypeEnum.VeloraChannelStreamStop:
                        case EventTypeEnum.VPZoneChannelStreamStop:
                            genericCommand = this.GetEventCommand(EventTypeEnum.ChannelStreamStop);
                            break;
                        case EventTypeEnum.TwitchChannelRaided:
                        case EventTypeEnum.VeloraChannelRaided:
                        case EventTypeEnum.VPZoneChannelRaided:
                            genericCommand = this.GetEventCommand(EventTypeEnum.ChannelRaided);
                            break;
                        case EventTypeEnum.TwitchChannelFollowed:
                        case EventTypeEnum.KickChannelFollowed:
                        case EventTypeEnum.VeloraChannelFollowed:
                        case EventTypeEnum.VPZoneChannelFollowed:
                            genericCommand = this.GetEventCommand(EventTypeEnum.ChannelFollowed);
                            break;
                        case EventTypeEnum.TwitchChannelSubscribed:
                        case EventTypeEnum.KickChannelSubscribed:
                        case EventTypeEnum.VeloraChannelSubscribed:
                        case EventTypeEnum.VPZoneChannelSubscribed:
                        case EventTypeEnum.YouTubeChannelNewMember:
                            genericCommand = this.GetEventCommand(EventTypeEnum.ChannelSubscribed);
                            break;
                        case EventTypeEnum.TwitchChannelResubscribed:
                        case EventTypeEnum.KickChannelResubscribed:
                        case EventTypeEnum.VeloraChannelResubscribed:
                        case EventTypeEnum.VPZoneChannelResubscribed:
                        case EventTypeEnum.YouTubeChannelMemberMilestone:
                            genericCommand = this.GetEventCommand(EventTypeEnum.ChannelResubscribed);
                            break;
                        case EventTypeEnum.TwitchChannelSubscriptionGifted:
                        case EventTypeEnum.KickChannelSubscriptionGifted:
                        case EventTypeEnum.VeloraChannelSubscriptionGifted:
                        case EventTypeEnum.VPZoneChannelSubscriptionGifted:
                        case EventTypeEnum.YouTubeChannelMembershipGifted:
                            genericCommand = this.GetEventCommand(EventTypeEnum.ChannelSubscriptionGifted);
                            break;
                        case EventTypeEnum.TwitchChannelMassSubscriptionsGifted:
                        case EventTypeEnum.KickChannelMassSubscriptionsGifted:
                        case EventTypeEnum.VeloraChannelMassSubscriptionsGifted:
                        case EventTypeEnum.VPZoneChannelMassSubscriptionsGifted:
                        case EventTypeEnum.YouTubeChannelMassMembershipGifted:
                            genericCommand = this.GetEventCommand(EventTypeEnum.ChannelMassSubscriptionsGifted);
                            break;
                    }

                    if (genericCommand != null)
                    {
                        Logger.Log(LogLevel.Debug, $"Performing generic event trigger: {genericCommand.EventType}");
                        await ServiceManager.Get<CommandService>().Queue(genericCommand, parameters);
                    }

                    ServiceManager.Get<StatisticsService>().LogEventStatistic(type, parameters);

                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"{type} - {parameters} - {ex}");
            }
            return false;
        }

        private bool CanPerformEvent(EventTypeEnum type, CommandParametersModel parameters)
        {
            UserV2ViewModel user = (parameters.User != null) ? parameters.User : ChannelSession.User;
            if (EventService.singleUseTracking.Contains(type) && this.userEventTracking.ContainsKey(type) && user != null)
            {
                return !this.userEventTracking[type].Contains(user.ID);
            }
            return true;
        }
    }
}

using MixItUp.Base.Model.Joystick;
using MixItUp.Base.Model.Razo;
using MixItUp.Base.Model.Kick.Kicks;
using MixItUp.Base.Model.Twitch.Bits;
using MixItUp.Base.Model.Velora;
using MixItUp.Base.Model.VPZone;
using MixItUp.Base.Model.User;
using MixItUp.Base.Services;
using MixItUp.Base.Services.Twitch;
using MixItUp.Base.Services.Twitch.New;
using MixItUp.Base.ViewModel.Chat;
using MixItUp.Base.ViewModel.Chat.YouTube;
using MixItUp.Base.ViewModel.User;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace MixItUp.Base.Model.Overlay
{
    [DataContract]
    public abstract class OverlayEventTrackingV3ModelBase : OverlayVisualTextV3ModelBase
    {
        [DataMember]
        public virtual bool Chatters { get; set; }

        [DataMember]
        public virtual bool ChatMessages { get; set; }

        [DataMember]
        public virtual bool Follows { get; set; }

        [DataMember]
        public virtual bool Raids { get; set; }

        [DataMember]
        public virtual bool TwitchSubscriptions { get; set; }
        [DataMember]
        public virtual bool TwitchBits { get; set; }
        [DataMember]
        public virtual bool TwitchPowerUps { get; set; }

        [DataMember]
        public virtual bool YouTubeMemberships { get; set; }
        [DataMember]
        public virtual bool YouTubeSuperChats { get; set; }
        [DataMember]
        public virtual bool YouTubeJewels { get; set; }

        [DataMember]
        public virtual bool KickSubscriptions { get; set; }
        [DataMember]
        public virtual bool KickKicks { get; set; }

        [DataMember]
        public virtual bool VeloraSubscriptions { get; set; }
        [DataMember]
        public virtual bool VPZoneSubscriptions { get; set; }
        [DataMember]
        public virtual bool VeloraCheered { get; set; }
        [DataMember]
        public virtual bool VPZoneCheered { get; set; }
        [DataMember]
        public virtual bool JoystickTipped { get; set; }
        [DataMember]
        public virtual bool RazoGift { get; set; }

        [DataMember]
        public virtual bool Donations { get; set; }

        [DataMember]
        public virtual bool ChannelPoints { get; set; }
        [DataMember]
        public virtual bool Streamloots { get; set; }
        [DataMember]
        public virtual bool Patreon { get; set; }

        public OverlayEventTrackingV3ModelBase(OverlayItemV3Type type) : base(type) { }

        public override async Task Initialize()
        {
            await base.Initialize();

            this.RemoveEventHandlers();

            ChatService.OnChatUserBanned += OnChatUserBanned;
            ChatService.OnChatUserTimedOut += OnChatUserTimedOut;

            if (this.Chatters || this.ChatMessages)
            {
                ChatService.OnChatMessageReceived += OnChatMessageReceived;
            }

            if (this.ChatMessages)
            {
                ChatService.OnChatMessageDeleted += OnChatMessageDeleted;
                ChatService.OnChatCleared += OnChatCleared;
            }

            if (this.Follows)
            {
                EventService.OnFollowOccurred += OnFollow;
            }

            if (this.Raids)
            {
                EventService.OnRaidOccurred += OnRaid;
            }

            if (this.TwitchSubscriptions)
            {
                EventService.OnSubscribeOccurred += OnSubscribe;
                EventService.OnResubscribeOccurred += OnSubscribe;
                EventService.OnSubscriptionGiftedOccurred += OnSubscribe;
                EventService.OnMassSubscriptionsGiftedOccurred += OnMassSubscription;
                EventService.RazoSubscriptionsGifted += OnRazoSubscriptionsGifted;
            }

            if (this.Donations)
            {
                EventService.OnDonationOccurred += OnDonation;
            }

            if (this.TwitchBits)
            {
                EventService.OnTwitchBitsCheeredOccurred += OnTwitchBits;
            }

            if (this.TwitchPowerUps)
            {
                EventService.OnTwitchPowerUpOccurred += OnTwitchPowerUp;
            }

            if (this.YouTubeSuperChats)
            {
                EventService.OnYouTubeSuperChatOccurred += OnYouTubeSuperChat;
            }

            if (this.KickKicks)
            {
                EventService.OnKickKicksGiftedOccurred += OnKickKicksGifted;
            }

            if (this.VeloraCheered)
            {
                EventService.OnVeloraChannelCheeredOccurred += OnVeloraCheered;
            }

            if (this.VPZoneCheered)
            {
                EventService.OnVPZoneChannelCheeredOccurred += OnVPZoneCheered;
            }
            if (this.JoystickTipped)
            {
                EventService.OnJoystickChannelTippedOccurred += OnJoystickTipped;
            }
            if (this.RazoGift)
            {
                EventService.OnRazoChannelGiftOccurred += OnRazoGift;
            }

            if (this.YouTubeJewels)
            {
                EventService.OnYouTubeJewelsGiftOccurred += OnYouTubeJewelsGift;
            }

            if (this.ChannelPoints)
            {
                EventService.OnChannelPointsRedeemedOccurred += OnChannelPointsRedeemed;
            }

            if (this.Streamloots)
            {
                EventService.OnStreamlootsOccurred += OnStreamloots;
            }

            if (this.Patreon)
            {
                EventService.OnPatreonSubscribedOccurred += OnPatreonSubscribed;
            }
        }

        public override async Task Uninitialize()
        {
            await base.Uninitialize();

            this.RemoveEventHandlers();
        }

        public virtual void OnChatUserBanned(object sender, UserV2ViewModel user) { }

        public virtual void OnChatUserTimedOut(object sender, UserV2ViewModel user) { }

        public virtual void OnChatMessageReceived(object sender, ChatMessageViewModel message) { }

        public virtual void OnChatMessageDeleted(object sender, string messageID) { }

        public virtual void OnChatCleared(object sender, EventArgs e) { }

        public virtual void OnFollow(object sender, UserV2ViewModel user) { }

        public virtual void OnRaid(object sender, Tuple<UserV2ViewModel, int> raid) { }

        public virtual void OnSubscribe(object sender, SubscriptionDetailsModel subscription) { }

        public virtual void OnMassSubscription(object sender, IEnumerable<SubscriptionDetailsModel> subscriptions) { }

        public virtual void OnDonation(object sender, UserDonationModel donation) { }

        public virtual void OnTwitchBits(object sender, TwitchBitsCheeredEventModel bitsCheered) { }

        public virtual void OnTwitchPowerUp(object sender, UserV2ViewModel user) { }

        public virtual void OnYouTubeSuperChat(object sender, YouTubeSuperChatViewModel superChat) { }

        public virtual void OnKickKicksGifted(object sender, KickKicksGiftedEventModel kicksGifted) { }

        public virtual void OnVeloraCheered(object sender, VeloraCheeredEventModel cheered) { }

        public virtual void OnVPZoneCheered(object sender, VPZoneCheeredEventModel cheered) { }
        public virtual void OnJoystickTipped(object sender, JoystickTippedEventModel cheered) { }
        public virtual void OnRazoSubscriptionsGifted(UserV2ViewModel gifter, int count) { }
        public virtual void OnRazoGift(object sender, RazoGiftEventModel cheered) { }

        public virtual void OnYouTubeJewelsGift(object sender, YouTubeJewelsGiftViewModel jewelsGift) { }

        public virtual void OnChannelPointsRedeemed(object sender, Tuple<UserV2ViewModel, int> redemption) { }

        public virtual void OnStreamloots(object sender, Tuple<UserV2ViewModel, int> purchase) { }

        public virtual void OnPatreonSubscribed(object sender, UserV2ViewModel user) { }

        private void RemoveEventHandlers()
        {
            ChatService.OnChatUserBanned -= OnChatUserBanned;
            ChatService.OnChatUserTimedOut -= OnChatUserTimedOut;
            ChatService.OnChatMessageReceived -= OnChatMessageReceived;
            ChatService.OnChatMessageDeleted -= OnChatMessageDeleted;
            ChatService.OnChatCleared -= OnChatCleared;
            EventService.OnFollowOccurred -= OnFollow;
            EventService.OnRaidOccurred -= OnRaid;
            EventService.OnSubscribeOccurred -= OnSubscribe;
            EventService.OnResubscribeOccurred -= OnSubscribe;
            EventService.OnSubscriptionGiftedOccurred -= OnSubscribe;
            EventService.OnMassSubscriptionsGiftedOccurred -= OnMassSubscription;
            EventService.RazoSubscriptionsGifted -= OnRazoSubscriptionsGifted;
            EventService.OnDonationOccurred -= OnDonation;
            EventService.OnTwitchBitsCheeredOccurred -= OnTwitchBits;
            EventService.OnTwitchPowerUpOccurred -= OnTwitchPowerUp;
            EventService.OnYouTubeSuperChatOccurred -= OnYouTubeSuperChat;
            EventService.OnKickKicksGiftedOccurred -= OnKickKicksGifted;
            EventService.OnVeloraChannelCheeredOccurred -= OnVeloraCheered;
            EventService.OnVPZoneChannelCheeredOccurred -= OnVPZoneCheered;
            EventService.OnJoystickChannelTippedOccurred -= OnJoystickTipped;
            EventService.OnRazoChannelGiftOccurred -= OnRazoGift;
            EventService.OnYouTubeJewelsGiftOccurred -= OnYouTubeJewelsGift;
            EventService.OnChannelPointsRedeemedOccurred -= OnChannelPointsRedeemed;
            EventService.OnStreamlootsOccurred -= OnStreamloots;
            EventService.OnPatreonSubscribedOccurred -= OnPatreonSubscribed;
        }
    }
}

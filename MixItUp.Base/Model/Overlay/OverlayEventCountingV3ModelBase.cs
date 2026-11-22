using MixItUp.Base.Model.Joystick;
using MixItUp.Base.Model.Razo;
using MixItUp.Base.Model.Kick.Kicks;
using MixItUp.Base.Model.Twitch.Bits;
using MixItUp.Base.Model.Velora;
using MixItUp.Base.Model.VPZone;
using MixItUp.Base.Model.User;
using MixItUp.Base.Services;
using MixItUp.Base.Services.Twitch.New;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Chat.YouTube;
using MixItUp.Base.ViewModel.User;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace MixItUp.Base.Model.Overlay
{
    [DataContract]
    public abstract class OverlayEventCountingV3ModelBase : OverlayVisualTextV3ModelBase
    {
        [DataMember]
        public double FollowAmount { get; set; }

        [DataMember]
        public double RaidAmount { get; set; }
        [DataMember]
        public double RaidPerViewAmount { get; set; }

        [DataMember]
        public Dictionary<int, double> TwitchSubscriptionsAmount { get; set; } = new Dictionary<int, double>();
        [DataMember]
        public double TwitchBitsAmount { get; set; }

        [DataMember]
        public Dictionary<string, double> YouTubeMembershipsAmount { get; set; } = new Dictionary<string, double>();
        [DataMember]
        public double YouTubeSuperChatAmount { get; set; }

        [DataMember]
        public Dictionary<int, double> KickSubscriptionsAmount { get; set; } = new Dictionary<int, double>();
        [DataMember]
        public double KickKicksAmount { get; set; }

        [DataMember]
        public Dictionary<int, double> VeloraSubscriptionsAmount { get; set; } = new Dictionary<int, double>();
        [DataMember]
        public Dictionary<int, double> VPZoneSubscriptionsAmount { get; set; } = new Dictionary<int, double>();
        [DataMember]
        public double VeloraCheeredAmount { get; set; }
        [DataMember]
        public double VPZoneCheeredAmount { get; set; }
        [DataMember]
        public double JoystickTippedAmount { get; set; }
        [DataMember]
        public double RazoGiftAmount { get; set; }
        [DataMember]
        public double RazoSubscriptionAmount { get; set; }

        [DataMember]
        public double DonationAmount { get; set; }

        public OverlayEventCountingV3ModelBase(OverlayItemV3Type type) : base(type) { }

        public abstract Task ProcessEvent(UserV2ViewModel user, double amount);

        public override async Task Initialize()
        {
            await base.Initialize();

            this.RemoveEventHandlers();

            if (this.FollowAmount > 0)
            {
                EventService.OnFollowOccurred += EventService_OnFollowOccurred;
            }

            if (this.RaidAmount > 0 || this.RaidPerViewAmount > 0)
            {
                EventService.OnRaidOccurred += EventService_OnRaidOccurred;
            }

            if (this.TwitchSubscriptionsAmount.Any(d => d.Value > 0) || this.YouTubeMembershipsAmount.Any(d => d.Value > 0) || this.KickSubscriptionsAmount.Any(d => d.Value > 0) || this.VeloraSubscriptionsAmount.Any(d => d.Value > 0) || this.VPZoneSubscriptionsAmount.Any(d => d.Value > 0))
            {
                EventService.OnSubscribeOccurred += EventService_OnSubscribeOccurred;
                EventService.OnResubscribeOccurred += EventService_OnSubscribeOccurred;
                EventService.OnSubscriptionGiftedOccurred += EventService_OnSubscribeOccurred;
                EventService.OnMassSubscriptionsGiftedOccurred += EventService_OnMassSubscriptionsGiftedOccurred;
            }

            if (this.DonationAmount > 0)
            {
                EventService.OnDonationOccurred += EventService_OnDonationOccurred;
            }

            if (this.TwitchBitsAmount > 0)
            {
                EventService.OnTwitchBitsCheeredOccurred += EventService_OnTwitchBitsCheeredOccurred;
            }

            if (this.YouTubeSuperChatAmount > 0)
            {
                EventService.OnYouTubeSuperChatOccurred += EventService_OnYouTubeSuperChatOccurred;
            }

            if (this.KickKicksAmount > 0)
            {
                EventService.OnKickKicksGiftedOccurred += EventService_OnKickKicksGiftedOccurred;
            }

            if (this.VeloraCheeredAmount > 0)
            {
                EventService.OnVeloraChannelCheeredOccurred += EventService_OnVeloraChannelCheeredOccurred;
            }

            if (this.VPZoneCheeredAmount > 0)
            {
                EventService.OnVPZoneChannelCheeredOccurred += EventService_OnVPZoneChannelCheeredOccurred;
            }
            if (this.JoystickTippedAmount > 0)
            {
                EventService.OnJoystickChannelTippedOccurred += EventService_OnJoystickChannelTippedOccurred;
            }
            if (this.RazoSubscriptionAmount > 0)
            {
                EventService.RazoSubscriptionsGifted += this.OnRazoSubscriptionsGifted;
                EventService.OnSubscribeOccurred += this.OnRazoSubscription;
                EventService.OnResubscribeOccurred += this.OnRazoSubscription;
            }
            if (this.RazoGiftAmount > 0)
            {
                EventService.OnRazoChannelGiftOccurred += EventService_OnRazoChannelGiftOccurred;
            }

        }

        public override async Task Uninitialize()
        {
            await base.Uninitialize();

            this.RemoveEventHandlers();
        }

        public void EnableFollows()
        {
            this.RemoveEventHandlers();
            EventService.OnFollowOccurred += EventService_OnFollowOccurred;
        }

        public void EnableSubscriptions()
        {
            this.RemoveEventHandlers();
            EventService.OnSubscribeOccurred += EventService_OnSubscribeOccurred;
            EventService.OnResubscribeOccurred += EventService_OnSubscribeOccurred;
            EventService.OnSubscriptionGiftedOccurred += EventService_OnSubscribeOccurred;
            EventService.OnMassSubscriptionsGiftedOccurred += EventService_OnMassSubscriptionsGiftedOccurred;
        }

        public void ClearAllAmountsToZero()
        {
            this.FollowAmount = 0;
            this.RaidAmount = 0;
            this.RaidPerViewAmount = 0;

            for (int i = 0; i < this.TwitchSubscriptionsAmount.Count; i++)
            {
                this.TwitchSubscriptionsAmount[i] = 0;
            }
            this.TwitchBitsAmount = 0;

            this.YouTubeMembershipsAmount.Clear();
            this.YouTubeSuperChatAmount = 0;

            this.KickSubscriptionsAmount.Clear();
            this.KickKicksAmount = 0;

            this.VeloraSubscriptionsAmount.Clear();
            this.VPZoneSubscriptionsAmount.Clear();
            this.VeloraCheeredAmount = 0;
            this.VPZoneCheeredAmount = 0;
            this.JoystickTippedAmount = 0;
            this.RazoGiftAmount = 0;
            this.RazoSubscriptionAmount = 0;

            this.DonationAmount = 0;
        }

        private async void EventService_OnFollowOccurred(object sender, UserV2ViewModel user)
        {
            Logger.Log(LogLevel.Debug, $"Processing follow for {this.ID} Overlay Widget");
            await this.ProcessEvent(user, this.FollowAmount);
        }

        private async void EventService_OnRaidOccurred(object sender, Tuple<UserV2ViewModel, int> raid)
        {
            Logger.Log(LogLevel.Debug, $"Processing raid of {raid.Item2} for {this.ID} Overlay Widget");
            await this.ProcessEvent(raid.Item1, this.RaidAmount + (this.RaidPerViewAmount * raid.Item2));
        }

        private async void EventService_OnSubscribeOccurred(object sender, SubscriptionDetailsModel subscription)
        {
            Logger.Log(LogLevel.Debug, $"Processing sub of {subscription} for {this.ID} Overlay Widget");
            if (subscription.Platform == StreamingPlatformTypeEnum.Twitch)
            {
                if (this.TwitchSubscriptionsAmount.TryGetValue(subscription.Tier, out double amount))
                {
                    await this.ProcessEvent(subscription.User, amount);
                }
            }
            else if (subscription.Platform == StreamingPlatformTypeEnum.YouTube)
            {
                if (this.YouTubeMembershipsAmount.TryGetValue(subscription.YouTubeMembershipTier, out double amount))
                {
                    await this.ProcessEvent(subscription.User, amount);
                }
            }
            else if (subscription.Platform == StreamingPlatformTypeEnum.Kick)
            {
                if (this.KickSubscriptionsAmount.TryGetValue(subscription.Tier, out double amount))
                {
                    await this.ProcessEvent(subscription.User, amount);
                }
            }
            else if (subscription.Platform == StreamingPlatformTypeEnum.Velora)
            {
                if (this.VeloraSubscriptionsAmount.TryGetValue(subscription.Tier, out double amount))
                {
                    await this.ProcessEvent(subscription.User, amount);
                }
            }
            else if (subscription.Platform == StreamingPlatformTypeEnum.VPZone)
            {
                if (this.VPZoneSubscriptionsAmount.TryGetValue(subscription.Tier, out double amount))
                {
                    await this.ProcessEvent(subscription.User, amount);
                }
            }
        }

        private async void EventService_OnMassSubscriptionsGiftedOccurred(object sender, IEnumerable<SubscriptionDetailsModel> subscriptions)
        {
            Logger.Log(LogLevel.Debug, $"Processing mass sub of {subscriptions.Count()} for {this.ID} Overlay Widget");
            if (subscriptions.Count() > 0)
            {
                double total = 0;
                foreach (SubscriptionDetailsModel subscription in subscriptions)
                {
                    if (subscription.Platform == StreamingPlatformTypeEnum.Twitch)
                    {
                        if (this.TwitchSubscriptionsAmount.TryGetValue(subscription.Tier, out double amount))
                        {
                            total += amount;
                        }
                    }
                    else if (subscription.Platform == StreamingPlatformTypeEnum.YouTube)
                    {
                        if (this.YouTubeMembershipsAmount.TryGetValue(subscription.YouTubeMembershipTier, out double amount))
                        {
                            total += amount;
                        }
                    }
                    else if (subscription.Platform == StreamingPlatformTypeEnum.Kick)
                    {
                        if (this.KickSubscriptionsAmount.TryGetValue(subscription.Tier, out double amount))
                        {
                            total += amount;
                        }
                    }
                    else if (subscription.Platform == StreamingPlatformTypeEnum.Velora)
                    {
                        if (this.VeloraSubscriptionsAmount.TryGetValue(subscription.Tier, out double amount))
                        {
                            total += amount;
                        }
                    }
                    else if (subscription.Platform == StreamingPlatformTypeEnum.VPZone)
                    {
                        if (this.VPZoneSubscriptionsAmount.TryGetValue(subscription.Tier, out double amount))
                        {
                            total += amount;
                        }
                    }
                }

                await this.ProcessEvent(subscriptions.First().User, total);
            }
        }

        private async void EventService_OnDonationOccurred(object sender, UserDonationModel donation)
        {
            Logger.Log(LogLevel.Debug, $"Processing donation of {donation.Amount} for {this.ID} Overlay Widget");
            await this.ProcessEvent(donation.User, this.DonationAmount * donation.Amount);
        }

        private async void EventService_OnTwitchBitsCheeredOccurred(object sender, TwitchBitsCheeredEventModel bitsCheered)
        {
            Logger.Log(LogLevel.Debug, $"Processing Twitch bits of {bitsCheered.Amount} for {this.ID} Overlay Widget");
            await this.ProcessEvent(bitsCheered.User, this.TwitchBitsAmount * bitsCheered.Amount);
        }

        private async void EventService_OnYouTubeSuperChatOccurred(object sender, YouTubeSuperChatViewModel superChat)
        {
            Logger.Log(LogLevel.Debug, $"Processing YouTube Super Chat of {superChat.Amount} for {this.ID} Overlay Widget");
            await this.ProcessEvent(superChat.User, this.YouTubeSuperChatAmount * superChat.Amount);
        }

        private async void EventService_OnKickKicksGiftedOccurred(object sender, KickKicksGiftedEventModel kicksGifted)
        {
            Logger.Log(LogLevel.Debug, $"Processing Kick Kicks of {kicksGifted.Amount} for {this.ID} Overlay Widget");
            await this.ProcessEvent(kicksGifted.User, this.KickKicksAmount * kicksGifted.Amount);
        }

        private async void EventService_OnVeloraChannelCheeredOccurred(object sender, VeloraCheeredEventModel cheered)
        {
            Logger.Log(LogLevel.Debug, $"Processing Velora Cheered of {cheered.Amount} for {this.ID} Overlay Widget");
            await this.ProcessEvent(cheered.User, this.VeloraCheeredAmount * cheered.Amount);
        }

        private async void EventService_OnVPZoneChannelCheeredOccurred(object sender, VPZoneCheeredEventModel cheered)
        {
            Logger.Log(LogLevel.Debug, $"Processing VPZone Cheered of {cheered.Amount} for {this.ID} Overlay Widget");
            await this.ProcessEvent(cheered.User, this.VPZoneCheeredAmount * cheered.Amount);
        }
        private async void EventService_OnJoystickChannelTippedOccurred(object sender, JoystickTippedEventModel cheered)
        {
            Logger.Log(LogLevel.Debug, $"Processing Joystick tip of {cheered.Amount} for {this.ID} Overlay Widget");
            await this.ProcessEvent(cheered.User, this.JoystickTippedAmount * cheered.Amount);
        }
        private async void EventService_OnRazoChannelGiftOccurred(object sender, RazoGiftEventModel cheered)
        {
            Logger.Log(LogLevel.Debug, $"Processing Razos gift of {cheered.Amount} for {this.ID} Overlay Widget");
            await this.ProcessEvent(cheered.User, this.RazoGiftAmount * cheered.Amount);
        }

        private async void OnRazoSubscriptionsGifted(UserV2ViewModel gifter, int count)
        {
            await this.ProcessEvent(gifter, count * this.RazoSubscriptionAmount);
        }

        private async void OnRazoSubscription(object sender, SubscriptionDetailsModel subscription)
        {
            if (subscription.Platform == StreamingPlatformTypeEnum.Razo) await this.ProcessEvent(subscription.User, this.RazoSubscriptionAmount);
        }

        private void RemoveEventHandlers()
        {
            EventService.OnFollowOccurred -= EventService_OnFollowOccurred;
            EventService.OnRaidOccurred -= EventService_OnRaidOccurred;
            EventService.OnSubscribeOccurred -= EventService_OnSubscribeOccurred;
            EventService.OnResubscribeOccurred -= EventService_OnSubscribeOccurred;
            EventService.OnSubscriptionGiftedOccurred -= EventService_OnSubscribeOccurred;
            EventService.OnMassSubscriptionsGiftedOccurred -= EventService_OnMassSubscriptionsGiftedOccurred;
            EventService.OnDonationOccurred -= EventService_OnDonationOccurred;
            EventService.OnTwitchBitsCheeredOccurred -= EventService_OnTwitchBitsCheeredOccurred;
            EventService.OnYouTubeSuperChatOccurred -= EventService_OnYouTubeSuperChatOccurred;
            EventService.OnKickKicksGiftedOccurred -= EventService_OnKickKicksGiftedOccurred;
            EventService.OnVeloraChannelCheeredOccurred -= EventService_OnVeloraChannelCheeredOccurred;
            EventService.OnVPZoneChannelCheeredOccurred -= EventService_OnVPZoneChannelCheeredOccurred;
            EventService.OnJoystickChannelTippedOccurred -= EventService_OnJoystickChannelTippedOccurred;
            EventService.OnRazoChannelGiftOccurred -= EventService_OnRazoChannelGiftOccurred;
            EventService.RazoSubscriptionsGifted -= this.OnRazoSubscriptionsGifted;
            EventService.OnSubscribeOccurred -= this.OnRazoSubscription;
            EventService.OnResubscribeOccurred -= this.OnRazoSubscription;
        }
    }
}

using MixItUp.Base.Model;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Services;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModels;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

namespace MixItUp.Base.ViewModel.MainControls
{
    public class EventCommandGroupViewModel : NotifyPropertyChangedBase
    {
        public string Name { get; set; }

        public string BrandId { get; set; }

        public string IconName { get; set; }

        public ObservableCollection<EventCommandItemViewModel> Commands { get; set; } = new ObservableCollection<EventCommandItemViewModel>();

        public bool ShowBrandImage { get { return !string.IsNullOrEmpty(this.BrandId); } }

        public bool ShowIcon { get { return !this.ShowBrandImage; } }

        public EventCommandGroupViewModel(string name, string brandId = null, string iconName = null)
        {
            this.Name = name;
            this.BrandId = brandId;
            this.IconName = iconName;

            IThemeService themeService = ServiceManager.Get<IThemeService>();
            if (themeService != null)
            {
                // Brand marks are theme-specific; re-resolve the bound image when the theme changes
                themeService.ThemeChanged += (sender, e) => this.NotifyPropertyChanged(nameof(this.BrandId));
            }
        }
    }

    public class EventCommandItemViewModel : UIViewModelBase
    {
        public EventTypeEnum EventType { get; set; }

        public EventCommandModel Command { get; set; }

        public EventCommandItemViewModel(EventTypeEnum eventType)
        {
            this.EventType = eventType;
            this.RefreshCommand();
        }

        public string Name { get { return EnumLocalizationHelper.GetLocalizedName(this.EventType); } }

        public string Service
        {
            get
            {
                int eventNumber = (int)this.EventType;
                if (this.EventType == EventTypeEnum.StreamlabsDonation)
                {
                    return Resources.Streamlabs;
                }
                else if (this.EventType == EventTypeEnum.TiltifyDonation)
                {
                    return Resources.Tiltify;
                }
                else if (this.EventType == EventTypeEnum.DonorDriveDonation || this.EventType == EventTypeEnum.DonorDriveDonationIncentive || this.EventType == EventTypeEnum.DonorDriveDonationMilestone ||
                    this.EventType == EventTypeEnum.DonorDriveDonationTeamIncentive || this.EventType == EventTypeEnum.DonorDriveDonationTeamMilestone)
                {
                    return Resources.DonorDrive;
                }
                else if (this.EventType == EventTypeEnum.TipeeeStreamDonation)
                {
                    return Resources.TipeeeStream;
                }
                else if (this.EventType == EventTypeEnum.TreatStreamDonation)
                {
                    return Resources.TreatStream;
                }
                else if (this.EventType == EventTypeEnum.RainmakerDonation)
                {
                    return Resources.Rainmaker;
                }
                else if (this.EventType == EventTypeEnum.PatreonSubscribed)
                {
                    return Resources.Patreon;
                }
                else if (this.EventType == EventTypeEnum.JustGivingDonation)
                {
                    return Resources.JustGiving;
                }
                else if (this.EventType == EventTypeEnum.StreamlootsCardRedeemed || this.EventType == EventTypeEnum.StreamlootsPackGifted || this.EventType == EventTypeEnum.StreamlootsPackPurchased || this.EventType == EventTypeEnum.StreamlootsPackCommunityGifted)
                {
                    return Resources.Streamloots;
                }
                else if (this.EventType == EventTypeEnum.StreamElementsDonation)
                {
                    return Resources.StreamElements;
                }
                else if (this.EventType == EventTypeEnum.ThroneGiftPurchased || this.EventType == EventTypeEnum.ThroneContribution || this.EventType == EventTypeEnum.ThroneGiftCrowdfunded)
                {
                    return Resources.Throne;
                }
                else if (this.EventType == EventTypeEnum.FourthwallDonation || this.EventType == EventTypeEnum.FourthwallOrderPlaced || this.EventType == EventTypeEnum.FourthwallGiftPurchase)
                {
                    return Resources.Fourthwall;
                }
                else if (this.EventType == EventTypeEnum.KoFiTip || this.EventType == EventTypeEnum.KoFiMembership || this.EventType == EventTypeEnum.KoFiShopOrder || this.EventType == EventTypeEnum.KoFiFirstMembership || this.EventType == EventTypeEnum.KoFiCommission)
                {
                    return Resources.KoFi;
                }
                else if (this.EventType == EventTypeEnum.PallyDonation)
                {
                    return Resources.Pally;
                }
                else if (eventNumber >= 1180 && eventNumber < 1190)
                {
                    return Resources.VConnect;
                }
                else if (eventNumber >= 1200 && eventNumber < 1300)
                {
                    return Resources.OBSStudio;
                }
                else if (eventNumber >= 200 && eventNumber < 300)
                {
                    return Resources.Twitch;
                }
                else if (eventNumber >= 300 && eventNumber < 400)
                {
                    return Resources.YouTube;
                }
                else if (eventNumber >= 600 && eventNumber < 700)
                {
                    return Resources.Kick;
                }
                else if (eventNumber >= 700 && eventNumber < 800)
                {
                    return Resources.Velora;
                }
                else if (eventNumber >= 800 && eventNumber < 900)
                {
                    return Resources.VPZone;
                }
                else
                {
                    return Resources.Generic;
                }
            }
        }

        public bool IsNewCommand { get { return this.Command == null; } }

        public bool IsExistingCommand { get { return this.Command != null; } }

        public void RefreshCommand()
        {
            this.Command = ServiceManager.Get<EventService>().GetEventCommand(this.EventType);
            this.NotifyPropertyChanged("Command");
            this.NotifyPropertyChanged("Name");
            this.NotifyPropertyChanged("Service");
            this.NotifyPropertyChanged("IsNewCommand");
            this.NotifyPropertyChanged("IsExistingCommand");
        }
    }

    public class EventsMainControlViewModel : WindowControlViewModelBase
    {
        public ObservableCollection<EventCommandGroupViewModel> EventCommandGroups { get; set; } = new ObservableCollection<EventCommandGroupViewModel>();

        public Dictionary<EventTypeEnum, EventCommandItemViewModel> EventTypeItems { get; set; } = new Dictionary<EventTypeEnum, EventCommandItemViewModel>();

        private readonly List<EventCommandGroupViewModel> allCommandGroups = new List<EventCommandGroupViewModel>();

        protected override async Task OnVisibleInternal()
        {
            this.RefreshVisibleGroups();
            await base.OnVisibleInternal();
        }

        private void RefreshVisibleGroups()
        {
            this.EventCommandGroups.Clear();
            foreach (EventCommandGroupViewModel group in this.allCommandGroups)
            {
                if (!Enum.TryParse(group.BrandId, true, out StreamingPlatformTypeEnum platform) || !ChannelSession.Settings.PlatformsToHide.Contains(platform))
                {
                    this.EventCommandGroups.Add(group);
                }
            }
        }

        public EventsMainControlViewModel(MainWindowViewModel windowViewModel)
            : base(windowViewModel)
        {
            this.EventCommandGroups.Clear();

            List<EventCommandGroupViewModel> commandGroups = this.allCommandGroups;

            EventCommandGroupViewModel genericCommands = new EventCommandGroupViewModel(Resources.Generic, iconName: "flash_on");
            genericCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.ApplicationLaunch));
            genericCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.ApplicationExit));
            genericCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.ChannelStreamStart));
            genericCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.ChannelStreamStop));
            genericCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.ChannelFollowed));
            genericCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.ChannelHosted));
            genericCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.ChannelRaided));
            genericCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.ChannelSubscribed));
            genericCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.ChannelResubscribed));
            genericCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.ChannelSubscriptionGifted));
            genericCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.ChannelMassSubscriptionsGifted));
            genericCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.GenericDonation));
            commandGroups.Add(genericCommands);

            EventCommandGroupViewModel twitchCommands = new EventCommandGroupViewModel(Resources.Twitch, brandId: "twitch");
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelStreamStart));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelStreamStop));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelUpdated));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelFollowed));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelRaided));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelOutgoingRaidCompleted));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelSubscribed));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelResubscribed));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelSubscriptionGifted));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelMassSubscriptionsGifted));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelWatchStreak));
            if (MixItUpService.Options.TwitchModiversaryEnabled)
            {
                twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelModiversary));
            }
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelHighlightedMessage));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelUserIntro));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelPowerUpMessageEffect));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelPowerUpGigantifiedEmote));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelPowerUpCelebration));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelBitsCheered));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelPointsRedeemed));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelCustomPowerUpRedeemed));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelCharityDonation));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelAdUpcoming));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelAdStarted));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelAdEnded));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelHypeTrainBegin));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelHypeTrainProgress));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelHypeTrainLevelUp));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelHypeTrainEnd));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelSharedChatStarted));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelSharedChatEnded));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelUserWarned));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelShoutoutReceived));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelSuspiciousUserMessage));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelSuspiciousUserUpdated));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelShieldModeStarted));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelShieldModeEnded));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelUnbanRequestCreated));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelUnbanRequestResolved));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelGoalStarted));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelGoalProgress));
            twitchCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TwitchChannelGoalEnded));
            commandGroups.Add(twitchCommands);

            EventCommandGroupViewModel youtubeCommands = new EventCommandGroupViewModel(Resources.YouTube, brandId: "youtube");
            youtubeCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.YouTubeChannelStreamStart));
            youtubeCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.YouTubeChannelStreamStop));
            youtubeCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.YouTubeChannelNewMember));
            youtubeCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.YouTubeChannelMemberMilestone));
            youtubeCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.YouTubeChannelMembershipGifted));
            youtubeCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.YouTubeChannelMassMembershipGifted));
            youtubeCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.YouTubeChannelSuperChat));
            youtubeCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.YouTubeChannelJewelsGift));
            commandGroups.Add(youtubeCommands);

            EventCommandGroupViewModel kickCommands = new EventCommandGroupViewModel(Resources.Kick, brandId: "kick");
            kickCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.KickChannelStreamStart));
            kickCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.KickChannelStreamStop));
            kickCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.KickChannelUpdated));
            kickCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.KickChannelFollowed));
            kickCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.KickChannelSubscribed));
            kickCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.KickChannelResubscribed));
            kickCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.KickChannelSubscriptionGifted));
            kickCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.KickChannelMassSubscriptionsGifted));
            kickCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.KickChannelPointsRedeemed));
            kickCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.KickChannelKicksGifted));
            commandGroups.Add(kickCommands);

            EventCommandGroupViewModel veloraCommands = new EventCommandGroupViewModel(Resources.Velora, brandId: "velora");
            veloraCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.VeloraChannelStreamStart));
            veloraCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.VeloraChannelStreamStop));
            veloraCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.VeloraChannelUpdated));
            veloraCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.VeloraChannelFollowed));
            veloraCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.VeloraChannelRaided));
            veloraCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.VeloraChannelSubscribed));
            veloraCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.VeloraChannelResubscribed));
            veloraCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.VeloraChannelSubscriptionGifted));
            veloraCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.VeloraChannelMassSubscriptionsGifted));
            veloraCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.VeloraChannelPointsRedeemed));
            veloraCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.VeloraChannelCheered));
            commandGroups.Add(veloraCommands);

            EventCommandGroupViewModel vpzoneCommands = new EventCommandGroupViewModel(Resources.VPZone, brandId: "vpzone");
            vpzoneCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.VPZoneChannelStreamStart));
            vpzoneCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.VPZoneChannelStreamStop));
            vpzoneCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.VPZoneChannelUpdated));
            vpzoneCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.VPZoneChannelFollowed));
            vpzoneCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.VPZoneChannelRaided));
            vpzoneCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.VPZoneChannelSubscribed));
            vpzoneCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.VPZoneChannelResubscribed));
            vpzoneCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.VPZoneChannelSubscriptionGifted));
            vpzoneCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.VPZoneChannelMassSubscriptionsGifted));
            vpzoneCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.VPZoneChannelPointsRedeemed));
            vpzoneCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.VPZoneChannelCheered));
            vpzoneCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.VPZoneChannelClipCreated));
            vpzoneCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.VPZoneChannelShoutout));
            commandGroups.Add(vpzoneCommands);

            EventCommandGroupViewModel chatCommands = new EventCommandGroupViewModel(Resources.Chat, iconName: "forum");
            chatCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.ChatUserEntranceCommand));
            chatCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.ChatUserFirstMessage));
            chatCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.ChatMessageReceived));
            chatCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.ChatWhisperReceived));
            chatCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.ChatMessageDeleted));
            chatCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.ChatUserTimeout));
            chatCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.ChatUserBan));
            chatCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.ChatUserFirstJoin));
            chatCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.ChatUserJoined));
            chatCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.ChatUserLeft));
            commandGroups.Add(chatCommands);

            EventCommandGroupViewModel donationCommands = new EventCommandGroupViewModel(Resources.Donations, iconName: "paid");
            donationCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.DonorDriveDonation));
            donationCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.DonorDriveDonationIncentive));
            donationCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.DonorDriveDonationMilestone));
            donationCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.DonorDriveDonationTeamIncentive));
            donationCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.DonorDriveDonationTeamMilestone));
            donationCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.StreamlabsDonation));
            donationCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.StreamElementsDonation));
            donationCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TipeeeStreamDonation));
            donationCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TreatStreamDonation));
            donationCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.RainmakerDonation));
            donationCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.TiltifyDonation));
            donationCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.JustGivingDonation));
            commandGroups.Add(donationCommands);

            EventCommandGroupViewModel streamlootsCommands = new EventCommandGroupViewModel(Resources.Streamloots, brandId: "streamloots");
            streamlootsCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.StreamlootsCardRedeemed));
            streamlootsCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.StreamlootsPackPurchased));
            streamlootsCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.StreamlootsPackGifted));
            streamlootsCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.StreamlootsPackCommunityGifted));
            commandGroups.Add(streamlootsCommands);

            EventCommandGroupViewModel crowdControlCommands = new EventCommandGroupViewModel(Resources.CrowdControl, brandId: "crowd-control");
            crowdControlCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.CrowdControlEffectRedeemed));
            commandGroups.Add(crowdControlCommands);

            EventCommandGroupViewModel pulsoidCommands = new EventCommandGroupViewModel(Resources.Pulsoid, brandId: "pulsoid");
            pulsoidCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.PulsoidHeartRateChanged));
            commandGroups.Add(pulsoidCommands);

            EventCommandGroupViewModel veadotubeCommands = new EventCommandGroupViewModel(Resources.Veadotube, brandId: "veadotube");
            veadotubeCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.VeadotubeAvatarStateChanged));
            veadotubeCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.VeadotubePushToTalkChanged));
            commandGroups.Add(veadotubeCommands);

            EventCommandGroupViewModel vconnectCommands = new EventCommandGroupViewModel(Resources.VConnect, brandId: "vconnect");
            vconnectCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.VConnectTriggerActivated));
            vconnectCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.VConnectTriggerEnded));
            vconnectCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.VConnectAssetSpawned));
            vconnectCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.VConnectAssetHit));
            vconnectCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.VConnectAssetDespawned));
            vconnectCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.VConnectMessageReceived));
            commandGroups.Add(vconnectCommands);

            EventCommandGroupViewModel patreonCommands = new EventCommandGroupViewModel(Resources.Patreon, brandId: "patreon");
            patreonCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.PatreonSubscribed));
            commandGroups.Add(patreonCommands);

            EventCommandGroupViewModel throneCommands = new EventCommandGroupViewModel(Resources.Throne, brandId: "throne");
            throneCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.ThroneGiftPurchased));
            throneCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.ThroneContribution));
            throneCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.ThroneGiftCrowdfunded));
            commandGroups.Add(throneCommands);

            EventCommandGroupViewModel fourthwallCommands = new EventCommandGroupViewModel(Resources.Fourthwall, brandId: "fourthwall");
            fourthwallCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.FourthwallDonation));
            fourthwallCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.FourthwallOrderPlaced));
            fourthwallCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.FourthwallGiftPurchase));
            commandGroups.Add(fourthwallCommands);

            EventCommandGroupViewModel koFiCommands = new EventCommandGroupViewModel(Resources.KoFi, brandId: "kofi");
            koFiCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.KoFiTip));
            koFiCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.KoFiCommission));
            koFiCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.KoFiFirstMembership));
            koFiCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.KoFiMembership));
            koFiCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.KoFiShopOrder));
            commandGroups.Add(koFiCommands);

            EventCommandGroupViewModel pallyCommands = new EventCommandGroupViewModel(Resources.Pally, brandId: "pally");
            pallyCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.PallyDonation));
            commandGroups.Add(pallyCommands);

            EventCommandGroupViewModel obsStudioCommands = new EventCommandGroupViewModel(Resources.OBSStudio, brandId: "obs");
            obsStudioCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.OBSStudioStreamStarted));
            obsStudioCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.OBSStudioStreamStopped));
            obsStudioCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.OBSStudioRecordingStarted));
            obsStudioCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.OBSStudioRecordingStopped));
            obsStudioCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.OBSStudioReplayBufferSaved));
            obsStudioCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.OBSStudioSceneChanged));
            obsStudioCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.OBSStudioSceneTransitionStarted));
            obsStudioCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.OBSStudioSceneTransitionEnded));
            obsStudioCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.OBSStudioSourceVisibilityChanged));
            obsStudioCommands.Commands.Add(new EventCommandItemViewModel(EventTypeEnum.OBSStudioFilterVisibilityChanged));
            commandGroups.Add(obsStudioCommands);

            this.RefreshVisibleGroups();

            foreach (EventCommandGroupViewModel group in this.allCommandGroups)
            {
                foreach (EventCommandItemViewModel item in group.Commands)
                {
                    this.EventTypeItems[item.EventType] = item;
                }
            }
        }
    }
}

using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.Kick.Kicks;
using MixItUp.Base.Model.Velora;
using MixItUp.Base.Model.VPZone;
using MixItUp.Base.Model.Settings;
using MixItUp.Base.Model.Twitch.Bits;
using MixItUp.Base.Model.User;
using MixItUp.Base.Services;
using MixItUp.Base.Services.Twitch;
using MixItUp.Base.Services.Twitch.New;
using MixItUp.Base.Services.Kick.New;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Chat.YouTube;
using MixItUp.Base.ViewModel.User;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Base.Model.Overlay
{
    public enum OverlayLabelDisplayV3TypeEnum
    {
        ViewerCount,
        ChatterCount,
        LatestFollower,
        TotalFollowers,
        LatestSubscriber,
        TotalSubscribers,
        LatestRaid,
        LatestDonation,
        LatestTwitchBits,
        LatestYouTubeSuperChat,
        LatestKicksGifted,
        LatestSubscriptionGifter,
        LatestVeloraCheered,
        LatestVPZoneCheered,

        Counter = 100,

        File = 200,

        Date = 300,
        Time = 301,
        CustomText = 302,
    }

    public enum OverlayLabelDisplayV3SettingTypeEnum
    {
        RotatingDisplays,
        NewestOnly,
    }

    [DataContract]
    public class OverlayLabelDisplayV3Model
    {
        [DataMember]
        public OverlayLabelDisplayV3TypeEnum Type { get; set; }

        [DataMember]
        public bool IsEnabled { get; set; }

        [DataMember]
        public string Format { get; set; }

        [DataMember]
        public Guid UserID { get; set; }
        [DataMember]
        public UserV2Model UserFallback { get; set; }

        [DataMember]
        public double Amount { get; set; }
        [DataMember]
        public string AmountText { get; set; }

        [DataMember]
        public string CounterName { get; set; }

        [DataMember]
        public string FilePath { get; set; }

        [DataMember]
        public string TimeZoneID { get; set; }

        public void ResetData()
        {
            this.UserID = Guid.Empty;
            this.UserFallback = null;
            this.Amount = 0;
            this.AmountText = string.Empty;
        }
    }

    [DataContract]
    public class OverlayLabelV3Model : OverlayVisualTextV3ModelBase
    {
        public const string UsernamePropertyName = "Username";
        public const string AmountPropertyName = "Amount";
        public const string TypeNamePropertyName = "TypeName";
        public const string DateTimeFormatPropertyName = "DateTimeFormat";
        public const string TimeZonePropertyName = "TimeZone";

        public static readonly string DefaultHTML = OverlayResources.OverlayLabelDefaultHTML;
        public static readonly string DefaultCSS = OverlayResources.OverlayLabelDefaultCSS + Environment.NewLine + Environment.NewLine + OverlayResources.OverlayTextDefaultCSS;
        public static readonly string DefaultJavascript = OverlayResources.OverlayLabelDefaultJavascript;

        [DataMember]
        public OverlayLabelDisplayV3SettingTypeEnum DisplaySetting { get; set; }

        [DataMember]
        public int DisplayRotationSeconds { get; set; }

        [DataMember]
        public Dictionary<OverlayLabelDisplayV3TypeEnum, OverlayLabelDisplayV3Model> Displays { get; set; } = new Dictionary<OverlayLabelDisplayV3TypeEnum, OverlayLabelDisplayV3Model>();

        [DataMember]
        public OverlayAnimationV3Model DisplayEntranceAnimation { get; set; } = new OverlayAnimationV3Model();
        [DataMember]
        public OverlayAnimationV3Model DisplayExitAnimation { get; set; } = new OverlayAnimationV3Model();

        private CancellationTokenSource refreshCancellationTokenSource;

        private FileSystemWatcher fileSystemWatcher = null;

        // The last text pushed to the widget per display, so that polled displays can skip
        // sending an update that would not change anything on screen. Seeded by Loaded() so
        // a reconnected overlay is never compared against text from the previous session.
        private readonly ConcurrentDictionary<OverlayLabelDisplayV3TypeEnum, string> lastSentFormats = new ConcurrentDictionary<OverlayLabelDisplayV3TypeEnum, string>();

        public OverlayLabelV3Model() : base(OverlayItemV3Type.Label) { }

        public override async Task Initialize()
        {
            if (string.Equals(this.Javascript, OverlayResources.OverlayLabelDefaultJavascriptOld, System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(this.Javascript, OverlayResources.OverlayLabelDefaultJavascriptOld2, System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(this.Javascript, OverlayResources.OverlayLabelDefaultJavascriptOld3, System.StringComparison.OrdinalIgnoreCase))
            {
                this.Javascript = OverlayResources.OverlayLabelDefaultJavascript;
            }

            await base.Initialize();

            this.RemoveEventHandlers();

            if (this.Displays.Count == 0)
            {
                return;
            }

            if (this.IsDisplayEnabled(OverlayLabelDisplayV3TypeEnum.ViewerCount) || this.IsDisplayEnabled(OverlayLabelDisplayV3TypeEnum.ChatterCount) ||
                this.IsDisplayEnabled(OverlayLabelDisplayV3TypeEnum.CustomText))
            {
                if (this.refreshCancellationTokenSource != null)
                {
                    this.refreshCancellationTokenSource.Cancel();
                }
                this.refreshCancellationTokenSource = new CancellationTokenSource();

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                AsyncRunner.RunAsyncBackground(async (cancellationToken) =>
                {
                    do
                    {
                        // These are polled rather than event-driven, so they only send when the
                        // resolved text actually changed. Otherwise a display with nothing new to
                        // report would take over a NewestOnly label once a minute.
                        if (this.IsDisplayEnabled(OverlayLabelDisplayV3TypeEnum.ViewerCount))
                        {
                            this.Displays[OverlayLabelDisplayV3TypeEnum.ViewerCount].Amount = ServiceManager.Get<ChatService>().GetViewerCount();
                            await this.SendUpdate(OverlayLabelDisplayV3TypeEnum.ViewerCount, skipIfUnchanged: true);
                        }

                        if (this.IsDisplayEnabled(OverlayLabelDisplayV3TypeEnum.ChatterCount))
                        {
                            this.Displays[OverlayLabelDisplayV3TypeEnum.ChatterCount].Amount = ServiceManager.Get<UserService>().ActiveUserCount;
                            await this.SendUpdate(OverlayLabelDisplayV3TypeEnum.ChatterCount, skipIfUnchanged: true);
                        }

                        if (this.IsDisplayEnabled(OverlayLabelDisplayV3TypeEnum.CustomText))
                        {
                            await this.SendUpdate(OverlayLabelDisplayV3TypeEnum.CustomText, skipIfUnchanged: true);
                        }

                        await Task.Delay(60000);

                    } while (!cancellationToken.IsCancellationRequested);

                }, this.refreshCancellationTokenSource.Token);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            }

            if (this.IsDisplayEnabled(OverlayLabelDisplayV3TypeEnum.LatestFollower) || this.IsDisplayEnabled(OverlayLabelDisplayV3TypeEnum.TotalFollowers))
            {
                EventService.OnFollowOccurred += EventService_OnFollowOccurred;

                if (this.IsDisplayEnabled(OverlayLabelDisplayV3TypeEnum.LatestFollower))
                {
                    UserV2ViewModel user = null;
                     if (ChannelSession.Settings.DefaultStreamingPlatform == StreamingPlatformTypeEnum.Twitch && ServiceManager.Get<TwitchSession>().IsConnected)
                    {
                        var followers = await ServiceManager.Get<TwitchSession>().StreamerService.GetNewAPIFollowers(ServiceManager.Get<TwitchSession>().StreamerModel, maxResults: 1);
                        if (followers != null && followers.Count() > 0)
                        {
                            user = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Twitch, platformID: followers.First().user_id, performPlatformSearch: true);
                        }
                    }
                    if (user == null && ChannelSession.Settings.LastFollowerUserID != Guid.Empty)
                    {
                        user = await ServiceManager.Get<UserService>().GetUserByID(ChannelSession.Settings.DefaultStreamingPlatform, ChannelSession.Settings.LastFollowerUserID);
                    }

                    if (user != null)
                    {
                        this.Displays[OverlayLabelDisplayV3TypeEnum.LatestFollower].UserID = user.ID;
                    }
                }

                if (this.IsDisplayEnabled(OverlayLabelDisplayV3TypeEnum.TotalFollowers))
                {
                    double amount = this.Displays[OverlayLabelDisplayV3TypeEnum.TotalFollowers].Amount;
                    if (ChannelSession.Settings.DefaultStreamingPlatform == StreamingPlatformTypeEnum.Twitch && ServiceManager.Get<TwitchSession>().IsConnected)
                    {
                        amount = await ServiceManager.Get<TwitchSession>().StreamerService.GetFollowerCount(ServiceManager.Get<TwitchSession>().StreamerModel);
                    }
                    else if (ChannelSession.Settings.DefaultStreamingPlatform == StreamingPlatformTypeEnum.YouTube && ServiceManager.Get<MixItUp.Base.Services.YouTube.New.YouTubeSession>().IsConnected)
                    {
                        var channel = await ServiceManager.Get<MixItUp.Base.Services.YouTube.New.YouTubeSession>().StreamerService.GetCurrentChannel();
                        if (channel?.Statistics?.SubscriberCount != null)
                        {
                            amount = channel.Statistics.SubscriberCount.Value;
                        }
                    }
                    else if (ChannelSession.Settings.DefaultStreamingPlatform == StreamingPlatformTypeEnum.Velora && ServiceManager.Get<MixItUp.Base.Services.Velora.New.VeloraSession>().IsConnected)
                    {
                        var user = await ServiceManager.Get<MixItUp.Base.Services.Velora.New.VeloraSession>().StreamerService.GetCurrentUser();
                        if (user?.FollowerCount != null) { amount = user.FollowerCount.Value; }
                    }
                    else if (ChannelSession.Settings.DefaultStreamingPlatform == StreamingPlatformTypeEnum.VPZone && ServiceManager.Get<MixItUp.Base.Services.VPZone.New.VPZoneSession>().IsConnected)
                    {
                        amount = ServiceManager.Get<MixItUp.Base.Services.VPZone.New.VPZoneSession>().FollowerCount;
                    }
                    this.Displays[OverlayLabelDisplayV3TypeEnum.TotalFollowers].Amount = amount;
                }
            }

            if (this.IsDisplayEnabled(OverlayLabelDisplayV3TypeEnum.LatestRaid))
            {
                EventService.OnRaidOccurred += EventService_OnRaidOccurred;
            }

            if (this.IsDisplayEnabled(OverlayLabelDisplayV3TypeEnum.LatestSubscriber) || this.IsDisplayEnabled(OverlayLabelDisplayV3TypeEnum.TotalSubscribers) || this.IsDisplayEnabled(OverlayLabelDisplayV3TypeEnum.LatestSubscriptionGifter))
            {
                EventService.OnSubscribeOccurred += EventService_OnSubscribeOccurred;
                EventService.OnResubscribeOccurred += EventService_OnResubscribeOccurred;
                EventService.OnSubscriptionGiftedOccurred += EventService_OnSubscriptionGiftedOccurred;
                EventService.OnMassSubscriptionsGiftedOccurred += EventService_OnMassSubscriptionsGiftedOccurred;

                if (this.IsDisplayEnabled(OverlayLabelDisplayV3TypeEnum.LatestSubscriber) && this.Displays[OverlayLabelDisplayV3TypeEnum.LatestSubscriber].UserID == Guid.Empty)
                {
                    UserV2ViewModel user = null;
                    if (ChannelSession.Settings.LastSubscriberUserID != Guid.Empty)
                    {
                        user = await ServiceManager.Get<UserService>().GetUserByID(ChannelSession.Settings.DefaultStreamingPlatform, ChannelSession.Settings.LastSubscriberUserID);
                    }

                    if (user == null)
                    {
                        if (ChannelSession.Settings.DefaultStreamingPlatform == StreamingPlatformTypeEnum.Twitch && ServiceManager.Get<TwitchSession>().IsConnected)
                        {
                            var subscribers = await ServiceManager.Get<TwitchSession>().StreamerService.GetSubscribers(ServiceManager.Get<TwitchSession>().StreamerModel, maxResults: 1);
                            if (subscribers != null && subscribers.Count() > 0)
                            {
                                user = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Twitch, platformID: subscribers.First().user_id, performPlatformSearch: true);
                            }
                        }
                    }

                    if (user != null)
                    {
                        this.Displays[OverlayLabelDisplayV3TypeEnum.LatestSubscriber].UserID = user.ID;
                        this.Displays[OverlayLabelDisplayV3TypeEnum.LatestSubscriber].AmountText = " ";
                    }
                }

                if (this.IsDisplayEnabled(OverlayLabelDisplayV3TypeEnum.TotalSubscribers))
                {
                    long amount = 0;
                    if (ChannelSession.Settings.DefaultStreamingPlatform == StreamingPlatformTypeEnum.Twitch && ServiceManager.Get<TwitchSession>().IsConnected)
                    {
                        amount = await ServiceManager.Get<TwitchSession>().StreamerService.GetSubscriberCount(ServiceManager.Get<TwitchSession>().StreamerModel);
                    }
                    else if (ChannelSession.Settings.DefaultStreamingPlatform == StreamingPlatformTypeEnum.Kick && ServiceManager.Get<KickSession>().IsConnected)
                    {
                        if (ServiceManager.Get<KickSession>().Channel != null)
                        {
                            amount = ServiceManager.Get<KickSession>().Channel.ActiveSubscribersCount;
                        }
                    }
                    else if (ChannelSession.Settings.DefaultStreamingPlatform == StreamingPlatformTypeEnum.Velora && ServiceManager.Get<MixItUp.Base.Services.Velora.New.VeloraSession>().IsConnected)
                    {
                        amount = ServiceManager.Get<MixItUp.Base.Services.Velora.New.VeloraSession>().SubscriberCount;
                    }
                    else if (ChannelSession.Settings.DefaultStreamingPlatform == StreamingPlatformTypeEnum.VPZone && ServiceManager.Get<MixItUp.Base.Services.VPZone.New.VPZoneSession>().IsConnected)
                    {
                        amount = ServiceManager.Get<MixItUp.Base.Services.VPZone.New.VPZoneSession>().SubscriberCount;
                    }
                    this.Displays[OverlayLabelDisplayV3TypeEnum.TotalSubscribers].Amount = amount;
                }
            }

            if (this.IsDisplayEnabled(OverlayLabelDisplayV3TypeEnum.LatestDonation))
            {
                EventService.OnDonationOccurred += EventService_OnDonationOccurred;
            }

            if (this.IsDisplayEnabled(OverlayLabelDisplayV3TypeEnum.LatestTwitchBits))
            {
                EventService.OnTwitchBitsCheeredOccurred += EventService_OnTwitchBitsCheeredOccurred;
            }

            if (this.IsDisplayEnabled(OverlayLabelDisplayV3TypeEnum.LatestYouTubeSuperChat))
            {
                EventService.OnYouTubeSuperChatOccurred += EventService_OnYouTubeSuperChatOccurred;
            }

            if (this.IsDisplayEnabled(OverlayLabelDisplayV3TypeEnum.LatestKicksGifted))
            {
                EventService.OnKickKicksGiftedOccurred += EventService_OnKickKicksGiftedOccurred;
            }

            if (this.IsDisplayEnabled(OverlayLabelDisplayV3TypeEnum.LatestVeloraCheered))
            {
                EventService.OnVeloraChannelCheeredOccurred += EventService_OnVeloraChannelCheeredOccurred;
            }

            if (this.IsDisplayEnabled(OverlayLabelDisplayV3TypeEnum.LatestVPZoneCheered))
            {
                EventService.OnVPZoneChannelCheeredOccurred += EventService_OnVPZoneChannelCheeredOccurred;
            }

            if (this.IsDisplayEnabled(OverlayLabelDisplayV3TypeEnum.Counter))
            {
                CounterModel.OnCounterUpdated += CounterModel_OnCounterUpdated;
                if (!string.IsNullOrEmpty(this.Displays[OverlayLabelDisplayV3TypeEnum.Counter].CounterName) &&
                    ChannelSession.Settings.Counters.TryGetValue(this.Displays[OverlayLabelDisplayV3TypeEnum.Counter].CounterName, out CounterModel counter))
                {
                    this.Displays[OverlayLabelDisplayV3TypeEnum.Counter].Amount = counter.Amount;
                }
            }

            if (this.IsDisplayEnabled(OverlayLabelDisplayV3TypeEnum.File))
            {
                if (this.fileSystemWatcher != null)
                {
                    this.fileSystemWatcher.Changed -= FileSystemWatcher_Changed;
                    this.fileSystemWatcher.Dispose();
                    this.fileSystemWatcher = null;
                }

                string filePath = this.Displays[OverlayLabelDisplayV3TypeEnum.File].FilePath;
                if (ServiceManager.Get<IFileService>().FileExists(filePath))
                {
                    this.Displays[OverlayLabelDisplayV3TypeEnum.File].Format = await ServiceManager.Get<IFileService>().ReadFile(filePath);

                    this.fileSystemWatcher = new FileSystemWatcher(Path.GetDirectoryName(filePath));
                    this.fileSystemWatcher.Filter = Path.GetFileName(filePath);
                    this.fileSystemWatcher.Changed += FileSystemWatcher_Changed;
                    this.fileSystemWatcher.EnableRaisingEvents = true;
                }
            }
        }

        public override async Task Uninitialize()
        {
            await base.Uninitialize();

            this.lastSentFormats.Clear();

            this.RemoveEventHandlers();

            if (this.fileSystemWatcher != null)
            {
                this.fileSystemWatcher.Changed -= FileSystemWatcher_Changed;
                this.fileSystemWatcher.Dispose();
                this.fileSystemWatcher = null;
            }
        }

        public override Dictionary<string, object> GetGenerationProperties()
        {
            Dictionary<string, object> properties = base.GetGenerationProperties();
            properties[nameof(this.DisplaySetting)] = this.DisplaySetting.ToString();
            properties[nameof(this.DisplayRotationSeconds)] = this.DisplayRotationSeconds;

            this.DisplayEntranceAnimation.AddAnimationProperties(properties, nameof(this.DisplayEntranceAnimation));
            this.DisplayExitAnimation.AddAnimationProperties(properties, nameof(this.DisplayExitAnimation));

            return properties;
        }

        protected override async Task Loaded()
        {
            await base.Loaded();

            foreach (var display in this.Displays)
            {
                if (display.Value.IsEnabled)
                {
                    try
                    {
                        Dictionary<string, object> data = await this.GetLabelDisplayProperties(display.Value);
                        await this.CallFunction("add", data);

                        this.lastSentFormats[display.Key] = data[nameof(display.Value.Format)] as string;
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(ex);
                    }
                }
            }
        }

        private async void EventService_OnFollowOccurred(object sender, UserV2ViewModel user)
        {
            if (this.IsDisplayEnabled(OverlayLabelDisplayV3TypeEnum.LatestFollower))
            {
                this.Displays[OverlayLabelDisplayV3TypeEnum.LatestFollower].UserID = user.ID;
                await this.SendUpdate(OverlayLabelDisplayV3TypeEnum.LatestFollower);
            }

            if (this.IsDisplayEnabled(OverlayLabelDisplayV3TypeEnum.TotalFollowers) && user.Platform == ChannelSession.Settings.DefaultStreamingPlatform)
            {
                this.Displays[OverlayLabelDisplayV3TypeEnum.TotalFollowers].Amount++;
                await this.SendUpdate(OverlayLabelDisplayV3TypeEnum.TotalFollowers);
            }
        }

        private async void EventService_OnRaidOccurred(object sender, Tuple<UserV2ViewModel, int> raid)
        {
            this.Displays[OverlayLabelDisplayV3TypeEnum.LatestRaid].UserID = raid.Item1.ID;
            this.Displays[OverlayLabelDisplayV3TypeEnum.LatestRaid].Amount = raid.Item2;
            await this.SendUpdate(OverlayLabelDisplayV3TypeEnum.LatestRaid);
        }

        private async void EventService_OnSubscribeOccurred(object sender, SubscriptionDetailsModel subscription)
        {
            if (this.IsDisplayEnabled(OverlayLabelDisplayV3TypeEnum.LatestSubscriber))
            {
                this.Displays[OverlayLabelDisplayV3TypeEnum.LatestSubscriber].UserID = subscription.User.ID;
                this.Displays[OverlayLabelDisplayV3TypeEnum.LatestSubscriber].Amount = 1;
                await this.SendUpdate(OverlayLabelDisplayV3TypeEnum.LatestSubscriber);
            }

            if (this.IsDisplayEnabled(OverlayLabelDisplayV3TypeEnum.TotalSubscribers))
            {
                this.Displays[OverlayLabelDisplayV3TypeEnum.TotalSubscribers].Amount++;
                await this.SendUpdate(OverlayLabelDisplayV3TypeEnum.TotalSubscribers);
            }
        }

        private async void EventService_OnResubscribeOccurred(object sender, SubscriptionDetailsModel subscription)
        {
            if (this.IsDisplayEnabled(OverlayLabelDisplayV3TypeEnum.LatestSubscriber))
            {
                this.Displays[OverlayLabelDisplayV3TypeEnum.LatestSubscriber].UserID = subscription.User.ID;
                this.Displays[OverlayLabelDisplayV3TypeEnum.LatestSubscriber].Amount = subscription.Months;
                await this.SendUpdate(OverlayLabelDisplayV3TypeEnum.LatestSubscriber);
            }

            if (this.IsDisplayEnabled(OverlayLabelDisplayV3TypeEnum.TotalSubscribers))
            {
                this.Displays[OverlayLabelDisplayV3TypeEnum.TotalSubscribers].Amount++;
                await this.SendUpdate(OverlayLabelDisplayV3TypeEnum.TotalSubscribers);
            }
        }

        private async void EventService_OnSubscriptionGiftedOccurred(object sender, SubscriptionDetailsModel subscription)
        {
            if (this.IsDisplayEnabled(OverlayLabelDisplayV3TypeEnum.LatestSubscriber))
            {
                this.Displays[OverlayLabelDisplayV3TypeEnum.LatestSubscriber].UserID = subscription.User.ID;
                this.Displays[OverlayLabelDisplayV3TypeEnum.LatestSubscriber].Amount = 1;
                await this.SendUpdate(OverlayLabelDisplayV3TypeEnum.LatestSubscriber);
            }

            if (this.IsDisplayEnabled(OverlayLabelDisplayV3TypeEnum.TotalSubscribers))
            {
                this.Displays[OverlayLabelDisplayV3TypeEnum.TotalSubscribers].Amount++;
                await this.SendUpdate(OverlayLabelDisplayV3TypeEnum.TotalSubscribers);
            }

            if (this.IsDisplayEnabled(OverlayLabelDisplayV3TypeEnum.LatestSubscriptionGifter) && subscription.Gifter != null)
            {
                this.Displays[OverlayLabelDisplayV3TypeEnum.LatestSubscriptionGifter].ResetData();
                if (subscription.Gifter.IsUnassociated)
                {
                    this.Displays[OverlayLabelDisplayV3TypeEnum.LatestSubscriptionGifter].UserFallback = subscription.Gifter.Model;
                }
                else
                {
                    this.Displays[OverlayLabelDisplayV3TypeEnum.LatestSubscriptionGifter].UserID = subscription.Gifter.ID;
                }

                this.Displays[OverlayLabelDisplayV3TypeEnum.LatestSubscriptionGifter].Amount = 1;
                await this.SendUpdate(OverlayLabelDisplayV3TypeEnum.LatestSubscriptionGifter);
            }
        }

        private async void EventService_OnMassSubscriptionsGiftedOccurred(object sender, IEnumerable<SubscriptionDetailsModel> subscriptions)
        {
            if (subscriptions != null && subscriptions.Count() > 0)
            {
                if (this.IsDisplayEnabled(OverlayLabelDisplayV3TypeEnum.LatestSubscriber))
                {
                    this.Displays[OverlayLabelDisplayV3TypeEnum.LatestSubscriber].UserID = subscriptions.Last().User.ID;
                    this.Displays[OverlayLabelDisplayV3TypeEnum.LatestSubscriber].Amount = 1;
                    await this.SendUpdate(OverlayLabelDisplayV3TypeEnum.LatestSubscriber);
                }

                if (this.IsDisplayEnabled(OverlayLabelDisplayV3TypeEnum.TotalSubscribers))
                {
                    this.Displays[OverlayLabelDisplayV3TypeEnum.TotalSubscribers].Amount += subscriptions.Count();
                    await this.SendUpdate(OverlayLabelDisplayV3TypeEnum.TotalSubscribers);
                }

                UserV2ViewModel gifter = subscriptions.Last().Gifter;
                if (this.IsDisplayEnabled(OverlayLabelDisplayV3TypeEnum.LatestSubscriptionGifter) && gifter != null)
                {
                    this.Displays[OverlayLabelDisplayV3TypeEnum.LatestSubscriptionGifter].ResetData();
                    if (gifter.IsUnassociated)
                    {
                        this.Displays[OverlayLabelDisplayV3TypeEnum.LatestSubscriptionGifter].UserFallback = gifter.Model;
                    }
                    else
                    {
                        this.Displays[OverlayLabelDisplayV3TypeEnum.LatestSubscriptionGifter].UserID = gifter.ID;
                    }

                    this.Displays[OverlayLabelDisplayV3TypeEnum.LatestSubscriptionGifter].Amount = subscriptions.Count();
                    await this.SendUpdate(OverlayLabelDisplayV3TypeEnum.LatestSubscriptionGifter);
                }
            }
        }

        private async void EventService_OnDonationOccurred(object sender, UserDonationModel donation)
        {
            this.Displays[OverlayLabelDisplayV3TypeEnum.LatestDonation].ResetData();

            if (donation.IsAnonymous || donation.User.IsUnassociated)
            {
                this.Displays[OverlayLabelDisplayV3TypeEnum.LatestDonation].UserFallback = donation.User.Model;
            }
            else
            {
                this.Displays[OverlayLabelDisplayV3TypeEnum.LatestDonation].UserID = donation.User.ID;
            }

            this.Displays[OverlayLabelDisplayV3TypeEnum.LatestDonation].Amount = donation.Amount;
            await this.SendUpdate(OverlayLabelDisplayV3TypeEnum.LatestDonation);
        }

        private async void EventService_OnTwitchBitsCheeredOccurred(object sender, TwitchBitsCheeredEventModel bitsCheered)
        {
            this.Displays[OverlayLabelDisplayV3TypeEnum.LatestTwitchBits].UserID = bitsCheered.User.ID;
            this.Displays[OverlayLabelDisplayV3TypeEnum.LatestTwitchBits].Amount = bitsCheered.Amount;
            await this.SendUpdate(OverlayLabelDisplayV3TypeEnum.LatestTwitchBits);
        }

        private async void EventService_OnYouTubeSuperChatOccurred(object sender, YouTubeSuperChatViewModel superChat)
        {
            this.Displays[OverlayLabelDisplayV3TypeEnum.LatestYouTubeSuperChat].UserID = superChat.User.ID;
            this.Displays[OverlayLabelDisplayV3TypeEnum.LatestYouTubeSuperChat].AmountText = superChat.AmountDisplay;
            await this.SendUpdate(OverlayLabelDisplayV3TypeEnum.LatestYouTubeSuperChat);
        }

        private async void EventService_OnKickKicksGiftedOccurred(object sender, KickKicksGiftedEventModel kicksGifted)
        {
            this.Displays[OverlayLabelDisplayV3TypeEnum.LatestKicksGifted].UserID = kicksGifted.User.ID;
            this.Displays[OverlayLabelDisplayV3TypeEnum.LatestKicksGifted].Amount = kicksGifted.Amount;
            await this.SendUpdate(OverlayLabelDisplayV3TypeEnum.LatestKicksGifted);
        }

        private async void EventService_OnVeloraChannelCheeredOccurred(object sender, VeloraCheeredEventModel cheered)
        {
            this.Displays[OverlayLabelDisplayV3TypeEnum.LatestVeloraCheered].UserID = cheered.User.ID;
            this.Displays[OverlayLabelDisplayV3TypeEnum.LatestVeloraCheered].Amount = cheered.Amount;
            await this.SendUpdate(OverlayLabelDisplayV3TypeEnum.LatestVeloraCheered);
        }

        private async void EventService_OnVPZoneChannelCheeredOccurred(object sender, VPZoneCheeredEventModel cheered)
        {
            this.Displays[OverlayLabelDisplayV3TypeEnum.LatestVPZoneCheered].UserID = cheered.User.ID;
            this.Displays[OverlayLabelDisplayV3TypeEnum.LatestVPZoneCheered].Amount = cheered.Amount;
            await this.SendUpdate(OverlayLabelDisplayV3TypeEnum.LatestVPZoneCheered);
        }

        private async void CounterModel_OnCounterUpdated(object sender, CounterModel counter)
        {
            if (string.Equals(counter.Name, this.Displays[OverlayLabelDisplayV3TypeEnum.Counter].CounterName, StringComparison.OrdinalIgnoreCase))
            {
                this.Displays[OverlayLabelDisplayV3TypeEnum.Counter].Amount = counter.Amount;
                await this.SendUpdate(OverlayLabelDisplayV3TypeEnum.Counter);
            }
        }

        private async void FileSystemWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Changed && this.IsDisplayEnabled(OverlayLabelDisplayV3TypeEnum.File))
            {
                // Attempt to read from the file up to 5 times with a 1 second delay in-between each attempt in the event there's a file lock
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        this.Displays[OverlayLabelDisplayV3TypeEnum.File].Format = await ServiceManager.Get<IFileService>().ReadFile(e.FullPath);
                        await this.SendUpdate(OverlayLabelDisplayV3TypeEnum.File);
                        return;
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(ex);
                    }

                    await Task.Delay(1000);
                }
            }
        }

        private async Task SendUpdate(OverlayLabelDisplayV3TypeEnum type, bool skipIfUnchanged = false)
        {
            OverlayLabelDisplayV3Model display = this.Displays[type];
            if (display.IsEnabled)
            {
                try
                {
                    Dictionary<string, object> data = await this.GetLabelDisplayProperties(display);

                    string format = data[nameof(display.Format)] as string;
                    if (skipIfUnchanged && this.lastSentFormats.TryGetValue(type, out string previous) &&
                        string.Equals(previous, format, StringComparison.Ordinal))
                    {
                        return;
                    }
                    this.lastSentFormats[type] = format;

                    await this.CallFunction("update", data);
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
            }
        }

        private async Task<Dictionary<string, object>> GetLabelDisplayProperties(OverlayLabelDisplayV3Model display)
        {
            UserV2ViewModel user = null;
            if (display.UserFallback != null)
            {
                user = new UserV2ViewModel(display.UserFallback);
            }
            else if (display.UserID != Guid.Empty)
            {
                user = await ServiceManager.Get<UserService>().GetUserByID(StreamingPlatformTypeEnum.All, display.UserID);
            }

            if (user == null)
            {
                user = ChannelSession.User;
            }

            string amount = display.Amount.ToString();
            if (!string.IsNullOrEmpty(display.AmountText))
            {
                amount = display.AmountText;
            }

            Dictionary<string, object> replacements = new Dictionary<string, object>();
            if (user != null)
            {
                replacements[OverlayLabelV3Model.UsernamePropertyName] = user.DisplayName;
            }
            if (!string.IsNullOrEmpty(amount))
            {
                replacements[OverlayLabelV3Model.AmountPropertyName] = amount;
            }
            replacements[OverlayLabelV3Model.TypeNamePropertyName] = EnumLocalizationHelper.GetLocalizedName(display.Type);

            string result = await OverlayHTMLHelper.ProcessSpecialIdentifiers(display.Format, new CommandParametersModel(user), properties: replacements);

            Dictionary<string, object> data = new Dictionary<string, object>();
            data[nameof(display.Type)] = display.Type.ToString();
            data[OverlayLabelV3Model.TypeNamePropertyName] = EnumLocalizationHelper.GetLocalizedName(display.Type);
            data[nameof(display.Format)] = result;
            data["User"] = user;

            if (OverlayLabelV3Model.IsDateTimeDisplay(display.Type))
            {
                // The format is handed to the widget unresolved so that the browser can
                // re-render it every second, rather than pushing an update per tick.
                data[OverlayLabelV3Model.DateTimeFormatPropertyName] = result;
                data[OverlayLabelV3Model.TimeZonePropertyName] = display.TimeZoneID ?? string.Empty;
            }

            return data;
        }

        public static bool IsDateTimeDisplay(OverlayLabelDisplayV3TypeEnum type)
        {
            return type == OverlayLabelDisplayV3TypeEnum.Date || type == OverlayLabelDisplayV3TypeEnum.Time;
        }

        private bool IsDisplayEnabled(OverlayLabelDisplayV3TypeEnum type)
        {
            return this.Displays.TryGetValue(type, out OverlayLabelDisplayV3Model display) && display.IsEnabled;
        }

        private void RemoveEventHandlers()
        {
            EventService.OnFollowOccurred -= EventService_OnFollowOccurred;
            EventService.OnRaidOccurred -= EventService_OnRaidOccurred;
            EventService.OnSubscribeOccurred -= EventService_OnSubscribeOccurred;
            EventService.OnResubscribeOccurred -= EventService_OnResubscribeOccurred;
            EventService.OnSubscriptionGiftedOccurred -= EventService_OnSubscriptionGiftedOccurred;
            EventService.OnMassSubscriptionsGiftedOccurred -= EventService_OnMassSubscriptionsGiftedOccurred;
            EventService.OnDonationOccurred -= EventService_OnDonationOccurred;
            EventService.OnTwitchBitsCheeredOccurred -= EventService_OnTwitchBitsCheeredOccurred;
            EventService.OnYouTubeSuperChatOccurred -= EventService_OnYouTubeSuperChatOccurred;
            EventService.OnKickKicksGiftedOccurred -= EventService_OnKickKicksGiftedOccurred;
            EventService.OnVeloraChannelCheeredOccurred -= EventService_OnVeloraChannelCheeredOccurred;
            EventService.OnVPZoneChannelCheeredOccurred -= EventService_OnVPZoneChannelCheeredOccurred;
            CounterModel.OnCounterUpdated -= CounterModel_OnCounterUpdated;
        }
    }
}

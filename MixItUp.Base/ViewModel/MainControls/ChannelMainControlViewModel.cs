using Google.Apis.YouTube.v3.Data;
using MixItUp.Base.Model;
using MixItUp.Base.Model.Kick.Categories;
using MixItUp.Base.Model.Kick.Common;
using MixItUp.Base.Model.Twitch.Games;
using MixItUp.Base.Model.Twitch.Streams;
using MixItUp.Base.Model.Twitch.Teams;
using MixItUp.Base.Model.Twitch.User;
using MixItUp.Base.Model.Velora.Streams;
using MixItUp.Base.Model.VPZone.Streams;
using MixItUp.Base.Services;
using MixItUp.Base.Services.Kick.New;
using MixItUp.Base.Services.Twitch.New;
using MixItUp.Base.Services.Velora.New;
using MixItUp.Base.Services.VPZone.New;
using MixItUp.Base.Services.Joystick;
using MixItUp.Base.Services.AltStream;
using MixItUp.Base.Services.Razo;
using MixItUp.Base.Services.YouTube.New;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Kick;
using MixItUp.Base.ViewModel.Twitch;
using MixItUp.Base.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MixItUp.Base.ViewModel.MainControls
{
    public class TwitchChannelControlViewModel : PlatformChannelControlViewModelBase
    {
        public enum TwitchSearchFindChannelToRaidTypeEnum
        {
            FollowedChannels,
            TeamMembers,
            SameCategory,
            SameLanguage,
            Featured,
        }

        public IEnumerable<TwitchSearchFindChannelToRaidTypeEnum> SearchFindChannelToRaidOptions { get; set; } = EnumHelper.GetEnumList<TwitchSearchFindChannelToRaidTypeEnum>();

        public TwitchSearchFindChannelToRaidTypeEnum SelectedSearchFindChannelToRaidOption
        {
            get { return this.selectedSearchFindChannelToRaidOption; }
            set
            {
                this.selectedSearchFindChannelToRaidOption = value;
                this.NotifyPropertyChanged();
            }
        }
        private TwitchSearchFindChannelToRaidTypeEnum selectedSearchFindChannelToRaidOption;

        public TwitchTagEditorViewModel TagEditor { get; set; } = new TwitchTagEditorViewModel();

        public TwitchChannelControlViewModel() { this.Platform = StreamingPlatformTypeEnum.Twitch; }

        protected override async Task OnOpenInternal()
        {
            await this.TagEditor.OnOpen();

            await this.TagEditor.LoadCurrentTags();

            await base.OnOpenInternal();
        }

        protected override async Task<Result> UpdateChannelInformation()
        {
            GameModel game = null;
            IEnumerable<GameModel> games = await ServiceManager.Get<TwitchSession>().StreamerService.GetNewAPIGamesByName(this.Category);
            if (games != null && games.Count() > 0)
            {
                game = games.FirstOrDefault(g => g.name.ToLower().Equals(this.Category));
                if (game == null)
                {
                    game = games.First();
                }
            }

            return await ServiceManager.Get<TwitchSession>().StreamerService.UpdateChannelInformation(ServiceManager.Get<TwitchSession>().StreamerModel,
                title: this.Title,
                gameID: game?.id,
                tags: this.TagEditor.CustomTags.Select(t => t.Tag));
        }

        protected override async Task RefreshChannelInformation()
        {
            await base.RefreshChannelInformation();

            if (ServiceManager.Get<TwitchSession>().Channel.tags != null)
            {
                foreach (string tag in ServiceManager.Get<TwitchSession>().Channel.tags)
                {
                    await this.TagEditor.AddCustomTag(tag);
                }
            }
        }

        protected override async Task SearchChannelsToRaid()
        {
            this.ChannelsToRaid.Clear();

            List<ChannelToRaidItemViewModel> results = new List<ChannelToRaidItemViewModel>();

            if (this.SelectedSearchFindChannelToRaidOption == TwitchSearchFindChannelToRaidTypeEnum.Featured)
            {
                foreach (StreamModel stream in await ServiceManager.Get<TwitchSession>().StreamerService.GetTopStreams(10))
                {
                    results.Add(new ChannelToRaidItemViewModel(stream));
                }
            }
            else if (this.SelectedSearchFindChannelToRaidOption == TwitchSearchFindChannelToRaidTypeEnum.SameCategory && ServiceManager.Get<TwitchSession>().StreamCategoryID != null)
            {
                IEnumerable<StreamModel> streams = await ServiceManager.Get<TwitchSession>().StreamerService.GetGameStreams(ServiceManager.Get<TwitchSession>().StreamCategoryID, 10);
                if (streams.Count() > 0)
                {
                    Dictionary<string, GameModel> games = new Dictionary<string, GameModel>();
                    foreach (GameModel game in await ServiceManager.Get<TwitchSession>().StreamerService.GetNewAPIGamesByIDs(streams.Select(s => s.game_id)))
                    {
                        games[game.id] = game;
                    }

                    foreach (StreamModel stream in streams)
                    {
                        results.Add(new ChannelToRaidItemViewModel(stream, games.ContainsKey(stream.game_id) ? games[stream.game_id] : null));
                    }
                }
            }
            else if (this.SelectedSearchFindChannelToRaidOption == TwitchSearchFindChannelToRaidTypeEnum.SameLanguage && ServiceManager.Get<TwitchSession>().Channel != null)
            {
                IEnumerable<StreamModel> streams = await ServiceManager.Get<TwitchSession>().StreamerService.GetLanguageStreams(ServiceManager.Get<TwitchSession>().Channel.broadcaster_language, 10);
                if (streams.Count() > 0)
                {
                    Dictionary<string, GameModel> games = new Dictionary<string, GameModel>();
                    foreach (GameModel game in await ServiceManager.Get<TwitchSession>().StreamerService.GetNewAPIGamesByIDs(streams.Select(s => s.game_id)))
                    {
                        games[game.id] = game;
                    }

                    foreach (StreamModel stream in streams)
                    {
                        results.Add(new ChannelToRaidItemViewModel(stream, games.ContainsKey(stream.game_id) ? games[stream.game_id] : null));
                    }
                }
            }
            else if (this.SelectedSearchFindChannelToRaidOption == TwitchSearchFindChannelToRaidTypeEnum.FollowedChannels)
            {
                foreach (StreamModel stream in await ServiceManager.Get<TwitchSession>().StreamerService.GetFollowedStreams(ServiceManager.Get<TwitchSession>().StreamerModel, 10))
                {
                    results.Add(new ChannelToRaidItemViewModel(stream));
                }
            }
            else if (this.SelectedSearchFindChannelToRaidOption == TwitchSearchFindChannelToRaidTypeEnum.TeamMembers)
            {
                List<UserModel> users = new List<UserModel>();
                foreach (TeamModel team in await ServiceManager.Get<TwitchSession>().StreamerService.GetChannelTeams(ServiceManager.Get<TwitchSession>().StreamerModel))
                {
                    TeamDetailsModel teamDetails = await ServiceManager.Get<TwitchSession>().StreamerService.GetTeam(team.id);
                    if (teamDetails != null && teamDetails.users != null)
                    {
                        foreach (TeamMemberModel user in teamDetails.users)
                        {
                            users.Add(new UserModel()
                            {
                                id = user.user_id,
                                login = user.user_login,
                                display_name = user.user_name
                            });
                        }
                    }
                }

                if (users.Count > 0)
                {
                    foreach (var userBatch in users.Batch(20))
                    {
                        foreach (StreamModel stream in await ServiceManager.Get<TwitchSession>().StreamerService.GetStreams(userBatch.Select(u => u.id)))
                        {
                            results.Add(new ChannelToRaidItemViewModel(stream));
                        }
                    }
                }
            }

            this.ChannelsToRaid.AddRange(results.Take(10));
        }
    }

    public class YouTubeChannelControlViewModel : PlatformChannelControlViewModelBase
    {
        public class LiveBroadcastViewModel : UIViewModelBase
        {
            public LiveBroadcast LiveBroadcast { get; set; }

            public ICommand OpenStreamCommand { get; set; }

            public LiveBroadcastViewModel(LiveBroadcast liveBroadcast)
            {
                this.LiveBroadcast = liveBroadcast;

                this.OpenStreamCommand = this.CreateCommand(() =>
                {
                    ServiceManager.Get<IProcessService>().LaunchLink(this.StreamURL);
                });
            }

            public string ID { get { return this.LiveBroadcast.Id; } }

            public string Title { get { return this.LiveBroadcast.Snippet.Title; } }

            public DateTimeOffset StartTime { get { return this.LiveBroadcast.Snippet.ActualStartTimeDateTimeOffset ?? this.LiveBroadcast.Snippet.ScheduledStartTimeDateTimeOffset.GetValueOrDefault(); } }

            public string StreamURL { get { return this.LiveBroadcast.GetStreamURL(); } }

            public string Display
            {
                get
                {
                    if (this.StartTime != DateTimeOffset.MinValue)
                    {
                        return $"{this.Title} - {this.StartTime.ToFriendlyDateString()}";
                    }
                    else
                    {
                        return this.Title;
                    }
                }
            }
        }

        public ObservableCollection<LiveBroadcastViewModel> ActiveBroadcasts { get; set; } = new ObservableCollection<LiveBroadcastViewModel>();

        public ObservableCollection<LiveBroadcastViewModel> UpcomingBroadcasts { get; set; } = new ObservableCollection<LiveBroadcastViewModel>();

        public LiveBroadcastViewModel SelectedUpcomingBroadcast
        {
            get { return this.selectedUpcomingBroadcast; }
            set
            {
                this.selectedUpcomingBroadcast = value;
                this.NotifyPropertyChanged();
            }
        }
        private LiveBroadcastViewModel selectedUpcomingBroadcast;

        public string Description
        {
            get { return this.description; }
            set
            {
                this.description = value;
                this.NotifyPropertyChanged();
            }
        }
        private string description;

        public ICommand AddUpcomingBroadcast { get; set; }

        public YouTubeChannelControlViewModel()
        {
            this.Platform = StreamingPlatformTypeEnum.YouTube;

            this.AddUpcomingBroadcast = this.CreateCommand(() =>
            {
                if (this.SelectedUpcomingBroadcast == null)
                {
                    return;
                }

                ServiceManager.Get<YouTubeSession>().ManualLiveBroadcasts[this.SelectedUpcomingBroadcast.ID] = this.SelectedUpcomingBroadcast.LiveBroadcast;

                this.UpcomingBroadcasts.Remove(this.SelectedUpcomingBroadcast);
                this.SelectedUpcomingBroadcast = null;

                this.RefreshActiveBroadcasts();
            });
        }

        protected override async Task OnOpenInternal()
        {
            await base.OnOpenInternal();

            this.RefreshActiveBroadcasts();

            LiveBroadcast liveBroadcast = ServiceManager.Get<YouTubeSession>().LiveBroadcasts.Values.FirstOrDefault();
            if (liveBroadcast != null)
            {
                this.Title = liveBroadcast.Snippet.Title;
                this.Description = liveBroadcast.Snippet.Description;
            }

            this.UpcomingBroadcasts.Clear();
            if (ServiceManager.Get<YouTubeSession>().IsConnected)
            {
                IEnumerable<LiveBroadcast> broadcasts = await ServiceManager.Get<YouTubeSession>().StreamerService.GetLatestBroadcasts();
                if (broadcasts != null)
                {
                    foreach (LiveBroadcast broadcast in broadcasts)
                    {
                        if (broadcast != null && !ServiceManager.Get<YouTubeSession>().LiveBroadcasts.ContainsKey(broadcast.Id))
                        {
                            this.UpcomingBroadcasts.Add(new LiveBroadcastViewModel(broadcast));
                        }
                    }
                }
            }
        }

        protected override Task RefreshChannelInformation()
        {
            this.RefreshActiveBroadcasts();

            return Task.CompletedTask;
        }

        protected override async Task<Result> UpdateChannelInformation()
        {
            return await ServiceManager.Get<YouTubeSession>().UpdateStreamTitleAndDescription(this.Title, this.Description);
        }

        protected override Task SearchChannelsToRaid()
        {
            return Task.CompletedTask;
        }

        private void RefreshActiveBroadcasts()
        {
            try
            {
                this.ActiveBroadcasts.Clear();
                foreach (LiveBroadcast broadcast in ServiceManager.Get<YouTubeSession>().LiveBroadcasts.Values.ToList())
                {
                    this.ActiveBroadcasts.Add(new LiveBroadcastViewModel(broadcast));
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }
    }

    public class KickChannelControlViewModel : PlatformChannelControlViewModelBase
    {
        public KickTagEditorViewModel TagEditor { get; set; } = new KickTagEditorViewModel();

        public KickChannelControlViewModel() { this.Platform = StreamingPlatformTypeEnum.Kick; }

        protected override async Task OnOpenInternal()
        {
            await this.TagEditor.OnOpen();

            await this.TagEditor.LoadCurrentTags();

            await base.OnOpenInternal();
        }

        protected override async Task<Result> UpdateChannelInformation()
        {
            long? categoryID = null;
            if (!string.IsNullOrWhiteSpace(this.Category))
            {
                PaginatedResponseModel<CategoryWithTagsModel> categories = await ServiceManager.Get<KickSession>().StreamerService.GetCategories(limit: 100, names: new List<string>() { this.Category });
                if (categories != null && categories.Data != null && categories.Data.Count > 0)
                {
                    CategoryWithTagsModel selectedCategory = categories.Data.FirstOrDefault(c => string.Equals(c.Name, this.Category, StringComparison.OrdinalIgnoreCase));
                    if (selectedCategory == null)
                    {
                        selectedCategory = categories.Data.First();
                    }
                    categoryID = selectedCategory.ID;
                }
            }

            return await ServiceManager.Get<KickSession>().StreamerService.UpdateChannel(
                title: this.Title,
                categoryID: categoryID,
                customTags: this.TagEditor.CustomTags.Select(t => t.Tag));
        }

        protected override async Task RefreshChannelInformation()
        {
            KickSession session = ServiceManager.Get<KickSession>();
            await session.RefreshDetails();

            if (!string.IsNullOrEmpty(session.Channel?.StreamTitle))
            {
                this.Title = session.Channel.StreamTitle;
            }
            if (!string.IsNullOrEmpty(session.Channel?.Category?.Name))
            {
                this.Category = session.Channel.Category.Name;
            }

            if (session.Channel?.Stream?.CustomTags != null)
            {
                this.TagEditor.ClearCustomTags();
                foreach (string tag in session.Channel.Stream.CustomTags)
                {
                    await this.TagEditor.AddCustomTag(tag);
                }
            }
        }

        protected override Task SearchChannelsToRaid() { return Task.CompletedTask; }
    }

    public class VeloraChannelControlViewModel : PlatformChannelControlViewModelBase
    {
        public VeloraChannelControlViewModel() { this.Platform = StreamingPlatformTypeEnum.Velora; }

        protected override async Task<Result> UpdateChannelInformation()
        {
            string categorySlug = null;
            if (!string.IsNullOrWhiteSpace(this.Category))
            {
                CategoryModel selectedCategory = await ServiceManager.Get<VeloraSession>().FindCategory(this.Category);
                if (selectedCategory == null)
                {
                    // Report the miss instead of updating the title and quietly dropping the category.
                    return new Result(MixItUp.Base.Resources.ErrorFailedToUpdateCategory);
                }
                categorySlug = selectedCategory.Slug;
            }

            return await ServiceManager.Get<VeloraSession>().StreamerService.UpdateStreamInfo(title: this.Title, categorySlug: categorySlug);
        }

        protected override Task SearchChannelsToRaid() { return Task.CompletedTask; }
    }

    public class VPZoneChannelControlViewModel : PlatformChannelControlViewModelBase
    {
        public VPZoneChannelControlViewModel() { this.Platform = StreamingPlatformTypeEnum.VPZone; }

        protected override async Task<Result> UpdateChannelInformation()
        {
            // VPZone matches the category text against its game catalog itself and keeps the raw text
            // when nothing matches, so an unrecognized name is still worth sending rather than
            // refusing the whole update.
            return await ServiceManager.Get<VPZoneSession>().StreamerService.UpdateChannel(
                ServiceManager.Get<VPZoneSession>().ChannelSlug, title: this.Title, category: this.Category);
        }

        protected override Task SearchChannelsToRaid() { return Task.CompletedTask; }
    }

    public class JoystickChannelControlViewModel : PlatformChannelControlViewModelBase
    {
        public JoystickChannelControlViewModel() { this.Platform = StreamingPlatformTypeEnum.Joystick; }
        public bool CanUpdateTitle => ServiceManager.Get<JoystickSession>().Permissions.Contains("stream:manage");
        protected override Task<Result> UpdateChannelInformation() => ServiceManager.Get<JoystickSession>().SetStreamTitle(this.Title);
        protected override Task SearchChannelsToRaid() => Task.CompletedTask;
    }

    public class AltStreamChannelControlViewModel : PlatformChannelControlViewModelBase
    {
        public AltStreamChannelControlViewModel() { this.Platform = StreamingPlatformTypeEnum.AltStream; }
        public bool CanUpdateTitle => false;
        protected override Task<Result> UpdateChannelInformation() => Task.FromResult(new Result(false));
        protected override Task SearchChannelsToRaid() => Task.CompletedTask;
    }

    public class RazoChannelControlViewModel : PlatformChannelControlViewModelBase
    {
        public RazoChannelControlViewModel() { this.Platform = StreamingPlatformTypeEnum.Razo; }
        public bool CanUpdateTitle => false;
        protected override Task<Result> UpdateChannelInformation() => Task.FromResult(new Result(false));
        protected override Task SearchChannelsToRaid() => Task.CompletedTask;
    }

    public abstract class PlatformChannelControlViewModelBase : UIViewModelBase
    {
        public class ChannelToRaidItemViewModel : UIViewModelBase
        {
            public StreamingPlatformTypeEnum Platform { get; set; }
            public string ID { get; set; }
            public string Name { get; set; }
            public long Viewers { get; set; }
            public string Category { get; set; }

            public ICommand OpenChannelCommand { get; private set; }
            public ICommand RaidChannelCommand { get; private set; }

            public ChannelToRaidItemViewModel(StreamModel stream)
                : this()
            {
                this.Platform = StreamingPlatformTypeEnum.Twitch;
                this.ID = stream.user_id;
                this.Name = stream.user_login;
                this.Viewers = stream.viewer_count;
                this.Category = stream.game_name;
            }

            public ChannelToRaidItemViewModel(StreamModel stream, GameModel game)
                : this()
            {
                this.Platform = StreamingPlatformTypeEnum.Twitch;
                this.ID = stream.user_id;
                this.Name = stream.user_name;
                this.Viewers = stream.viewer_count;
                this.Category = (game != null) ? game.name : MixItUp.Base.Resources.Unknown;
            }


            private ChannelToRaidItemViewModel()
            {
                this.OpenChannelCommand = this.CreateCommand(() =>
                {
                    ServiceManager.Get<IProcessService>().LaunchLink(this.URL);
                });

                this.RaidChannelCommand = this.CreateCommand(async () =>
                {
                    if (this.Platform == StreamingPlatformTypeEnum.Twitch)
                    {
                        UserModel targetChannel = await ServiceManager.Get<TwitchSession>().StreamerService.GetNewAPIUserByLogin(this.Name);
                        if (targetChannel != null)
                        {
                            await ServiceManager.Get<TwitchSession>().StreamerService.RaidChannel(ServiceManager.Get<TwitchSession>().StreamerModel, targetChannel);
                        }
                    }
                });
            }

            public string URL
            {
                get
                {
                    if (this.Platform == StreamingPlatformTypeEnum.Twitch)
                    {
                        return $"https://www.twitch.tv/{this.Name}";
                    }
                    return string.Empty;
                }
            }
        }

        public StreamingPlatformTypeEnum Platform { get; protected set; }

        public ObservableCollection<string> PastTitles { get; private set; } = new ObservableCollection<string>();

        public string Title
        {
            get { return this.title; }
            set
            {
                this.title = value;
                this.NotifyPropertyChanged();
            }
        }
        private string title;

        public ObservableCollection<string> PastCategories { get; private set; } = new ObservableCollection<string>();

        public string Category
        {
            get { return this.category; }
            set
            {
                this.category = value;
                this.NotifyPropertyChanged();
            }
        }
        private string category;

        public ICommand UpdateChannelInformationCommand { get; private set; }

        public ICommand SearchChannelToRaidCommand { get; private set; }

        public ObservableCollection<ChannelToRaidItemViewModel> ChannelsToRaid { get; private set; } = new ObservableCollection<ChannelToRaidItemViewModel>();

        public PlatformChannelControlViewModelBase()
        {
            this.UpdateChannelInformationCommand = this.CreateCommand(async () =>
            {
                Result result = await this.UpdateChannelInformation();
                if (!result.Success)
                {
                    StringBuilder str = new StringBuilder();
                    str.AppendLine(MixItUp.Base.Resources.FailedToUpdateChannelInformation);
                    str.AppendLine();
                    str.Append(result.ToString());
                    await DialogHelper.ShowMessage(str.ToString());
                }
                else
                {
                    this.UpdateRecentData();
                }

                await this.RefreshChannelInformation();
            });

            this.SearchChannelToRaidCommand = this.CreateCommand(async () =>
            {
                await this.SearchChannelsToRaid();
            });
        }

        protected override async Task OnOpenInternal()
        {
            this.PastTitles.AddRange(ChannelSession.Settings.RecentStreamTitles);

            this.PastCategories.AddRange(ChannelSession.Settings.RecentStreamCategories);

            await base.OnOpenInternal();
        }

        protected override async Task OnVisibleInternal()
        {
            await this.RefreshChannelInformation();

            await base.OnVisibleInternal();
        }

        protected virtual async Task RefreshChannelInformation()
        {
            StreamingPlatformSessionBase session = StreamingPlatforms.GetPlatformSession(this.Platform);

            if ((await AsyncRunner.RunAsync(session.RefreshDetails()))?.Success != true) { return; }

            this.Title = session.StreamTitle;
            this.Category = session.StreamCategoryName;
        }

        protected abstract Task<Result> UpdateChannelInformation();

        protected void UpdateRecentData()
        {
            ChannelSession.Settings.RecentStreamTitles.Remove(this.Title);
            ChannelSession.Settings.RecentStreamTitles.Insert(0, this.Title);
            while (ChannelSession.Settings.RecentStreamTitles.Count > 10)
            {
                ChannelSession.Settings.RecentStreamTitles.RemoveAt(ChannelSession.Settings.RecentStreamTitles.Count - 1);
            }

            ChannelSession.Settings.RecentStreamCategories.Remove(this.Category);
            ChannelSession.Settings.RecentStreamCategories.Insert(0, this.Category);
            while (ChannelSession.Settings.RecentStreamCategories.Count > 10)
            {
                ChannelSession.Settings.RecentStreamCategories.RemoveAt(ChannelSession.Settings.RecentStreamCategories.Count - 1);
            }

            this.PastTitles.ClearAndAddRange(ChannelSession.Settings.RecentStreamTitles);
            this.PastCategories.ClearAndAddRange(ChannelSession.Settings.RecentStreamCategories);
        }

        protected abstract Task SearchChannelsToRaid();
    }

    public class ChannelMainControlViewModel : WindowControlViewModelBase
    {
        public TwitchChannelControlViewModel Twitch { get; set; } = new TwitchChannelControlViewModel();

        public YouTubeChannelControlViewModel YouTube { get; set; } = new YouTubeChannelControlViewModel();

        public KickChannelControlViewModel Kick { get; set; } = new KickChannelControlViewModel();

        public VeloraChannelControlViewModel Velora { get; set; } = new VeloraChannelControlViewModel();

        public VPZoneChannelControlViewModel VPZone { get; set; } = new VPZoneChannelControlViewModel();
        public JoystickChannelControlViewModel Joystick { get; set; } = new JoystickChannelControlViewModel();
        public AltStreamChannelControlViewModel AltStream { get; set; } = new AltStreamChannelControlViewModel();
        public RazoChannelControlViewModel Razo { get; set; } = new RazoChannelControlViewModel();

        public bool IsTwitchConnected { get { return ServiceManager.Get<TwitchSession>().IsConnected; } }

        public bool IsYouTubeConnected { get { return ServiceManager.Get<YouTubeSession>().IsConnected; } }

        public bool IsKickConnected { get { return ServiceManager.Get<KickSession>().IsConnected; } }

        public bool IsVeloraConnected { get { return ServiceManager.Get<VeloraSession>().IsConnected; } }

        public bool IsVPZoneConnected { get { return ServiceManager.Get<VPZoneSession>().IsConnected; } }
        public bool IsJoystickConnected { get { return ServiceManager.Get<JoystickSession>().IsConnected; } }
        public bool IsAltStreamConnected { get { return ServiceManager.Get<AltStreamSession>().IsConnected; } }
        public bool IsRazoConnected { get { return ServiceManager.Get<RazoSession>().IsConnected; } }

        public ChannelMainControlViewModel(MainWindowViewModel windowViewModel) : base(windowViewModel) { }

        public async Task RefreshAccount(ChannelChatSessionBase session)
        {
            this.NotifyPropertyChanged("Is" + session.Platform + "Connected");
            if (!session.IsConnected) return;
            if (session.Platform == StreamingPlatformTypeEnum.AltStream) await this.AltStream.OnVisible();
            if (session.Platform == StreamingPlatformTypeEnum.Razo) await this.Razo.OnVisible();
        }

        protected override async Task OnOpenInternal()
        {
            if (this.IsTwitchConnected)
            {
                await this.Twitch.OnOpen();
            }

            if (this.IsYouTubeConnected)
            {
                await this.YouTube.OnOpen();
            }

            if (this.IsKickConnected)
            {
                await this.Kick.OnOpen();
            }

            if (this.IsVeloraConnected)
            {
                await this.Velora.OnOpen();
            }

            if (this.IsVPZoneConnected)
            {
                await this.VPZone.OnOpen();
            }
            if (this.IsJoystickConnected)
            {
                await this.Joystick.OnOpen();
            }
            if (this.IsAltStreamConnected)
            {
                await this.AltStream.OnOpen();
            }
            if (this.IsRazoConnected)
            {
                await this.Razo.OnOpen();
            }

            await base.OnOpenInternal();
        }

        protected override async Task OnVisibleInternal()
        {
            this.NotifyPropertyChanged(nameof(IsTwitchConnected));
            this.NotifyPropertyChanged(nameof(IsYouTubeConnected));
            this.NotifyPropertyChanged(nameof(IsKickConnected));
            this.NotifyPropertyChanged(nameof(IsVeloraConnected));
            this.NotifyPropertyChanged(nameof(IsVPZoneConnected));
            this.NotifyPropertyChanged(nameof(IsJoystickConnected));
            this.NotifyPropertyChanged(nameof(IsAltStreamConnected));
            this.NotifyPropertyChanged(nameof(IsRazoConnected));

            if (this.IsTwitchConnected)
            {
                await this.Twitch.OnVisible();
            }

            if (this.IsYouTubeConnected)
            {
                await this.YouTube.OnVisible();
            }

            if (this.IsKickConnected)
            {
                await this.Kick.OnVisible();
            }

            if (this.IsVeloraConnected)
            {
                await this.Velora.OnVisible();
            }

            if (this.IsVPZoneConnected)
            {
                await this.VPZone.OnVisible();
            }
            if (this.IsJoystickConnected)
            {
                await this.Joystick.OnVisible();
            }
            if (this.IsAltStreamConnected)
            {
                await this.AltStream.OnVisible();
            }
            if (this.IsRazoConnected)
            {
                await this.Razo.OnVisible();
            }

            await base.OnVisibleInternal();
        }
    }
}

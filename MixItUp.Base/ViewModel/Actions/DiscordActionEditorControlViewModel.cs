using MixItUp.Base.Model.Actions;
using MixItUp.Base.Services;
using MixItUp.Base.Services.External;
using MixItUp.Base.Util;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MixItUp.Base.ViewModel.Actions
{
    public class DiscordActionEditorControlViewModel : ActionEditorControlViewModelBase
    {
        public override ActionTypeEnum Type { get { return ActionTypeEnum.Discord; } }

        public IEnumerable<DiscordActionTypeEnum> ActionTypes { get { return EnumHelper.GetEnumList<DiscordActionTypeEnum>(); } }

        public DiscordActionTypeEnum SelectedActionType
        {
            get { return this.selectedActionType; }
            set
            {
                this.selectedActionType = value;
                this.NotifyPropertyChanged(nameof(this.ShowChangeChannelName));
                this.NotifyPropertyChanged();
                this.NotifyPropertyChanged("ShowSendMessageGrid");
                this.NotifyPropertyChanged("ShowMuteGrid");
                this.NotifyPropertyChanged("ShowDeafenGrid");
                this.NotifyPropertyChanged("ShowSendEmbedGrid");
            }
        }
        private DiscordActionTypeEnum selectedActionType;

        public bool ShowSendMessageGrid { get { return this.SelectedActionType == DiscordActionTypeEnum.SendMessage; } }

        public bool ShowSendEmbedGrid { get { return this.SelectedActionType == DiscordActionTypeEnum.SendEmbed; } }

        public ObservableCollection<DiscordChannel> Channels { get; set; } = new ObservableCollection<DiscordChannel>();
        public ObservableCollection<DiscordChannel> RenameChannels { get; } = new ObservableCollection<DiscordChannel>();
        public bool ShowChangeChannelName => this.SelectedActionType == DiscordActionTypeEnum.ChangeChannelName;

        public DiscordChannel SelectedChannel
        {
            get { return this.selectedChannel; }
            set
            {
                this.selectedChannel = value;
                this.NotifyPropertyChanged();
            }
        }
        private DiscordChannel selectedChannel;

        public string ChatMessage
        {
            get { return this.chatMessage; }
            set
            {
                this.chatMessage = value;
                this.NotifyPropertyChanged();
            }
        }
        private string chatMessage;

        public string UploadFilePath
        {
            get { return this.uploadFilePath; }
            set
            {
                this.uploadFilePath = value;
                this.NotifyPropertyChanged();
            }
        }
        private string uploadFilePath;

        public bool ShowMuteGrid { get { return this.SelectedActionType == DiscordActionTypeEnum.MuteSelf; } }

        public bool MuteSelf
        {
            get { return this.muteSelf; }
            set
            {
                this.muteSelf = value;
                this.NotifyPropertyChanged();
            }
        }
        private bool muteSelf;

        public bool ShowDeafenGrid { get { return this.SelectedActionType == DiscordActionTypeEnum.DeafenSelf; } }

        public bool DeafenSelf
        {
            get { return this.deafenSelf; }
            set
            {
                this.deafenSelf = value;
                this.NotifyPropertyChanged();
            }
        }
        private bool deafenSelf;

        public string EmbedTitle
        {
            get { return this.embedTitle; }
            set
            {
                this.embedTitle = value;
                this.NotifyPropertyChanged();
            }
        }
        private string embedTitle;

        public string EmbedDescription
        {
            get { return this.embedDescription; }
            set
            {
                this.embedDescription = value;
                this.NotifyPropertyChanged();
            }
        }
        private string embedDescription;

        public string EmbedColor
        {
            get { return this.embedColor; }
            set
            {
                this.embedColor = value;
                this.NotifyPropertyChanged();
            }
        }
        private string embedColor = "#5865F2";

        public string EmbedURL
        {
            get { return this.embedURL; }
            set
            {
                this.embedURL = value;
                this.NotifyPropertyChanged();
            }
        }
        private string embedURL;

        public string EmbedThumbnailURL
        {
            get { return this.embedThumbnailURL; }
            set
            {
                this.embedThumbnailURL = value;
                this.NotifyPropertyChanged();
            }
        }
        private string embedThumbnailURL;

        public string EmbedImageURL
        {
            get { return this.embedImageURL; }
            set
            {
                this.embedImageURL = value;
                this.NotifyPropertyChanged();
            }
        }
        private string embedImageURL;

        public string EmbedAuthorName
        {
            get { return this.embedAuthorName; }
            set
            {
                this.embedAuthorName = value;
                this.NotifyPropertyChanged();
            }
        }
        private string embedAuthorName;

        public string EmbedAuthorURL
        {
            get { return this.embedAuthorURL; }
            set
            {
                this.embedAuthorURL = value;
                this.NotifyPropertyChanged();
            }
        }
        private string embedAuthorURL;

        public string EmbedAuthorIconURL
        {
            get { return this.embedAuthorIconURL; }
            set
            {
                this.embedAuthorIconURL = value;
                this.NotifyPropertyChanged();
            }
        }
        private string embedAuthorIconURL;

        public string EmbedFooterText
        {
            get { return this.embedFooterText; }
            set
            {
                this.embedFooterText = value;
                this.NotifyPropertyChanged();
            }
        }
        private string embedFooterText;

        public string EmbedFooterIconURL
        {
            get { return this.embedFooterIconURL; }
            set
            {
                this.embedFooterIconURL = value;
                this.NotifyPropertyChanged();
            }
        }
        private string embedFooterIconURL;

        public bool EmbedIncludeTimestamp
        {
            get { return this.embedIncludeTimestamp; }
            set
            {
                this.embedIncludeTimestamp = value;
                this.NotifyPropertyChanged();
            }
        }
        private bool embedIncludeTimestamp;

        public ICommand RefreshChannelsCommand { get; set; }

        private string existingSelectedChannel;

        public DiscordActionEditorControlViewModel(DiscordActionModel action)
            : base(action)
        {
            this.SelectedActionType = action.ActionType;
            if (this.SelectedActionType == DiscordActionTypeEnum.SendMessage || this.ShowChangeChannelName)
            {
                this.existingSelectedChannel = action.ChannelID;
                this.ChatMessage = action.MessageText;
                this.UploadFilePath = action.FilePath;
            }
            else if (this.SelectedActionType == DiscordActionTypeEnum.MuteSelf)
            {
                this.MuteSelf = action.ShouldMuteDeafen;
            }
            else if (this.SelectedActionType == DiscordActionTypeEnum.DeafenSelf)
            {
                this.DeafenSelf = action.ShouldMuteDeafen;
            }
            else if (this.SelectedActionType == DiscordActionTypeEnum.SendEmbed)
            {
                this.existingSelectedChannel = action.ChannelID;
                this.ChatMessage = action.MessageText;
                this.UploadFilePath = action.FilePath;
                this.EmbedTitle = action.EmbedTitle;
                this.EmbedDescription = action.EmbedDescription;
                this.EmbedColor = action.EmbedColor ?? "#5865F2";
                this.EmbedURL = action.EmbedURL;
                this.EmbedThumbnailURL = action.EmbedThumbnailURL;
                this.EmbedImageURL = action.EmbedImageURL;
                this.EmbedAuthorName = action.EmbedAuthorName;
                this.EmbedAuthorURL = action.EmbedAuthorURL;
                this.EmbedAuthorIconURL = action.EmbedAuthorIconURL;
                this.EmbedFooterText = action.EmbedFooterText;
                this.EmbedFooterIconURL = action.EmbedFooterIconURL;
                this.EmbedIncludeTimestamp = action.EmbedIncludeTimestamp;
            }

            this.InitializeRefreshCommand();
        }

        public DiscordActionEditorControlViewModel() : base()
        {
            this.InitializeRefreshCommand();
        }

        private void InitializeRefreshCommand()
        {
            this.RefreshChannelsCommand = this.CreateCommand(async () =>
            {
                if (ServiceManager.Get<DiscordService>().IsConnected)
                {
                    var currentSelectedChannelID = this.SelectedChannel?.ID ?? this.existingSelectedChannel;

                    var channels = await ServiceManager.Get<DiscordService>().RefreshCachedServerChannels(ServiceManager.Get<DiscordService>().Server);

                    this.RenameChannels.Clear();
                    this.RenameChannels.AddRange(channels);
                    this.Channels.Clear();
                    this.Channels.AddRange(channels.Where(c => c.Type == DiscordChannel.DiscordChannelTypeEnum.Announcements || c.Type == DiscordChannel.DiscordChannelTypeEnum.Text));

                    if (!string.IsNullOrEmpty(currentSelectedChannelID))
                    {
                        this.SelectedChannel = (this.ShowChangeChannelName ? this.RenameChannels : this.Channels).FirstOrDefault(c => c.ID.Equals(currentSelectedChannelID));
                    }
                }
            });
        }

        public override Task<Result> Validate()
        {
            if (this.ShowChangeChannelName)
            {
                if (this.SelectedChannel == null) { return Task.FromResult(new Result(Resources.DiscordActionMissingChannel)); }
                if (string.IsNullOrWhiteSpace(this.ChatMessage) || (!SpecialIdentifierStringBuilder.ContainsSpecialIdentifiers(this.ChatMessage) && this.ChatMessage.Length > 100))
                {
                    return Task.FromResult(new Result(Resources.DiscordChannelNameInvalid));
                }
            }
            if (this.ShowSendMessageGrid)
            {
                if (this.SelectedChannel == null)
                {
                    return Task.FromResult<Result>(new Result(MixItUp.Base.Resources.DiscordActionMissingChannel));
                }

                if (string.IsNullOrEmpty(this.ChatMessage))
                {
                    return Task.FromResult<Result>(new Result(MixItUp.Base.Resources.DiscordActionMissingChatMessage));
                }
            }
            else if (this.ShowSendEmbedGrid)
            {
                if (this.SelectedChannel == null)
                {
                    return Task.FromResult<Result>(new Result(MixItUp.Base.Resources.DiscordActionMissingChannel));
                }

                if (string.IsNullOrEmpty(this.EmbedTitle) &&
                    string.IsNullOrEmpty(this.EmbedDescription) &&
                    string.IsNullOrEmpty(this.EmbedImageURL) &&
                    string.IsNullOrEmpty(this.EmbedThumbnailURL))
                {
                    return Task.FromResult<Result>(new Result(MixItUp.Base.Resources.DiscordActionMissingEmbedContent));
                }
            }

            return Task.FromResult<Result>(new Result());
        }

        protected override Task<ActionModelBase> GetActionInternal()
        {
            if (this.ShowChangeChannelName)
            {
                return Task.FromResult<ActionModelBase>(DiscordActionModel.CreateForChannelName(this.SelectedChannel, this.ChatMessage));
            }
            if (this.ShowSendMessageGrid)
            {
                return Task.FromResult<ActionModelBase>(DiscordActionModel.CreateForChatMessage(this.SelectedChannel, this.ChatMessage, this.UploadFilePath));
            }
            else if (this.ShowMuteGrid)
            {
                return Task.FromResult<ActionModelBase>(DiscordActionModel.CreateForMuteSelf(this.MuteSelf));
            }
            else if (this.ShowDeafenGrid)
            {
                return Task.FromResult<ActionModelBase>(DiscordActionModel.CreateForDeafenSelf(this.DeafenSelf));
            }
            else if (this.ShowSendEmbedGrid)
            {
                return Task.FromResult<ActionModelBase>(DiscordActionModel.CreateForEmbed(
                    this.SelectedChannel,
                    this.ChatMessage,
                    this.UploadFilePath,
                    this.EmbedTitle,
                    this.EmbedDescription,
                    this.EmbedColor,
                    this.EmbedURL,
                    this.EmbedThumbnailURL,
                    this.EmbedImageURL,
                    this.EmbedAuthorName,
                    this.EmbedAuthorURL,
                    this.EmbedAuthorIconURL,
                    this.EmbedFooterText,
                    this.EmbedFooterIconURL,
                    this.EmbedIncludeTimestamp));
            }
            return Task.FromResult<ActionModelBase>(null);
        }

        protected override async Task OnOpenInternal()
        {
            if (ServiceManager.Get<DiscordService>().IsConnected)
            {
                List<DiscordChannel> channels = new List<DiscordChannel>(await ServiceManager.Get<DiscordService>().GetCachedServerChannels(ServiceManager.Get<DiscordService>().Server));
                this.RenameChannels.AddRange(channels);
                this.Channels.AddRange(channels.Where(c => c.Type == DiscordChannel.DiscordChannelTypeEnum.Announcements || c.Type == DiscordChannel.DiscordChannelTypeEnum.Text));

                if (!string.IsNullOrEmpty(this.existingSelectedChannel))
                {
                    this.SelectedChannel = (this.ShowChangeChannelName ? this.RenameChannels : this.Channels).FirstOrDefault(c => c.ID.Equals(this.existingSelectedChannel));

                    // Fallback: if we couldn't find the channel in the list but have a saved ID, create a placeholder
                    // This prevents losing the saved channel if the API fails
                    // DISABLED FOR NOW, ENABLE LATER IF NEEDED OR IF USERS STILL HAVING ISSUES EVEN AFTER ADDING CACHING
                    //if (this.SelectedChannel == null)
                    //{
                    //    var placeholderChannel = new DiscordChannel { ID = this.existingSelectedChannel, Name = this.existingSelectedChannel };
                    //    this.Channels.Add(placeholderChannel);
                    //    this.SelectedChannel = placeholderChannel;
                    //}
                }
            }
            await base.OnOpenInternal();
        }
    }
}

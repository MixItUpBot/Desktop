using MixItUp.Base.Model;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Services;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.User;
using MixItUp.Base.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MixItUp.Base.ViewModel.Chat
{
    public class PlatformOption
    {
        public StreamingPlatformTypeEnum Platform { get; set; }
        public string Name { get; set; }
        public string LogoPath { get; set; }
    }

    public class ChatListControlViewModel : WindowControlViewModelBase
    {
        // Slash commands are parsed by ChatSlashCommandProcessor; this remains for ModerationService.
        public static readonly Regex UserNameTagRegex = new Regex(@"@\w+");

        public ThreadSafeObservableCollection<ChatMessageViewModel> Messages { get; private set; }

        public int AlternationCount { get { return (ChannelSession.Settings.UseAlternatingBackgroundColors) ? 2 : 1; } }

        public IEnumerable<string> SendAsOptions
        {
            get
            {
                List<string> results = new List<string>() { MixItUp.Base.Resources.Streamer };
                if (StreamingPlatforms.GetConnectedPlatformSessions().Any(session => session.IsBotConnected))
                {
                    results.Add(MixItUp.Base.Resources.Bot);
                }
                return results;
            }
        }

        public int SendAsIndex
        {
            get { return this.sendAsIndex; }
            set
            {
                this.sendAsIndex = value;
                this.NotifyPropertyChanged();
            }
        }
        private int sendAsIndex = 0;

        public bool SendAsStreamer { get { return (this.SendAsIndex == 0); } }

        public string SendMessageText
        {
            get { return this.sendMessageText; }
            set
            {
                this.sendMessageText = value;
                this.NotifyPropertyChanged();
            }
        }
        private string sendMessageText;

        public List<string> SentMessageHistory { get; set; } = new List<string>();
        private int SentMessageHistoryIndex = 0;

        public bool IsScrollingLocked
        {
            get { return this.isScrollingLocked; }
            set
            {
                this.isScrollingLocked = value;
                this.NotifyPropertyChanged();
                this.NotifyPropertyChanged("LockIconColor");
            }
        }
        private bool isScrollingLocked = true;

        public string LockIconColor { get { return (this.IsScrollingLocked) ? "Green" : "Red"; } }

        public IEnumerable<CommandModelBase> ContextMenuChatCommands { get { return ServiceManager.Get<ChatService>().ChatMenuCommands.ToList(); } }

        public event EventHandler MessageSentOccurred = delegate { };
        public event EventHandler ScrollingLockChanged = delegate { };
        public event EventHandler ContextMenuCommandsChanged = delegate { };

        public ICommand SendMessageCommand { get; private set; }

        public ICommand ScrollingLockCommand { get; private set; }

        public ObservableCollection<PlatformOption> PlatformOptions { get; set; } = new ObservableCollection<PlatformOption>();
        private PlatformOption selectedPlatform;
        public PlatformOption SelectedPlatform
        {
            get => selectedPlatform;
            set
            {
                selectedPlatform = value;
                this.NotifyPropertyChanged();
            }
        }

        public ChatListControlViewModel(UIViewModelBase windowViewModel)
            : base(windowViewModel)
        {
            this.UpdatePlatformOptions();

            this.SendMessageCommand = this.CreateCommand(async () =>
            {
                if (!string.IsNullOrEmpty(this.SendMessageText))
                {
                    StreamingPlatformTypeEnum platformType = SelectedPlatform?.Platform ?? StreamingPlatformTypeEnum.All;

                    // A slash command that any targeted platform implements is executed against that platform's
                    // API and swallowed, so it is never broadcast as literal chat text to the platforms that do
                    // not implement it. Anything else falls through and is sent as an ordinary chat message.
                    SlashCommandResultEnum slashCommandResult = await ChatSlashCommandProcessor.Process(this.SendMessageText, platformType, this.SendAsStreamer);
                    if (slashCommandResult == SlashCommandResultEnum.NotRecognized)
                    {
                        await ServiceManager.Get<ChatService>().SendMessage(this.SendMessageText, platformType, sendAsStreamer: this.SendAsStreamer);
                    }

                    this.SentMessageHistory.Remove(this.SendMessageText);
                    this.SentMessageHistory.Insert(0, this.SendMessageText);
                    this.SentMessageHistoryIndex = -1;

                    this.SendMessageText = string.Empty;
                    this.MessageSentOccurred(this, new EventArgs());
                }
            });

            this.ScrollingLockCommand = this.CreateCommand(() =>
            {
                this.IsScrollingLocked = !this.IsScrollingLocked;
                this.ScrollingLockChanged(this, new EventArgs());
            });

            ChatService.OnChatVisualSettingsChanged += ChatService_OnChatVisualSettingsChanged;
            ServiceManager.Get<ChatService>().ChatCommandsReprocessed += Chat_ChatCommandsReprocessed;
        }

        public void MoveSentMessageHistoryUp()
        {
            if (this.SentMessageHistory.Count > 0 && this.SentMessageHistoryIndex < (this.SentMessageHistory.Count - 1))
            {
                this.SentMessageHistoryIndex++;
                this.SendMessageText = this.SentMessageHistory[this.SentMessageHistoryIndex];
            }
        }

        public void MoveSentMessageHistoryDown()
        {
            if (this.SentMessageHistory.Count > 0 && this.SentMessageHistoryIndex >= 0)
            {
                this.SentMessageHistoryIndex--;
                if (this.SentMessageHistoryIndex < 0)
                {
                    this.SendMessageText = string.Empty;
                }
                else
                {
                    this.SendMessageText = this.SentMessageHistory[this.SentMessageHistoryIndex];
                }
            }
        }

        protected override async Task OnOpenInternal()
        {
            await base.OnOpenInternal();

            this.Messages = ServiceManager.Get<ChatService>().Messages;
        }

        protected override async Task OnVisibleInternal()
        {
            await base.OnVisibleInternal();

            this.UpdatePlatformOptions();
        }

        public void UpdatePlatformOptions()
        {
            var selected = this.SelectedPlatform?.Platform;
            PlatformOptions.Clear();

            PlatformOptions.Add(new PlatformOption
            {
                Platform = StreamingPlatformTypeEnum.All,
                Name = "All",
                LogoPath = null
            });

            foreach (StreamingPlatformTypeEnum platform in StreamingPlatforms.SupportedPlatforms)
            {
                if (StreamingPlatforms.IsPlatformConnected(platform))
                {
                    PlatformOptions.Add(new PlatformOption
                    {
                        Platform = platform,
                        Name = platform.ToString(),
                        LogoPath = StreamingPlatforms.GetPlatformSmallImage(platform)
                    });
                }
            }

            SelectedPlatform = PlatformOptions.FirstOrDefault(option => option.Platform == selected) ?? PlatformOptions.FirstOrDefault();
            this.NotifyPropertyChanged(nameof(SendAsOptions));

            this.NotifyPropertyChanged(nameof(PlatformOptions));
            this.NotifyPropertyChanged(nameof(SelectedPlatform));
        }

        private void ChatService_OnChatVisualSettingsChanged(object sender, EventArgs e)
        {
            this.NotifyPropertyChanged("AlternationCount");
            this.Messages.Clear();
        }

        private void Chat_ChatCommandsReprocessed(object sender, EventArgs e)
        {
            this.ContextMenuCommandsChanged(this, new EventArgs());
        }
    }
}

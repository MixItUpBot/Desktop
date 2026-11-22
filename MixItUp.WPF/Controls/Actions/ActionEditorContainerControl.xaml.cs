using MixItUp.Base.Model.Actions;
using MixItUp.Base.ViewModel.Actions;
using MixItUp.WPF.Util;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace MixItUp.WPF.Controls.Actions
{
    /// <summary>
    /// Interaction logic for ActionEditorContainerControl.xaml
    /// </summary>
    public partial class ActionEditorContainerControl : LoadingControlBase
    {
        public ActionEditorControlViewModelBase ViewModel { get; private set; }

        public ContentControl ContentControl { get; private set; }

        public ActionEditorControlBase ActionControl { get; private set; }

        private bool isCreatingControl = false;

        public ActionEditorContainerControl()
        {
            InitializeComponent();
            this.DataContextChanged += ActionEditorContainerControl_DataContextChanged;
        }

        protected override async Task OnLoaded()
        {
            this.ContentControl = (ContentControl)this.GetByUid("ActionContentControl");
            await this.InitializeFromDataContext();
        }

        private async void ActionEditorContainerControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            this.ViewModel = null;
            this.ReleaseContentControl();

            if (this.IsLoaded)
            {
                await this.InitializeFromDataContext();
            }
        }

        private async Task InitializeFromDataContext()
        {
            if (!this.IsLoaded || this.ContentControl == null || !(this.DataContext is ActionEditorControlViewModelBase))
            {
                return;
            }

            this.ViewModel = (ActionEditorControlViewModelBase)this.DataContext;

            if (this.ViewModel.IsMinimized)
            {
                this.ActionContainer.Minimize();
            }
            else
            {
                this.ActionContainer.Maximize();
                await this.EnsureActionControlLoaded();
            }
        }

        private async Task EnsureActionControlLoaded()
        {
            if (this.ViewModel == null || this.ContentControl == null || this.isCreatingControl)
            {
                return;
            }

            if (this.ActionControl != null && this.ContentControl.Content == this.ActionControl)
            {
                return;
            }

            this.isCreatingControl = true;
            try
            {
                this.ActionControl = this.CreateActionControl(this.ViewModel.Type);
                if (this.ActionControl != null)
                {
                    this.ContentControl.Content = this.ActionControl;
                    await this.ViewModel.EnsureEditorOpened();
                    this.ViewModel.MarkUserExpanded();
                }
            }
            finally
            {
                this.isCreatingControl = false;
            }
        }

        private ActionEditorControlBase CreateActionControl(ActionTypeEnum type)
        {
            switch (type)
            {
                case ActionTypeEnum.Chat: return new ChatActionEditorControl();
                case ActionTypeEnum.Command: return new CommandActionEditorControl();
                case ActionTypeEnum.Conditional: return new ConditionalActionEditorControl();
                case ActionTypeEnum.Consumables: return new ConsumablesActionEditorControl();
                case ActionTypeEnum.Counter: return new CounterActionEditorControl();
                case ActionTypeEnum.Discord: return new DiscordActionEditorControl();
                case ActionTypeEnum.ExternalProgram: return new ExternalProgramActionEditorControl();
                case ActionTypeEnum.File: return new FileActionEditorControl();
                case ActionTypeEnum.GameQueue: return new GameQueueActionEditorControl();
                case ActionTypeEnum.Group: return new GroupActionEditorControl();
                case ActionTypeEnum.IFTTT: return new IFTTTActionEditorControl();
                case ActionTypeEnum.Input: return new InputActionEditorControl();
                case ActionTypeEnum.Kick: return new KickActionEditorControl();
                case ActionTypeEnum.LumiaStream: return new LumiaStreamActionEditorControl();
                case ActionTypeEnum.MeldStudio: return new MeldStudioActionEditorControl();
                case ActionTypeEnum.Moderation: return new ModerationActionEditorControl();
                case ActionTypeEnum.MtionStudio: return new MtionStudioActionEditorControl();
                case ActionTypeEnum.MusicPlayer: return new MusicPlayerActionEditorControl();
                case ActionTypeEnum.Overlay: return new OverlayActionEditorControl();
                case ActionTypeEnum.PixelChat: return new PixelChatActionEditorControl();
                case ActionTypeEnum.PolyPop: return new PolyPopActionEditorControl();
                case ActionTypeEnum.RahiTuber: return new RahiTuberActionEditorControl();
                case ActionTypeEnum.Random: return new RandomActionEditorControl();
                case ActionTypeEnum.Repeat: return new RepeatActionEditorControl();
                case ActionTypeEnum.SAMMI: return new SAMMIActionEditorControl();
                case ActionTypeEnum.Script: return new ScriptActionEditorControl();
                case ActionTypeEnum.Serial: return new SerialActionEditorControl();
                case ActionTypeEnum.Sound: return new SoundActionEditorControl();
                case ActionTypeEnum.PlatformMessage: return new PlatformMessageActionEditorControl();
                case ActionTypeEnum.SpecialIdentifier: return new SpecialIdentifierActionEditorControl();
                case ActionTypeEnum.StreamingSoftware: return new StreamingSoftwareActionEditorControl();
                case ActionTypeEnum.Streamlabs: return new StreamlabsActionEditorControl();
                case ActionTypeEnum.TextToSpeech: return new TextToSpeechActionEditorControl();
                case ActionTypeEnum.TITS: return new TITSActionEditorControl();
                case ActionTypeEnum.Twitch: return new TwitchActionEditorControl();
                case ActionTypeEnum.UserLookup: return new UserLookupActionEditorControl();
                case ActionTypeEnum.VConnect: return new VConnectActionEditorControl();
                case ActionTypeEnum.Veadotube: return new VeadotubeActionEditorControl();
                case ActionTypeEnum.Velora: return new VeloraActionEditorControl();
                case ActionTypeEnum.AltStream: return new AltStreamActionEditorControl();
                case ActionTypeEnum.VPZone: return new VPZoneActionEditorControl();
                case ActionTypeEnum.Voicemod: return new VoicemodActionEditorControl();
                case ActionTypeEnum.VTSPog: return new VTSPogActionEditorControl();
                case ActionTypeEnum.VTubeStudio: return new VTubeStudioActionEditorControl();
                case ActionTypeEnum.Wait: return new WaitActionEditorControl();
                case ActionTypeEnum.WebRequest: return new WebRequestActionEditorControl();
                case ActionTypeEnum.YouTube: return new YouTubeActionEditorControl();
            }
            return null;
        }

        private async void ActionContainer_Maximized(object sender, RoutedEventArgs e)
        {
            if (this.ViewModel != null)
            {
                this.ViewModel.IsMinimized = false;
                await this.EnsureActionControlLoaded();
            }
        }

        private void ActionContainer_Minimized(object sender, RoutedEventArgs e)
        {
            if (this.ViewModel != null)
            {
                this.ViewModel.IsMinimized = true;
            }
        }

        private void ReleaseContentControl()
        {
            if (this.ContentControl != null)
            {
                this.ContentControl.Content = null;
            }
            this.ActionControl = null;
        }
    }
}

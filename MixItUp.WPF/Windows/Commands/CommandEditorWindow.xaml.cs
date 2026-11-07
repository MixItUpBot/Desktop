using MixItUp.Base.Model.Actions;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.Webhooks;
using MixItUp.Base.Services;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Commands;
using MixItUp.WPF.Controls.Commands;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;

namespace MixItUp.WPF.Windows.Commands
{
    /// <summary>
    /// Interaction logic for CommandEditorWindow.xaml
    /// </summary>
    public partial class CommandEditorWindow : CommandEditorWindowBase
    {
        private static Dictionary<Guid, CommandEditorWindow> OpenWindows = new Dictionary<Guid, CommandEditorWindow>();
        public static CommandEditorWindow GetCommandEditorWindow(CommandModelBase existingCommand, IEnumerable<ActionModelBase> actions = null)
        {
            if (OpenWindows.TryGetValue(existingCommand.ID, out var window))
            {
                return window;
            }

            window = new CommandEditorWindow(existingCommand, actions);
            OpenWindows.Add(existingCommand.ID, window);
            return window;
        }

        public CommandEditorDetailsControlBase editorDetailsControl { get; private set; }

        public CommandEditorWindowViewModelBase viewModel { get; private set; }

        public event EventHandler<CommandModelBase> CommandSaved = delegate { };

        private Guid commandId = Guid.Empty;

        private IEnumerable<ActionModelBase> importedActions = null;

        private CommandEditorWindow(CommandModelBase existingCommand, IEnumerable<ActionModelBase> actions = null)
            : this()
        {
            commandId = existingCommand.ID;

            switch (existingCommand.Type)
            {
                case CommandTypeEnum.Chat:
                    this.editorDetailsControl = new ChatCommandEditorDetailsControl();
                    this.viewModel = new ChatCommandEditorWindowViewModel((ChatCommandModel)existingCommand);
                    break;
                case CommandTypeEnum.Event:
                    this.editorDetailsControl = new EventCommandEditorDetailsControl();
                    this.viewModel = new EventCommandEditorWindowViewModel((EventCommandModel)existingCommand);
                    break;
                case CommandTypeEnum.Timer:
                    this.editorDetailsControl = new TimerCommandEditorDetailsControl();
                    this.viewModel = new TimerCommandEditorWindowViewModel((TimerCommandModel)existingCommand);
                    break;
                case CommandTypeEnum.TwitchChannelPoints:
                    this.editorDetailsControl = new TwitchChannelPointsCommandEditorDetailsControl();
                    this.viewModel = new TwitchChannelPointsCommandEditorWindowViewModel((TwitchChannelPointsCommandModel)existingCommand);
                    break;
                case CommandTypeEnum.ActionGroup:
                    this.editorDetailsControl = new ActionGroupCommandEditorDetailsControl();
                    this.viewModel = new ActionGroupCommandEditorWindowViewModel((ActionGroupCommandModel)existingCommand);
                    break;
                case CommandTypeEnum.StreamlootsCard:
                    this.editorDetailsControl = new StreamlootsCardCommandEditorDetailsControl();
                    this.viewModel = new StreamlootsCardCommandEditorWindowViewModel((StreamlootsCardCommandModel)existingCommand);
                    break;
                case CommandTypeEnum.Webhook:
                    this.editorDetailsControl = new WebhookCommandEditorDetailsControl();
                    this.viewModel = new WebhookCommandEditorWindowViewModel((WebhookCommandModel)existingCommand);
                    break;
                case CommandTypeEnum.Custom:
                    this.editorDetailsControl = new CustomCommandEditorDetailsControl();
                    this.viewModel = new CustomCommandEditorWindowViewModel((CustomCommandModel)existingCommand);
                    break;
                case CommandTypeEnum.UserOnlyChat:
                    this.editorDetailsControl = new ChatCommandEditorDetailsControl();
                    this.viewModel = new UserOnlyChatCommandEditorWindowViewModel((UserOnlyChatCommandModel)existingCommand);
                    break;
                case CommandTypeEnum.TwitchBits:
                    this.editorDetailsControl = new TwitchBitsCommandEditorDetailsControl();
                    this.viewModel = new TwitchBitsCommandEditorWindowViewModel((TwitchBitsCommandModel)existingCommand);
                    break;
                case CommandTypeEnum.CrowdControlEffect:
                    this.editorDetailsControl = new CrowdControlEffectCommandEditorDetailsControl();
                    this.viewModel = new CrowdControlEffectCommandEditorWindowViewModel((CrowdControlEffectCommandModel)existingCommand);
                    break;
                case CommandTypeEnum.TwitchCustomPowerUp:
                    this.editorDetailsControl = new TwitchCustomPowerUpCommandEditorDetailsControl();
                    this.viewModel = new TwitchCustomPowerUpCommandEditorWindowViewModel((TwitchCustomPowerUpCommandModel)existingCommand);
                    break;
                case CommandTypeEnum.KickChannelPoints:
                    this.editorDetailsControl = new KickChannelPointsCommandEditorDetailsControl();
                    this.viewModel = new KickChannelPointsCommandEditorWindowViewModel((KickChannelPointsCommandModel)existingCommand);
                    break;
                case CommandTypeEnum.KickKicks:
                    this.editorDetailsControl = new KickKicksCommandEditorDetailsControl();
                    this.viewModel = new KickKicksCommandEditorWindowViewModel((KickKicksCommandModel)existingCommand);
                    break;
                case CommandTypeEnum.VeloraChannelPoints:
                    this.editorDetailsControl = new VeloraChannelPointsCommandEditorDetailsControl();
                    this.viewModel = new VeloraChannelPointsCommandEditorWindowViewModel((VeloraChannelPointsCommandModel)existingCommand);
                    break;
                case CommandTypeEnum.VPZoneChannelPoints:
                    this.editorDetailsControl = new VPZoneChannelPointsCommandEditorDetailsControl();
                    this.viewModel = new VPZoneChannelPointsCommandEditorWindowViewModel((VPZoneChannelPointsCommandModel)existingCommand);
                    break;
            }

            this.importedActions = actions;

            this.DataContext = this.ViewModel = this.viewModel;

            this.ViewModel.StartLoadingOperationOccurred += (sender, eventArgs) => { this.StartLoadingOperation(); };
            this.ViewModel.EndLoadingOperationOccurred += (sender, eventArgs) => { this.EndLoadingOperation(); };
        }

        public CommandEditorWindow(CommandTypeEnum commandType, string name = null, IEnumerable<ActionModelBase> actions = null)
            : this()
        {
            switch (commandType)
            {
                case CommandTypeEnum.Chat:
                    this.editorDetailsControl = new ChatCommandEditorDetailsControl();
                    this.viewModel = new ChatCommandEditorWindowViewModel();
                    break;
                case CommandTypeEnum.Timer:
                    this.editorDetailsControl = new TimerCommandEditorDetailsControl();
                    this.viewModel = new TimerCommandEditorWindowViewModel();
                    break;
                case CommandTypeEnum.TwitchChannelPoints:
                    this.editorDetailsControl = new TwitchChannelPointsCommandEditorDetailsControl();
                    this.viewModel = new TwitchChannelPointsCommandEditorWindowViewModel();
                    break;
                case CommandTypeEnum.ActionGroup:
                    this.editorDetailsControl = new ActionGroupCommandEditorDetailsControl();
                    this.viewModel = new ActionGroupCommandEditorWindowViewModel();
                    break;
                case CommandTypeEnum.StreamlootsCard:
                    this.editorDetailsControl = new StreamlootsCardCommandEditorDetailsControl();
                    this.viewModel = new StreamlootsCardCommandEditorWindowViewModel();
                    break;
                case CommandTypeEnum.TwitchBits:
                    this.editorDetailsControl = new TwitchBitsCommandEditorDetailsControl();
                    this.viewModel = new TwitchBitsCommandEditorWindowViewModel();
                    break;
                case CommandTypeEnum.CrowdControlEffect:
                    this.editorDetailsControl = new CrowdControlEffectCommandEditorDetailsControl();
                    this.viewModel = new CrowdControlEffectCommandEditorWindowViewModel();
                    break;
                case CommandTypeEnum.TwitchCustomPowerUp:
                    this.editorDetailsControl = new TwitchCustomPowerUpCommandEditorDetailsControl();
                    this.viewModel = new TwitchCustomPowerUpCommandEditorWindowViewModel();
                    break;
                case CommandTypeEnum.KickChannelPoints:
                    this.editorDetailsControl = new KickChannelPointsCommandEditorDetailsControl();
                    this.viewModel = new KickChannelPointsCommandEditorWindowViewModel();
                    break;
                case CommandTypeEnum.KickKicks:
                    this.editorDetailsControl = new KickKicksCommandEditorDetailsControl();
                    this.viewModel = new KickKicksCommandEditorWindowViewModel();
                    break;
                case CommandTypeEnum.VeloraChannelPoints:
                    this.editorDetailsControl = new VeloraChannelPointsCommandEditorDetailsControl();
                    this.viewModel = new VeloraChannelPointsCommandEditorWindowViewModel();
                    break;
                case CommandTypeEnum.VPZoneChannelPoints:
                    this.editorDetailsControl = new VPZoneChannelPointsCommandEditorDetailsControl();
                    this.viewModel = new VPZoneChannelPointsCommandEditorWindowViewModel();
                    break;
                case CommandTypeEnum.Custom:
                    this.editorDetailsControl = new CustomCommandEditorDetailsControl();
                    this.viewModel = new CustomCommandEditorWindowViewModel();
                    break;
            }

            if (!string.IsNullOrEmpty(name))
            {
                this.viewModel.Name = name;
            }
            this.importedActions = actions;

            this.DataContext = this.ViewModel = this.viewModel;

            this.ViewModel.StartLoadingOperationOccurred += (sender, eventArgs) => { this.StartLoadingOperation(); };
            this.ViewModel.EndLoadingOperationOccurred += (sender, eventArgs) => { this.EndLoadingOperation(); };
        }

        public CommandEditorWindow(CommandTypeEnum commandType, Guid id)
            : this()
        {
            switch (commandType)
            {
                case CommandTypeEnum.UserOnlyChat:
                    this.editorDetailsControl = new ChatCommandEditorDetailsControl();
                    this.viewModel = new UserOnlyChatCommandEditorWindowViewModel(id);
                    break;
            }
            this.DataContext = this.ViewModel = this.viewModel;

            this.ViewModel.StartLoadingOperationOccurred += (sender, eventArgs) => { this.StartLoadingOperation(); };
            this.ViewModel.EndLoadingOperationOccurred += (sender, eventArgs) => { this.EndLoadingOperation(); };
        }

        public CommandEditorWindow(Webhook webhook)
            : this()
        {
            this.editorDetailsControl = new WebhookCommandEditorDetailsControl();
            this.DataContext = this.ViewModel = this.viewModel = new WebhookCommandEditorWindowViewModel(webhook);

            this.ViewModel.StartLoadingOperationOccurred += (sender, eventArgs) => { this.StartLoadingOperation(); };
            this.ViewModel.EndLoadingOperationOccurred += (sender, eventArgs) => { this.EndLoadingOperation(); };
        }

        public CommandEditorWindow(EventTypeEnum eventType)
            : this()
        {
            this.editorDetailsControl = new EventCommandEditorDetailsControl();
            this.DataContext = this.ViewModel = this.viewModel = new EventCommandEditorWindowViewModel(eventType);

            this.ViewModel.StartLoadingOperationOccurred += (sender, eventArgs) => { this.StartLoadingOperation(); };
            this.ViewModel.EndLoadingOperationOccurred += (sender, eventArgs) => { this.EndLoadingOperation(); };
        }

        private CommandEditorWindow()
            : base()
        {
            InitializeComponent();

            this.Initialize(this.StatusBar);
        }

        protected override async Task OnLoaded()
        {
            this.viewModel.CommandSaved += ViewModel_CommandSaved;
            await this.viewModel.OnOpen();

            if (this.importedActions != null)
            {
                await this.viewModel.AddActions(this.importedActions);
            }

            this.DetailsContentControl.Content = this.editorDetailsControl;

            await base.OnLoaded();
        }

        protected override void OnClosed(EventArgs e)
        {
            OpenWindows.Remove(this.commandId);
            base.OnClosed(e);
        }

        private void ViewModel_CommandSaved(object sender, CommandModelBase command)
        {
            this.AllowClose = true;
            this.CommandSaved(this, command);
            this.Close();
        }

        public void ForceShow()
        {
            if (!this.IsVisible)
            {
                this.Show();
            }

            if (this.WindowState == System.Windows.WindowState.Minimized)
            {
                this.WindowState = System.Windows.WindowState.Normal;
            }

            this.Activate();
            this.Topmost = true;  // important
            this.Topmost = false; // important
            this.Focus();         // important
        }

        private void CommandEditorWindow_PreviewDragEnter(object sender, DragEventArgs e)
        {
            object obj = e.Data.GetData("FileNameW");
            if (obj != null)
            {
                e.Handled = true;
                this.ActionsGrid.Visibility = Visibility.Hidden;
                this.ImportCommandVisualGrid.Visibility = Visibility.Visible;
            }
        }

        private async void CommandEditorWindow_Drop(object sender, DragEventArgs e)
        {
            object obj = e.Data.GetData("FileNameW");
            if (obj != null)
            {
                bool success = false;
                try
                {
                    e.Handled = true;
                    this.ActionsGrid.Visibility = Visibility.Visible;
                    this.ImportCommandVisualGrid.Visibility = Visibility.Hidden;

                    string filename = ((string[])obj)[0];
                    if (!string.IsNullOrEmpty(filename))
                    {
                        CommandModelBase command = await FileSerializerHelper.DeserializeFromFile<CommandModelBase>(filename);
                        if (command != null)
                        {
                            await this.viewModel.AddActions(command.Actions);
                            success = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }

                if (!success)
                {
                    await DialogHelper.ShowMessage(MixItUp.Base.Resources.FailedToImportCommand);
                }
            }
        }

        private void CommandEditorWindow_PreviewDragLeave(object sender, DragEventArgs e)
        {
            object obj = e.Data.GetData("FileNameW");
            if (obj != null)
            {
                e.Handled = true;
                this.ActionsGrid.Visibility = Visibility.Visible;
                this.ImportCommandVisualGrid.Visibility = Visibility.Hidden;
            }
        }

        private async void UploadCommunityCommandButton_Click(object sender, RoutedEventArgs e)
        {
            this.ViewModel.StartLoadingOperation();

            CommandModelBase command = await this.viewModel.ValidateAndBuildCommand();
            if (command != null)
            {
                CommunityCommandUploadWindow window = new CommunityCommandUploadWindow(command);
                window.Show();
            }

            this.ViewModel.EndLoadingOperation();
        }
    }
}

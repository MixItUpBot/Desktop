using MixItUp.Base;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.Store;
using MixItUp.Base.Services;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel;
using MixItUp.Base.ViewModel.CommunityCommands;
using MixItUp.WPF.Branding;
using MixItUp.WPF.Controls.Dialogs;
using MixItUp.WPF.Controls.MainControls;
using MixItUp.WPF.Windows;
using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace MixItUp.WPF
{
    /// <summary>
    /// Interaction logic for StreamerWindow.xaml
    /// </summary>
    public partial class MainWindow : LoadingWindowBase
    {
        public class MainWindowUIViewModel : MainWindowViewModel
        {
            public Visibility HelpLinkVisibility { get { return Visibility.Collapsed; } }
        }

        private bool restartApplication = false;

        private bool shutdownStarted = false;
        private bool shutdownComplete = false;

        private MainWindowViewModel viewModel;

        public MainWindow()
            : base(new MainWindowUIViewModel())
        {
            InitializeComponent();

            ChannelSession.OnRestartRequested += ChannelSession_OnRestartRequested;

            this.Closing += MainWindow_Closing;
            this.Initialize(this.StatusBar);

            this.viewModel = (MainWindowViewModel)this.ViewModel;
            this.viewModel.StartLoadingOperationOccurred += (sender, args) =>
            {
                this.StartLoadingOperation();
            };
            this.viewModel.EndLoadingOperationOccurred += (sender, args) =>
            {
                this.EndLoadingOperation();
            };

            if (ChannelSession.AppSettings.Width > 0 && !ChannelSession.AppSettings.DontSaveLastWindowPosition)
            {
                this.WindowStartupLocation = WindowStartupLocation.Manual;
                this.Height = ChannelSession.AppSettings.Height;
                this.Width = ChannelSession.AppSettings.Width;
                this.Top = ChannelSession.AppSettings.Top;
                this.Left = ChannelSession.AppSettings.Left;

                var rect = new System.Drawing.Rectangle((int)this.Left, (int)this.Top, (int)this.Width, (int)this.Height);
                var screen = System.Windows.Forms.Screen.FromRectangle(rect);
                if (!screen.Bounds.Contains(rect))
                {
                    // Off the bottom of the screen?
                    if (this.Top + this.Height > screen.Bounds.Top + screen.Bounds.Height)
                    {
                        this.Top = screen.Bounds.Top + screen.Bounds.Height - this.Height;
                    }

                    // Off the right side of the screen?
                    if (this.Left + this.Width > screen.Bounds.Left + screen.Bounds.Width)
                    {
                        this.Left = screen.Bounds.Left + screen.Bounds.Width - this.Width;
                    }

                    // Off the top of the screen?
                    if (this.Top < screen.Bounds.Top)
                    {
                        this.Top = screen.Bounds.Top;
                    }

                    // Off the left side of the screen?
                    if (this.Left < screen.Bounds.Left)
                    {
                        this.Left = screen.Bounds.Left;
                    }
                }

                if (ChannelSession.AppSettings.IsMaximized)
                {
                    WindowState = WindowState.Maximized;
                }
            }
        }

        public void Restart()
        {
            this.restartApplication = true;
            this.Close();
        }

        internal void RestartForUpdate()
        {
            if (this.shutdownStarted) { return; }
            this.restartApplication = true;
            this.shutdownStarted = true;
            _ = this.StartShutdownProcess();
        }

        protected override async Task OnLoaded()
        {
            ServiceManager.Get<IInputService>().Initialize(new WindowInteropHelper(this).Handle);
            foreach (HotKeyConfiguration hotKeyConfiguration in ChannelSession.Settings.HotKeys.Values)
            {
                CommandModelBase command = ChannelSession.Settings.GetCommand(hotKeyConfiguration.CommandID);
                if (command != null)
                {
                    if (!ServiceManager.Get<IInputService>().RegisterHotKey(hotKeyConfiguration)) { Logger.Log(LogLevel.Warning, MixItUp.Base.Resources.HotKeyUnavailable); }
                }
            }

            if (!string.IsNullOrEmpty(ChannelSession.Settings.Name))
            {
                this.Title += " - " + ChannelSession.Settings.Name;
            }

            this.Title += " - v" + VersionHelper.GetFullVersionString() + BuildChannelHelper.BUILD_CHANNEL_SUFFIX;

            await this.MainMenu.Initialize(this);

            //await this.MainMenu.AddMenuItem(MixItUp.Base.Resources.MixItUpOnline, new MixItUpOnlineControl(), "https://online.mixitup.bot/alpha");
            await this.MainMenu.AddMenuItem(MixItUp.Base.Resources.Channel, new ChannelControl(), "https://wiki.mixitup.bot/channel", feature: Features.Channel);
            await this.MainMenu.AddMenuItem(MixItUp.Base.Resources.Chat, new ChatControl(), "https://wiki.mixitup.bot/chat", canHide: false, feature: Features.Chat);
            await this.MainMenu.AddMenuItem(MixItUp.Base.Resources.Commands, new ChatCommandsControl(), "https://wiki.mixitup.bot/commands/chat-commands", feature: Features.ChatCommands);
            await this.MainMenu.AddMenuItem(MixItUp.Base.Resources.Events, new EventsControl(), "https://wiki.mixitup.bot/commands/event-commands", feature: Features.EventCommands);
            await this.MainMenu.AddMenuItem(MixItUp.Base.Resources.Timers, new TimerControl(), "https://wiki.mixitup.bot/commands/timer-commands", feature: Features.TimerCommands);
            await this.MainMenu.AddMenuItem(MixItUp.Base.Resources.ActionGroups, new ActionGroupControl(), "https://wiki.mixitup.bot/commands/action-groups", feature: Features.ActionGroups);
            await this.MainMenu.AddMenuItem(MixItUp.Base.Resources.CommunityCommands, new CommunityCommandsControl(), "https://wiki.mixitup.bot/commands/community-commands", feature: Features.CommunityCommands);
            await this.MainMenu.AddMenuItem(MixItUp.Base.Resources.Users, new UsersControl(), "https://wiki.mixitup.bot/users", feature: Features.Users);
            await this.MainMenu.AddMenuItem(MixItUp.Base.Resources.MusicPlayer, new MusicPlayerControl(), "https://wiki.mixitup.bot/music-player", feature: Features.MusicPlayer);
            //await this.MainMenu.AddMenuItem(MixItUp.Base.Resources.Statistics, new StatisticsControl(), "https://wiki.mixitup.bot/statistics");
            await this.MainMenu.AddMenuItem(MixItUp.Base.Resources.CurrencyRankInventory, new CurrencyRankInventoryControl(), "https://wiki.mixitup.bot/consumables", feature: Features.Consumables);
            await this.MainMenu.AddMenuItem(MixItUp.Base.Resources.TwitchChannelPoints, new TwitchChannelPointsControl(), "https://wiki.mixitup.bot/commands/twitch-channel-point-commands", brand: Brands.Twitch);
            await this.MainMenu.AddMenuItem(MixItUp.Base.Resources.TwitchCustomPowerUps, new TwitchCustomPowerUpsControl(), "https://wiki.mixitup.bot/commands/twitch-custom-power-up-commands", brand: Brands.Twitch);
            await this.MainMenu.AddMenuItem(MixItUp.Base.Resources.TwitchBits, new TwitchBitsControl(), "https://wiki.mixitup.bot/commands/twitch-bits-commands", brand: Brands.Twitch);
            await this.MainMenu.AddMenuItem(MixItUp.Base.Resources.KickChannelPoints, new KickChannelPointsControl(), "https://wiki.mixitup.bot/commands/kick-channel-point-commands", brand: Brands.Kick);
            await this.MainMenu.AddMenuItem(MixItUp.Base.Resources.KickKicks, new KickKicksControl(), "https://wiki.mixitup.bot/commands/kick-kicks-commands", brand: Brands.Kick);
            await this.MainMenu.AddMenuItem(MixItUp.Base.Resources.VeloraChannelPoints, new VeloraChannelPointsControl(), "https://wiki.mixitup.bot/en/platforms/velora/channel-points", brand: Brands.Velora);
            await this.MainMenu.AddMenuItem(MixItUp.Base.Resources.VPZoneChannelPoints, new VPZoneChannelPointsControl(), "https://wiki.mixitup.bot/en/platforms/vpzone/channel-points", brand: Brands.VPZone);
            await this.MainMenu.AddMenuItem(MixItUp.Base.Resources.ResourceManager.GetString("AltStreamChannelPoints"), new AltStreamChannelPointsControl(), "https://altstream.online/documentation?tab=api-v2", brand: Brands.AltStream);
            await this.MainMenu.AddMenuItem(MixItUp.Base.Resources.StreamlootsCards, new StreamlootsCardsControl(), "https://wiki.mixitup.bot/commands/streamloots-card-commands", brand: Brands.Streamloots);
            await this.MainMenu.AddMenuItem(MixItUp.Base.Resources.CrowdControl, new CrowdControlControl(), "https://wiki.mixitup.bot/commands/crowd-control-commands", brand: Brands.CrowdControl);
            await this.MainMenu.AddMenuItem(MixItUp.Base.Resources.StreamPass, new StreamPassControl(), "https://wiki.mixitup.bot/consumables/stream-pass", feature: Features.StreamPass);
            await this.MainMenu.AddMenuItem(MixItUp.Base.Resources.RedemptionStore, new RedemptionStoreControl(), "https://wiki.mixitup.bot/redemption-store", feature: Features.RedemptionStore);
            await this.MainMenu.AddMenuItem(MixItUp.Base.Resources.OverlayWidgets, new OverlayWidgetsControl(), "https://wiki.mixitup.bot/overlay-widgets", feature: Features.OverlayWidgets);
            await this.MainMenu.AddMenuItem(MixItUp.Base.Resources.Games, new GamesControl(), "https://wiki.mixitup.bot/commands/game-commands", feature: Features.Games);
            await this.MainMenu.AddMenuItem(MixItUp.Base.Resources.Giveaway, new GiveawayControl(), "https://wiki.mixitup.bot/giveaways", feature: Features.Giveaways);
            await this.MainMenu.AddMenuItem(MixItUp.Base.Resources.GameQueue, new GameQueueControl(), "https://wiki.mixitup.bot/game-queue", feature: Features.GameQueue);
            await this.MainMenu.AddMenuItem(MixItUp.Base.Resources.Quotes, new QuoteControl(), "https://wiki.mixitup.bot/quotes", feature: Features.Quotes);
            await this.MainMenu.AddMenuItem(MixItUp.Base.Resources.Moderation, new ModerationControl(), "https://wiki.mixitup.bot/moderation", feature: Features.Moderation);
            await this.MainMenu.AddMenuItem(MixItUp.Base.Resources.CommandHistory, new CommandHistoryControl(), "https://wiki.mixitup.bot/commands/command-history", feature: Features.CommandHistory);
            await this.MainMenu.AddMenuItem(MixItUp.Base.Resources.Services, new ServicesControl(), "https://wiki.mixitup.bot/services", canHide: false, feature: Features.Services);
            await this.MainMenu.AddMenuItem(MixItUp.Base.Resources.Webhooks, new WebhooksControl(), "https://wiki.mixitup.bot/commands/webhook-commands", feature: Features.WebhookCommands);
            await this.MainMenu.AddMenuItem(MixItUp.Base.Resources.Accounts, new AccountsControl(), "https://wiki.mixitup.bot/accounts", canHide: false, feature: Features.Accounts);
            await this.MainMenu.AddMenuItem(MixItUp.Base.Resources.Changelog, new ChangelogControl(), "https://wiki.mixitup.bot/", feature: Features.Changelog);
            await this.MainMenu.AddMenuItem(MixItUp.Base.Resources.About, new AboutControl(), "https://wiki.mixitup.bot/", canHide: false, feature: Features.About);

            if (ChannelSession.IsDebug())
            {
                await this.MainMenu.AddMenuItem(MixItUp.Base.Resources.Debug, new DebugControl(), "https://wiki.mixitup.bot/", feature: Features.Debug);
                await this.MainMenu.AddMenuItem("Icon Map", new IconMapControl(), feature: Features.IconMap);
            }

            this.MainMenu.LoadMenuOrder();

            this.MainMenu.MenuItemSelected(MixItUp.Base.Resources.Chat);


            await ServiceManager.Get<MixItUpService>().StartNotificationPolling();

            ActivationProtocolHandler.OnCommunityCommandActivation += ActivationProtocolHandler_OnCommunityCommandActivation;
            ActivationProtocolHandler.OnCommandFileActivation += ActivationProtocolHandler_OnCommandFileActivation;

            if (SettingsV3Upgrader.OverlayV3UpgradeOccurred && ChannelSession.Settings.OverlayEndpointsV3.Count > 1)
            {
                await DialogHelper.ShowCustom(new OverlayEndpointsUpdateDialogControl());
            }
        }

        private async Task StartShutdownProcess()
        {
            Guid? profile = ChannelSession.Settings?.ID;
            try
            {
                Logger.Log(LogLevel.Debug, "Starting shutdown process");

                ServiceManager.Get<MixItUpService>()?.StopNotificationPolling();

                if (WindowState == WindowState.Maximized)
                {
                    // Use the RestoreBounds as the current values will be 0, 0 and the size of the screen
                    ChannelSession.AppSettings.Top = RestoreBounds.Top;
                    ChannelSession.AppSettings.Left = RestoreBounds.Left;
                    ChannelSession.AppSettings.Height = RestoreBounds.Height;
                    ChannelSession.AppSettings.Width = RestoreBounds.Width;
                    ChannelSession.AppSettings.IsMaximized = true;
                }
                else
                {
                    ChannelSession.AppSettings.Top = this.Top;
                    ChannelSession.AppSettings.Left = this.Left;
                    ChannelSession.AppSettings.Height = this.Height;
                    ChannelSession.AppSettings.Width = this.Width;
                    ChannelSession.AppSettings.IsMaximized = false;
                }

                Properties.Settings.Default.Save();

                this.ShuttingDownGrid.Visibility = Visibility.Visible;
                this.MainMenu.Visibility = Visibility.Collapsed;

                await ServiceManager.Get<SettingsService>().Save(ChannelSession.Settings);

                await ChannelSession.AppSettings.Save();

                await ChannelSession.Close();

                this.shutdownComplete = true;

                Logger.Log(LogLevel.Debug, "Shutdown process complete");
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            if (this.restartApplication)
            {
                Services.BackgroundUpdates.Relaunch(profile);
            }
            this.Close();
        }

        private async void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.Activate();
            if (!this.shutdownStarted)
            {
                e.Cancel = true;

                int timeout = 10000;
                var confirmationTask = DialogHelper.ShowConfirmation(MixItUp.Base.Resources.ExitConfirmation);
                var delayTask = Task.Delay(timeout);

                var completedTask = await Task.WhenAny(confirmationTask, delayTask);

                bool confirmed = false;
                if (completedTask == confirmationTask)
                {
                    confirmed = await confirmationTask;
                }
                else
                {
                    DialogHelper.CloseCurrent();
                    confirmed = true;
                }

                if (confirmed)
                {
                    this.shutdownStarted = true;
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    this.StartShutdownProcess();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                }
            }
            else if (!this.shutdownComplete)
            {
                e.Cancel = true;
            }
        }

        private void ChannelSession_OnRestartRequested(object sender, EventArgs e) { this.Restart(); }

        private async void ActivationProtocolHandler_OnCommunityCommandActivation(object sender, Guid commandID)
        {
            await DispatcherHelper.Dispatcher.InvokeAsync(async () =>
            {
                CommunityCommandDetailsModel commandDetails = await ServiceManager.Get<MixItUpService>().GetCommandDetails(commandID);
                if (commandDetails != null)
                {
                    await CommunityCommandsControl.ProcessDownloadedCommunityCommand(new CommunityCommandDetailsViewModel(commandDetails));
                }
            });
        }

        private async void ActivationProtocolHandler_OnCommandFileActivation(object sender, CommandModelBase command)
        {
            await DispatcherHelper.Dispatcher.InvokeAsync(async () =>
            {
                await DialogHelper.ShowCustom(new CommandImporterDialogControl(command));
            });
        }
    }
}

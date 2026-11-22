using MixItUp.Base.Model;
using MixItUp.Base.Services;
using MixItUp.Base.Services.Velora.New;
using MixItUp.Base.Services.VPZone.New;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModels;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MixItUp.Base.ViewModel.Accounts
{
    public class StreamingPlatformAccountControlViewModel : UIViewModelBase
    {
        public StreamingPlatformTypeEnum Platform
        {
            get { return this.platform; }
            private set
            {
                this.platform = value;
                this.NotifyPropertyChanged();
                this.NotifyAllProperties();
            }
        }
        private StreamingPlatformTypeEnum platform;

        public string PlatformName { get { return EnumLocalizationHelper.GetLocalizedName(this.Platform); } }

        public string PlatformImage { get { return StreamingPlatforms.GetPlatformImage(this.Platform); } }

        public string ButtonColor
        {
            get
            {
                if (this.Platform == StreamingPlatformTypeEnum.Twitch) { return "#9146FF"; }
                if (this.Platform == StreamingPlatformTypeEnum.YouTube) { return "#FF0033"; }
                if (this.Platform == StreamingPlatformTypeEnum.Kick) { return "#00E701"; }
                if (this.Platform == StreamingPlatformTypeEnum.Velora) { return "#FDCB16"; }
                if (this.Platform == StreamingPlatformTypeEnum.VPZone) { return "#7C3AED"; }
                if (this.Platform == StreamingPlatformTypeEnum.Joystick) { return "#09ABAB"; }
                return "#3f51b5";
            }
        }
        public string ButtonImage
        {
            get
            {
                if (this.Platform == StreamingPlatformTypeEnum.Twitch) { return "/Assets/Images/twitch-dark_lg.png"; }
                if (this.Platform == StreamingPlatformTypeEnum.YouTube) { return "/Assets/Images/youtube-dark_lg.png"; }
                if (this.Platform == StreamingPlatformTypeEnum.Kick) { return "/Assets/Images/kick-light_lg.png"; }
                if (this.Platform == StreamingPlatformTypeEnum.Velora) { return "/Assets/Images/velora-light_lg.png"; }
                if (this.Platform == StreamingPlatformTypeEnum.VPZone) { return "/Assets/Images/vpzone-light_lg.png"; }
                if (this.Platform == StreamingPlatformTypeEnum.Joystick) { return "/Assets/Images/joystick-light_lg.png"; }
                return StreamingPlatforms.GetPlatformImage(this.Platform);
            }
        }
        public string ButtonTextForeground
        {
            get
            {
                if (this.Platform == StreamingPlatformTypeEnum.Twitch) { return "#FFFFFF"; }
                if (this.Platform == StreamingPlatformTypeEnum.YouTube) { return "#FFFFFF"; }
                if (this.Platform == StreamingPlatformTypeEnum.Kick) { return "#000000"; }
                if (this.Platform == StreamingPlatformTypeEnum.Velora) { return "#000000"; }
                if (this.Platform == StreamingPlatformTypeEnum.VPZone) { return "#FFFFFF"; }
                if (this.Platform == StreamingPlatformTypeEnum.Joystick) { return "#000000"; }
                return "#000000";
            }
        }

        public string ButtonLoginText
        {
            get
            {
                if (this.Platform == StreamingPlatformTypeEnum.Twitch) { return MixItUp.Base.Resources.LogInWithTwitch; }
                else if (this.Platform == StreamingPlatformTypeEnum.YouTube) { return MixItUp.Base.Resources.LogInWithYouTube; }
                else if (this.Platform == StreamingPlatformTypeEnum.Kick) { return MixItUp.Base.Resources.LogInWithKick; }
                else if (this.Platform == StreamingPlatformTypeEnum.Velora) { return MixItUp.Base.Resources.LogInWithVelora; }
                else if (this.Platform == StreamingPlatformTypeEnum.VPZone) { return MixItUp.Base.Resources.LogInWithVPZone; }
                else if (this.Platform == StreamingPlatformTypeEnum.Joystick) { return MixItUp.Base.Resources.LogInWithJoystick; }
                return string.Empty;
            }
        }

        public string ButtonLogoutText
        {
            get
            {
                if (this.Platform == StreamingPlatformTypeEnum.Twitch) { return MixItUp.Base.Resources.LogOutOfTwitch; }
                else if (this.Platform == StreamingPlatformTypeEnum.YouTube) { return MixItUp.Base.Resources.LogOutOfYouTube; }
                else if (this.Platform == StreamingPlatformTypeEnum.Kick) { return MixItUp.Base.Resources.LogOutOfKick; }
                else if (this.Platform == StreamingPlatformTypeEnum.Velora) { return MixItUp.Base.Resources.LogOutOfVelora; }
                else if (this.Platform == StreamingPlatformTypeEnum.VPZone) { return MixItUp.Base.Resources.LogOutOfVPZone; }
                else if (this.Platform == StreamingPlatformTypeEnum.Joystick) { return MixItUp.Base.Resources.LogOutOfJoystick; }
                return string.Empty;
            }
        }

        // A Velora bot is not a second login: it is created or picked in-app from the bots under the
        // streamer's account, so its button reads "Set Up Bot" rather than "Log In With Velora".
        public string BotButtonLoginText
        {
            get
            {
                if (this.Platform == StreamingPlatformTypeEnum.Velora) { return MixItUp.Base.Resources.VeloraSetUpBot; }
                return this.ButtonLoginText;
            }
        }

        public string BotButtonLogoutText
        {
            get
            {
                if (this.Platform == StreamingPlatformTypeEnum.Velora) { return MixItUp.Base.Resources.VeloraRemoveBot; }
                return this.ButtonLogoutText;
            }
        }

        // Velora is the one platform whose bot is not a separate login, so its accounts box carries an
        // explanatory note under the buttons.
        public bool IsPlatformBotNoteVisible { get { return this.Platform == StreamingPlatformTypeEnum.Velora; } }

        public string PlatformBotNoteText
        {
            get
            {
                if (this.Platform == StreamingPlatformTypeEnum.Velora) { return MixItUp.Base.Resources.VeloraBotSetupNote; }
                return string.Empty;
            }
        }

        public bool IsStreamerAccountEnabled { get { return this.session.IsEnabled; } }
        public bool IsStreamerAccountConnected { get { return this.session.IsConnected; } }

        public string StreamerAccountUsername
        {
            get { return this.streamerAccountUsername; }
            set
            {
                this.streamerAccountUsername = value;
                this.NotifyPropertyChanged();
            }
        }
        private string streamerAccountUsername;
        public string StreamerAccountAvatar
        {
            get { return this.streamerAccountAvatar; }
            set
            {
                this.streamerAccountAvatar = value;
                this.NotifyPropertyChanged();
            }
        }
        private string streamerAccountAvatar;

        public bool IsBotAccountEnabled { get { return this.session.IsBotEnabled; } }
        public bool IsBotAccountConnected { get { return this.session.IsBotConnected; } }

        public string BotAccountUsername
        {
            get { return this.Platform == StreamingPlatformTypeEnum.Joystick ? this.session?.BotUsername : this.botAccountUsername; }
            set
            {
                this.botAccountUsername = value;
                this.NotifyPropertyChanged();
            }
        }
        private string botAccountUsername;
        public string BotAccountAvatar
        {
            get { return this.botAccountAvatar; }
            set
            {
                this.botAccountAvatar = value;
                this.NotifyPropertyChanged();
            }
        }
        private string botAccountAvatar;

        public bool IsStreamerAccountLogInVisible { get { return !this.IsStreamerAccountEnabled && !this.IsStreamerAccountConnected && !this.isStreamerConnecting; } }
        public ICommand StreamerAccountLogInCommand { get; set; }
        public bool IsStreamerAccountCancelVisible { get { return !this.IsStreamerAccountEnabled && !this.IsStreamerAccountConnected && this.isStreamerConnecting; } }
        public ICommand StreamerAccountCancelCommand { get; set; }
        public bool IsStreamerAccountLogoutVisible { get { return this.IsStreamerAccountEnabled || this.IsStreamerAccountConnected; } }
        public ICommand StreamerAccountLogOutCommand { get; set; }

        public bool HasBotAccountColumn => this.Platform != StreamingPlatformTypeEnum.Joystick;
        public bool IsBotAccountLogInVisible { get { return this.Platform != StreamingPlatformTypeEnum.Joystick && !this.IsBotAccountEnabled && !this.IsBotAccountConnected && !this.isBotConnecting; } }
        public ICommand BotAccountLogInCommand { get; set; }
        public bool IsBotAccountCancelVisible { get { return !this.IsBotAccountEnabled && !this.IsBotAccountConnected && this.isBotConnecting; } }
        public ICommand BotAccountCancelCommand { get; set; }
        public bool IsBotAccountLogoutVisible { get { return this.IsBotAccountEnabled || this.IsBotAccountConnected; } }
        public ICommand BotAccountLogOutCommand { get; set; }

        // Velora only: the connected bot can be edited in place (profile image / rename).
        public bool IsBotAccountEditVisible { get { return this.Platform == StreamingPlatformTypeEnum.Velora && this.IsBotAccountConnected; } }
        public ICommand BotAccountEditCommand { get; set; }

        private StreamingPlatformSessionBase session;

        // The connecting flags drive the log in / cancel button swap. They are set on the UI thread before the
        // background attempt starts, so a fast-failing attempt cannot leave the cancel spinner running forever
        // the way a task field assigned after the fact could.
        private bool isStreamerConnecting = false;
        private CancellationTokenSource streamerConnectCancellationTokenSource = new CancellationTokenSource();

        private bool isBotConnecting = false;
        private CancellationTokenSource botConnectCancellationTokenSource = new CancellationTokenSource();

        public StreamingPlatformAccountControlViewModel(StreamingPlatformTypeEnum platform)
        {
            this.Platform = platform;

            this.session = StreamingPlatforms.GetPlatformSession(this.Platform);
            if (this.session.IsEnabled)
            {
                if (this.session.IsConnected)
                {
                    this.StreamerAccountUsername = this.session.StreamerUsername;
                    this.StreamerAccountAvatar = this.session.StreamerAvatarURL;
                }
                else
                {
                    this.StreamerAccountUsername = Resources.Unknown;
                }
            }

            if (this.session.IsBotEnabled)
            {
                if (this.session.IsBotConnected)
                {
                    this.BotAccountUsername = this.session.BotUsername;
                    this.BotAccountAvatar = this.session.BotAvatarURL;
                }
                else
                {
                    this.BotAccountUsername = Resources.Unknown;
                }
            }

            this.StreamerAccountLogInCommand = this.CreateCommand(() =>
            {
                try
                {
                    this.streamerConnectCancellationTokenSource.Cancel();

                    // Held locally so this attempt always judges itself by its own token. Reading the field
                    // instead let a superseded attempt mistake a newer attempt's fresh token for its own and
                    // report a failure the user never caused.
                    CancellationTokenSource attemptCancellationTokenSource = new CancellationTokenSource();
                    this.streamerConnectCancellationTokenSource = attemptCancellationTokenSource;
                    this.isStreamerConnecting = true;

                    _ = AsyncRunner.RunAsyncBackground(async (cancellationToken) =>
                    {
                        string messageToShow = null;
                        try
                        {
                            Result result = await this.session.ManualConnectStreamer(attemptCancellationTokenSource.Token);
                            if (attemptCancellationTokenSource.IsCancellationRequested)
                            {
                                // Cancelled by the user or superseded by a newer attempt. Either way this
                                // attempt must not touch shared state or raise a dialog.
                                return;
                            }

                            if (result.Success)
                            {
                                if (ChannelSession.Settings.DefaultStreamingPlatform == StreamingPlatformTypeEnum.None)
                                {
                                    ChannelSession.Settings.DefaultStreamingPlatform = this.Platform;
                                }

                                if (string.Equals(this.session.StreamerID, this.session.BotID, StringComparison.CurrentCultureIgnoreCase))
                                {
                                    await this.session.DisableBot();
                                }

                                this.StreamerAccountUsername = this.session.StreamerUsername;
                                this.StreamerAccountAvatar = this.session.StreamerAvatarURL;
                            }
                            else
                            {
                                await this.session.DisableStreamer();

                                messageToShow = GetConnectionFailureMessage(result);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Log(ex);
                        }
                        finally
                        {
                            if (this.streamerConnectCancellationTokenSource == attemptCancellationTokenSource)
                            {
                                this.isStreamerConnecting = false;
                            }

                            this.NotifyAllProperties();
                        }

                        // Shown only after the button has been put back, so the user is not left looking at a
                        // progress spinner behind an error they have already been told about.
                        if (messageToShow != null)
                        {
                            await DispatcherHelper.Dispatcher.InvokeAsync(async () => await DialogHelper.ShowMessage(messageToShow));
                        }

                    }, attemptCancellationTokenSource.Token);
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }

                this.NotifyAllProperties();
            });

            this.StreamerAccountCancelCommand = this.CreateCommand(async () =>
            {
                try
                {
                    this.streamerConnectCancellationTokenSource.Cancel();

                    this.isStreamerConnecting = false;

                    await this.session.DisableStreamer();
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }

                this.NotifyAllProperties();
            });

            this.StreamerAccountLogOutCommand = this.CreateCommand(async () =>
            {
                try
                {
                    await this.session.DisableStreamer();

                    this.StreamerAccountUsername = null;
                    this.StreamerAccountAvatar = null;
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }

                this.NotifyAllProperties();
            });


            this.BotAccountLogInCommand = this.CreateCommand(() =>
            {
                try
                {
                    this.botConnectCancellationTokenSource.Cancel();

                    CancellationTokenSource attemptCancellationTokenSource = new CancellationTokenSource();
                    this.botConnectCancellationTokenSource = attemptCancellationTokenSource;
                    this.isBotConnecting = true;

                    _ = AsyncRunner.RunAsyncBackground(async (cancellationToken) =>
                    {
                        string messageToShow = null;
                        try
                        {
                            Result result = await this.session.ManualConnectBot(attemptCancellationTokenSource.Token);
                            if (attemptCancellationTokenSource.IsCancellationRequested)
                            {
                                return;
                            }

                            if (result.Success)
                            {
                                if (string.Equals(this.session.StreamerID, this.session.BotID, StringComparison.CurrentCultureIgnoreCase))
                                {
                                    await this.session.DisableBot();

                                    messageToShow = Resources.BotAccountMustBeDifferent;
                                }

                                this.BotAccountUsername = this.session.BotUsername;
                                this.BotAccountAvatar = this.session.BotAvatarURL;
                            }
                            else
                            {
                                await this.session.DisableBot();

                                messageToShow = GetConnectionFailureMessage(result);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Log(ex);
                        }
                        finally
                        {
                            if (this.botConnectCancellationTokenSource == attemptCancellationTokenSource)
                            {
                                this.isBotConnecting = false;
                            }

                            this.NotifyAllProperties();
                        }

                        if (messageToShow != null)
                        {
                            await DispatcherHelper.Dispatcher.InvokeAsync(async () => await DialogHelper.ShowMessage(messageToShow));
                        }

                    }, attemptCancellationTokenSource.Token);
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }

                this.NotifyAllProperties();
            });

            this.BotAccountCancelCommand = this.CreateCommand(async () =>
            {
                try
                {
                    this.botConnectCancellationTokenSource.Cancel();

                    this.isBotConnecting = false;

                    await this.session.DisableBot();
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }

                this.NotifyAllProperties();
            });

            this.BotAccountLogOutCommand = this.CreateCommand(async () =>
            {
                try
                {
                    await this.session.DisableBot();

                    this.BotAccountUsername = null;
                    this.BotAccountAvatar = null;
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }

                this.NotifyAllProperties();
            });

            this.BotAccountEditCommand = this.CreateCommand(async () =>
            {
                try
                {
                    if (this.Platform == StreamingPlatformTypeEnum.Velora)
                    {
                        Result result = await ServiceManager.Get<VeloraSession>().EditBot();
                        if (!result.Success && !string.IsNullOrEmpty(result.Message))
                        {
                            await DialogHelper.ShowMessage(result.Message);
                        }

                        this.BotAccountUsername = this.session.BotUsername;
                        this.BotAccountAvatar = this.session.BotAvatarURL;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }

                this.NotifyAllProperties();
            });
        }

        /// <summary>Last line of defense against showing the user an empty dialog: a failed connection that
        /// carries no message of its own still has to say something actionable.</summary>
        private static string GetConnectionFailureMessage(Result result)
        {
            if (result != null && !string.IsNullOrWhiteSpace(result.Message))
            {
                return result.Message;
            }
            return Resources.AuthenticationFailedGeneric;
        }

        private void NotifyAllProperties()
        {
            this.NotifyPropertyChanged(nameof(IsStreamerAccountConnected));
            this.NotifyPropertyChanged(nameof(IsStreamerAccountLogInVisible));
            this.NotifyPropertyChanged(nameof(IsStreamerAccountCancelVisible));
            this.NotifyPropertyChanged(nameof(IsStreamerAccountLogoutVisible));

            this.NotifyPropertyChanged(nameof(IsBotAccountConnected));
            this.NotifyPropertyChanged(nameof(IsBotAccountLogInVisible));
            this.NotifyPropertyChanged(nameof(IsBotAccountCancelVisible));
            this.NotifyPropertyChanged(nameof(IsBotAccountLogoutVisible));
            this.NotifyPropertyChanged(nameof(IsBotAccountEditVisible));
        }
    }
}

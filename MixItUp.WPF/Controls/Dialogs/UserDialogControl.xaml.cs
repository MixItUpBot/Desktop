using MixItUp.Base.Model;
using MixItUp.Base.Model.User;
using MixItUp.Base.Services;
using MixItUp.Base.Services.Joystick;
using MixItUp.Base.ViewModel.User;
using System.Windows;
using System.Windows.Controls;

namespace MixItUp.WPF.Controls.Dialogs
{
    public enum UserDialogResult
    {
        Purge,
        Timeout1,
        Timeout5,
        Ban,
        Unban,
        Close,
        PromoteToMod,
        DemoteFromMod,
        ChannelPage,
        EditUser,
    }

    /// <summary>
    /// Interaction logic for UserDialogControl.xaml
    /// </summary>
    public partial class UserDialogControl : UserControl
    {
        private UserV2ViewModel user;

        public UserDialogControl(UserV2ViewModel user)
        {
            this.DataContext = this.user = user;

            InitializeComponent();

            this.Loaded += UserDialogControl_Loaded;
        }

        private async void UserDialogControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (this.user != null && this.user.Platform != StreamingPlatformTypeEnum.None && !string.IsNullOrEmpty(this.user.Username))
            {
                await this.user.Refresh(force: true);

                this.DataContext = this.user;
                if (this.user.Platform is StreamingPlatformTypeEnum.Joystick or StreamingPlatformTypeEnum.Razo or StreamingPlatformTypeEnum.AltStream)
                {
                    // Joystick accepts mute and ban only from a specific chat message.
                    this.PurgeButton.Visibility = this.TimeoutOneMinButton.Visibility = this.TimeoutFiveMinButton.Visibility = Visibility.Collapsed;
                    this.PromoteToModButton.Visibility = this.DemoteFromModButton.Visibility = this.BanButton.Visibility = Visibility.Collapsed;
                    this.UnbanButton.Visibility = this.user.Platform == StreamingPlatformTypeEnum.Joystick &&
                        ServiceManager.Get<JoystickSession>() is { IsConnected: true } joystick && joystick.Permissions.Contains("chat:moderate")
                        ? Visibility.Visible : Visibility.Collapsed;
                    return;
                }

#pragma warning disable CS0612 // Type or member is obsolete
                if (this.user.HasRole(UserRoleEnum.Banned))
#pragma warning restore CS0612 // Type or member is obsolete
                {
                    this.UnbanButton.Visibility = System.Windows.Visibility.Visible;
                    this.BanButton.Visibility = System.Windows.Visibility.Collapsed;
                }

                if (this.user.HasRole(UserRoleEnum.Moderator))
                {
                    this.DemoteFromModButton.Visibility = System.Windows.Visibility.Visible;
                    this.PromoteToModButton.Visibility = System.Windows.Visibility.Collapsed;
                }
            }
        }
    }
}

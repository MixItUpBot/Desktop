using Strings = MixItUp.Base.Resources;
using MixItUp.Base.Model;
using MixItUp.Base.Services;
using MixItUp.Base.Util;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace MixItUp.WPF.Controls.Accounts
{
    public partial class ChannelChatAccountControl : UserControl
    {
        public StreamingPlatformTypeEnum Platform { get; set; }
        private ChannelChatSessionBase Session => (ChannelChatSessionBase)StreamingPlatforms.GetPlatformSession(this.Platform);
        public ChannelChatAccountControl() { this.InitializeComponent(); }

        private void ControlLoaded(object sender, RoutedEventArgs e)
        {
            this.Card.Header = this.Platform.ToString();
            this.Channel.Visibility = this.ChannelLabel.Visibility = this.Session.RequiresChannelName ? Visibility.Visible : Visibility.Collapsed;
            ChannelChatSessionBase.AccountChanged += this.AccountChanged;
            this.Unloaded += this.ControlUnloaded;
            if (this.Platform == StreamingPlatformTypeEnum.Razo)
            {
                this.TokenLabel.Text = Strings.ResourceManager.GetString("RazoPrivateCode");
                System.Windows.Automation.AutomationProperties.SetName(this.Token, this.TokenLabel.Text);
            }
            var brand = this.Platform == StreamingPlatformTypeEnum.Razo ? Branding.Brands.Razo : Branding.Brands.AltStream;
            this.LogoutButton.Background = brand.PrimaryBrush;
            this.LogoutIcon.Source = brand.Mono.OnLight.Small;
            this.Logo.Source = new BitmapImage(new Uri("pack://application:,,,/MixItUp;component" + StreamingPlatforms.GetPlatformImage(this.Platform)));
            this.UpdateFields();
        }

        private void ControlUnloaded(object sender, RoutedEventArgs e)
        {
            ChannelChatSessionBase.AccountChanged -= this.AccountChanged;
            this.Unloaded -= this.ControlUnloaded;
        }

        private void AccountChanged(object sender, EventArgs e)
        {
            if (sender == this.Session) this.Dispatcher.InvokeAsync(this.UpdateFields);
        }

        private void UpdateFields()
        {
            var saved = this.Session.GetAuthenticationSettings();
            string channel = this.Session.ChannelID ?? saved?.ChannelID;
            bool connected = !string.IsNullOrWhiteSpace(channel);
            this.Credentials.Visibility = connected ? Visibility.Collapsed : Visibility.Visible;
            this.Account.Visibility = connected ? Visibility.Visible : Visibility.Collapsed;
            this.Status.Text = string.IsNullOrWhiteSpace(this.Session.BotUsername) ? channel : channel + " · " + this.Session.BotUsername;
            if (connected)
            {
                this.Channel.Clear();
                this.Token.Clear();
            }
        }

        private async void ConnectClicked(object sender, RoutedEventArgs e)
        {
            this.IsEnabled = false;
            try
            {
                var result = await this.Session.ConnectChannel(this.Channel.Text, this.Token.Password);
                if (result.Success) this.UpdateFields();
                else
                {
                    this.Token.Clear();
                    this.UpdateFields();
                    await DialogHelper.ShowMessage(result.Message);
                }
            }
            finally { this.IsEnabled = true; }
        }

        private async void DisconnectClicked(object sender, RoutedEventArgs e)
        {
            this.IsEnabled = false;
            try { await this.Session.ForgetChannel(); this.UpdateFields(); }
            catch (Exception ex) { Logger.Log(ex); await DialogHelper.ShowMessage(Strings.SaveFailed); }
            finally { this.IsEnabled = true; }
        }
    }
}

using MaterialDesignThemes.Wpf;
using UIResources = MixItUp.Base.Resources;
using MixItUp.Base.Model.Settings;
using MixItUp.Base.Util;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace MixItUp.WPF.Controls.Dialogs
{
    public partial class TwitchAuthenticationMigrationDialogControl : UserControl
    {
        private readonly TwitchAuthenticationMigrationDialogModel model;
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private bool busy;
        private bool cancelling;

        public TwitchAuthenticationMigrationDialogControl(TwitchAuthenticationMigrationDialogModel model)
        {
            this.model = model;
            InitializeComponent();
            this.StreamerNameTextBlock.Text = model.StreamerName;
            this.BotNameTextBlock.Text = model.BotName;
            this.BotAccountPanel.Visibility = model.HasBot ? Visibility.Visible : Visibility.Collapsed;
            this.Unloaded += (sender, args) => this.cancellation.Cancel();
            this.UpdateState();
        }

        private async void StreamerLoginButton_Click(object sender, RoutedEventArgs e) { await this.Login(false); }
        private async void BotLoginButton_Click(object sender, RoutedEventArgs e) { await this.Login(true); }

        private async Task Login(bool bot)
        {
            if (this.busy) { return; }
            this.busy = true;
            this.ErrorTextBlock.Visibility = Visibility.Collapsed;
            this.UpdateState();
            try
            {
                Result result = await (bot ? this.model.LoginBot : this.model.LoginStreamer)(this.cancellation.Token);
                if (result.Success)
                {
                    if (bot) { this.model.BotAuthenticated = true; }
                    else { this.model.StreamerAuthenticated = true; }
                }
                else if (!this.cancelling)
                {
                    this.ErrorTextBlock.Text = result.Message;
                    this.ErrorTextBlock.Visibility = Visibility.Visible;
                }
            }
            catch (Exception)
            {
                if (!this.cancelling)
                {
                    this.ErrorTextBlock.Text = UIResources.TwitchMigrationFailed;
                    this.ErrorTextBlock.Visibility = Visibility.Visible;
                }
            }
            finally
            {
                this.busy = false;
                this.UpdateState();
                if (this.cancelling) { DialogHost.CloseDialogCommand.Execute(false, this); }
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.cancelling = true;
            this.cancellation.Cancel();
            if (!this.busy) { DialogHost.CloseDialogCommand.Execute(false, this); }
        }

        private void UpdateState()
        {
            this.StreamerLoginButton.IsEnabled = !this.busy && !this.model.StreamerAuthenticated;
            this.BotLoginButton.IsEnabled = !this.busy && !this.model.BotAuthenticated;
            this.StreamerStatusTextBlock.Text = this.model.StreamerAuthenticated ? UIResources.TwitchMigrationSignedIn : string.Empty;
            this.BotStatusTextBlock.Text = this.model.BotAuthenticated ? UIResources.TwitchMigrationSignedIn : string.Empty;
            this.ContinueButton.IsEnabled = !this.busy && this.model.CanContinue;
            this.LoginProgressBar.Visibility = this.busy ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}

using MixItUp.WPF.Services;
using System;
using System.Windows;
using System.Windows.Controls;

namespace MixItUp.WPF.Controls
{
    public class UpdateReadyButton : Button
    {
        public UpdateReadyButton()
        {
            this.Visibility = Visibility.Collapsed;
            this.Loaded += (_, _) =>
            {
                BackgroundUpdates.ReadyChanged += this.ReadyChanged;
                this.Refresh();
            };
            this.Unloaded += (_, _) => BackgroundUpdates.ReadyChanged -= this.ReadyChanged;
        }

        private void ReadyChanged(object sender, EventArgs args) => this.Dispatcher.InvokeAsync(this.Refresh);
        private void Refresh() => this.Visibility = BackgroundUpdates.IsReady ? Visibility.Visible : Visibility.Collapsed;

        protected override void OnClick()
        {
            base.OnClick();
            BackgroundUpdates.RestartIfReady();
        }
    }
}

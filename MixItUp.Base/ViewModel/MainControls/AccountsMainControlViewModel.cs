using MixItUp.Base.Model;
using MixItUp.Base.ViewModel.Accounts;

namespace MixItUp.Base.ViewModel.MainControls
{
    public class AccountsMainControlViewModel : WindowControlViewModelBase
    {
        public StreamingPlatformAccountControlViewModel Twitch { get; set; } = new StreamingPlatformAccountControlViewModel(StreamingPlatformTypeEnum.Twitch);

        public StreamingPlatformAccountControlViewModel YouTube { get; set; } = new StreamingPlatformAccountControlViewModel(StreamingPlatformTypeEnum.YouTube);

        public StreamingPlatformAccountControlViewModel Kick { get; set; } = new StreamingPlatformAccountControlViewModel(StreamingPlatformTypeEnum.Kick);

        public StreamingPlatformAccountControlViewModel Velora { get; set; } = new StreamingPlatformAccountControlViewModel(StreamingPlatformTypeEnum.Velora);

        public StreamingPlatformAccountControlViewModel VPZone { get; set; } = new StreamingPlatformAccountControlViewModel(StreamingPlatformTypeEnum.VPZone);
        public StreamingPlatformAccountControlViewModel Joystick { get; set; } = new StreamingPlatformAccountControlViewModel(StreamingPlatformTypeEnum.Joystick);

        public AccountsMainControlViewModel(MainWindowViewModel windowViewModel)
            : base(windowViewModel)
        {
            this.Twitch.StartLoadingOperationOccurred += (sender, eventArgs) => { this.StartLoadingOperation(); };
            this.Twitch.EndLoadingOperationOccurred += (sender, eventArgs) => { this.EndLoadingOperation(); };
            this.YouTube.StartLoadingOperationOccurred += (sender, eventArgs) => { this.StartLoadingOperation(); };
            this.YouTube.EndLoadingOperationOccurred += (sender, eventArgs) => { this.EndLoadingOperation(); };
            this.Kick.StartLoadingOperationOccurred += (sender, eventArgs) => { this.StartLoadingOperation(); };
            this.Kick.EndLoadingOperationOccurred += (sender, eventArgs) => { this.EndLoadingOperation(); };
            this.Velora.StartLoadingOperationOccurred += (sender, eventArgs) => { this.StartLoadingOperation(); };
            this.Velora.EndLoadingOperationOccurred += (sender, eventArgs) => { this.EndLoadingOperation(); };

            this.VPZone.StartLoadingOperationOccurred += (sender, eventArgs) => { this.StartLoadingOperation(); };
            this.Joystick.StartLoadingOperationOccurred += (sender, eventArgs) => { this.StartLoadingOperation(); };
            this.VPZone.EndLoadingOperationOccurred += (sender, eventArgs) => { this.EndLoadingOperation(); };
            this.Joystick.EndLoadingOperationOccurred += (sender, eventArgs) => { this.EndLoadingOperation(); };
        }
    }
}

using MixItUp.Base.Services;
using MixItUp.Base.Services.External;
using System;
using System.Threading;
using System.Windows.Input;

namespace MixItUp.Base.ViewModel.Services
{
    public class VelumStudioServiceControlViewModel : StreamingServiceControlViewModelBase
    {
        public const string DefaultWebSocketAddress = "ws://127.0.0.1:28710";
        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }
        public string WebSocketAddress { get; set; }
        public string CertificateFingerprint { get; set; }
        public Func<string> Token { get; set; }
        public override string WikiPageName => "velum-studio";

        public VelumStudioServiceControlViewModel() : base(Resources.VelumStudio)
        {
            this.WebSocketAddress = string.IsNullOrWhiteSpace(ChannelSession.Settings.VelumStudioWebSocketAddress)
                ? DefaultWebSocketAddress : ChannelSession.Settings.VelumStudioWebSocketAddress;
            this.CertificateFingerprint = ChannelSession.Settings.VelumStudioCertificateFingerprint;
            this.HelpCommand = this.CreateCommand(() => ServiceManager.Get<IProcessService>().LaunchLink("https://velumstudio.dev/docs/api/"));
            this.IsConnected = ServiceManager.Get<VelumStudioService>().IsConnected;
            this.ConnectCommand = this.CreateCommand(async () =>
            {
                var service = ServiceManager.Get<VelumStudioService>();
                ChannelSession.Settings.VelumStudioWebSocketAddress = this.WebSocketAddress?.Trim();
                ChannelSession.Settings.VelumStudioCertificateFingerprint = this.CertificateFingerprint?.Trim();
                ChannelSession.Settings.VelumStudioToken = this.Token?.Invoke() ?? ChannelSession.Settings.VelumStudioToken;
                var result = await service.ManualConnect(CancellationToken.None);
                if (result.Success)
                {
                    ChannelSession.Settings.VelumStudioEnabled = true;
                    this.IsConnected = true;
                    this.ChangeDefaultStreamingSoftware();
                }
                else { await this.ShowConnectFailureMessage(result); }
            });
            this.DisconnectCommand = this.CreateCommand(async () =>
            {
                await ServiceManager.Get<VelumStudioService>().Disable();
                this.IsConnected = false;
            });
        }
    }
}

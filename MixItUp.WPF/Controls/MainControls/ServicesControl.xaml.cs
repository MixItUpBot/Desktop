using MixItUp.Base.Util;
using MixItUp.WPF.Branding;
using MixItUp.WPF.Controls.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace MixItUp.WPF.Controls.MainControls
{
    /// <summary>
    /// Interaction logic for ServicesControl.xaml
    /// </summary>
    public partial class ServicesControl : MainControlBase
    {
        private ObservableCollection<UserControl> services = new ObservableCollection<UserControl>();

        public ServicesControl()
        {
            InitializeComponent();

            this.ServicesListView.ItemsSource = services;
        }

        protected override async Task InitializeInternal()
        {
            List<UserControl> services = new List<UserControl>();

            services.Add(new ServiceContainerControl(this.Window, new CrowdControlServiceControl()));
            services.Add(new ServiceContainerControl(this.Window, new DeveloperAPIServiceControl()));
            services.Add(new ServiceContainerControl(this.Window, new DiscordServiceControl()));
            services.Add(new ServiceContainerControl(this.Window, new DonorDriveServiceControl()));
            services.Add(new ServiceContainerControl(this.Window, new FourthwallServiceControl()));
            services.Add(new ServiceContainerControl(this.Window, new IFTTTServiceControl()));
            services.Add(new ServiceContainerControl(this.Window, new JustGivingServiceControl()));
            services.Add(new ServiceContainerControl(this.Window, new KoFiServiceControl()));
            services.Add(new ServiceContainerControl(this.Window, new LoupeDeckServiceControl()));
            services.Add(new ServiceContainerControl(this.Window, new LumiaStreamServiceControl()));
            services.Add(new ServiceContainerControl(this.Window, new MCPServiceControl()));
            services.Add(new ServiceContainerControl(this.Window, new MeldStudioServiceControl()));
            services.Add(new ServiceContainerControl(this.Window, new VelumStudioServiceControl()));
            services.Add(new ServiceContainerControl(this.Window, new MtionStudioServiceControl()));
            services.Add(new ServiceContainerControl(this.Window, new OBSStudioServiceControl()));
            services.Add(new ServiceContainerControl(this.Window, new OverlayServiceControl()));
            services.Add(new ServiceContainerControl(this.Window, new PallyServiceControl()));
            services.Add(new ServiceContainerControl(this.Window, new PatreonServiceControl()));
            services.Add(new ServiceContainerControl(this.Window, new PixelChatServiceControl()));
            services.Add(new ServiceContainerControl(this.Window, new PolyPopServiceControl()));
            services.Add(new ServiceContainerControl(this.Window, new PulsoidServiceControl()));
            services.Add(new ServiceContainerControl(this.Window, new RahiTuberServiceControl()));
            services.Add(new ServiceContainerControl(this.Window, new RainmakerServiceControl()));
            services.Add(new ServiceContainerControl(this.Window, new SAMMIServiceControl()));
            services.Add(new ServiceContainerControl(this.Window, new StreamAvatarsServiceControl()));
            services.Add(new ServiceContainerControl(this.Window, new StreamDeckServiceControl()));
            services.Add(new ServiceContainerControl(this.Window, new StreamElementsServiceControl()));
            services.Add(new ServiceContainerControl(this.Window, new StreamlabsServiceControl()));
            services.Add(new ServiceContainerControl(this.Window, new StreamlabsDesktopServiceControl()));
            services.Add(new ServiceContainerControl(this.Window, new StreamlootsServiceControl()));

            ServiceCategoryControl ttsCategory = new ServiceCategoryControl(this.Window, MixItUp.Base.Resources.TextToSpeech, Features.TextToSpeech);
            ttsCategory.AddService(new AmazonPollyServiceControl());
            ttsCategory.AddService(new GoogleCloudTTSServiceControl());
            ttsCategory.AddService(new MicrosoftAzureSpeechServiceControl());
            ttsCategory.AddService(new ResponsiveVoiceServiceControl());
            ttsCategory.AddService(new TTSMonsterServiceControl());
            ttsCategory.AddService(new TTSMonsterAPIServiceControl());
            services.Add(ttsCategory);

            services.Add(new ServiceContainerControl(this.Window, new ThroneServiceControl()));
            services.Add(new ServiceContainerControl(this.Window, new TiltifyServiceControl()));
            services.Add(new ServiceContainerControl(this.Window, new TipeeeStreamServiceControl()));
            services.Add(new ServiceContainerControl(this.Window, new TITSServiceControl()));
            services.Add(new ServiceContainerControl(this.Window, new TreatStreamServiceControl()));
            services.Add(new ServiceContainerControl(this.Window, new VConnectServiceControl()));
            services.Add(new ServiceContainerControl(this.Window, new VeadotubeServiceControl()));
            services.Add(new ServiceContainerControl(this.Window, new VoicemodServiceControl()));
            services.Add(new ServiceContainerControl(this.Window, new VTSPogServiceControl()));
            services.Add(new ServiceContainerControl(this.Window, new VTubeStudioServiceControl()));
            services.Add(new ServiceContainerControl(this.Window, new XSplitServiceControl()));

            this.services.ClearAndAddRange(services);

            await base.InitializeInternal();
        }

        private async void ReconnectServicesButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            this.ReconnectServicesButton.IsEnabled = false;
            try
            {
                await this.Window.RunAsyncOperation(async () =>
                {
                    IReadOnlyList<string> failures = await MixItUp.Base.ChannelSession.ReconnectServices();
                    if (failures.Count > 0)
                    {
                        await DialogHelper.ShowMessage(string.Format(MixItUp.Base.Resources.ConnectedServicesFailed, string.Join(System.Environment.NewLine, failures)));
                    }
                    await this.InitializeInternal();
                });
            }
            finally
            {
                this.ReconnectServicesButton.IsEnabled = true;
            }
        }

        protected override async Task OnVisibilityChanged()
        {
            await this.InitializeInternal();
        }
    }
}

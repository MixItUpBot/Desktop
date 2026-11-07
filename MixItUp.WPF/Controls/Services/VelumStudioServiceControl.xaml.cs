using MixItUp.Base.ViewModel.Services;
using MixItUp.Base;
using MixItUp.WPF.Branding;

namespace MixItUp.WPF.Controls.Services
{
    public partial class VelumStudioServiceControl : ServiceControlBase
    {
        public override Brand Brand => Brands.VelumStudio;
        public VelumStudioServiceControl()
        {
            this.DataContext = this.ViewModel = new VelumStudioServiceControlViewModel();
            this.InitializeComponent();
            this.TokenPasswordBox.Password = ChannelSession.Settings.VelumStudioToken ?? string.Empty;
            ((VelumStudioServiceControlViewModel)this.ViewModel).Token = () => this.Dispatcher.Invoke(() => this.TokenPasswordBox.Password);
        }
    }
}

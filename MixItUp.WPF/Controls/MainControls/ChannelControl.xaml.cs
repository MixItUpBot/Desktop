using MixItUp.Base.ViewModel.MainControls;
using MixItUp.Base.ViewModel;
using System.Threading.Tasks;
using MixItUp.Base.Services;

namespace MixItUp.WPF.Controls.MainControls
{
    /// <summary>
    /// Interaction logic for ChannelControl.xaml
    /// </summary>
    public partial class ChannelControl : MainControlBase
    {
        private ChannelMainControlViewModel viewModel;

        public ChannelControl()
        {
            InitializeComponent();
            this.Loaded += (_, _) => ChannelChatSessionBase.AccountChanged += this.AccountChanged;
            this.Unloaded += (_, _) => ChannelChatSessionBase.AccountChanged -= this.AccountChanged;

        }

        private void AccountChanged(object sender, System.EventArgs e)
        {
            this.Dispatcher.InvokeAsync(async () =>
            {
                if (this.IsVisible && this.viewModel != null)
                    await this.viewModel.RefreshAccount((ChannelChatSessionBase)sender);
            });
        }

        protected override async Task InitializeInternal()
        {
            this.DataContext = this.viewModel = new ChannelMainControlViewModel((MainWindowViewModel)this.Window.ViewModel);
            await this.viewModel.OnOpen();
            await base.InitializeInternal();
        }

        protected override async Task OnVisibilityChanged()
        {
            await this.viewModel.OnVisible();
        }
    }
}
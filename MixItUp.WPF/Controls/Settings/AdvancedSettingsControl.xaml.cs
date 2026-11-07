using MixItUp.Base.ViewModel.Settings;
using MixItUp.Base.Util;
using MixItUp.WPF.Windows.MissingFilesCheck;
using System.Threading.Tasks;
using System.Windows;

namespace MixItUp.WPF.Controls.Settings
{
    /// <summary>
    /// Interaction logic for AdvancedSettingsControl.xaml
    /// </summary>
    public partial class AdvancedSettingsControl : SettingsControlBase
    {
        private AdvancedSettingsControlViewModel viewModel;

        public AdvancedSettingsControl()
        {
            InitializeComponent();

            this.DataContext = this.viewModel = new AdvancedSettingsControlViewModel();
        }

        protected override async Task InitializeInternal()
        {
            await this.viewModel.OnOpen();
            await base.InitializeInternal();
        }

        protected override async Task OnVisibilityChanged()
        {
            await this.InitializeInternal();
        }

        private void Diagnostics_Click(object sender, RoutedEventArgs e)
        {
            new MixItUp.WPF.Windows.DiagnosticsWindow { Owner = System.Windows.Window.GetWindow(this) }.ShowDialog();
        }

        private async void MissingFilesButton_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ShowConfirmation(MixItUp.Base.Resources.MissingFilesCheckDialog))
            {
                MissingFilesCheckWindow window = new MissingFilesCheckWindow();
                window.Show();
            }
        }
    }
}

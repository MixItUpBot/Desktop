using MixItUp.Base.Model.Commands;
using MixItUp.Base.Services;
using MixItUp.Base.ViewModel;
using MixItUp.Base.ViewModel.MainControls;
using MixItUp.WPF.Util;
using MixItUp.WPF.Windows.Commands;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace MixItUp.WPF.Controls.MainControls
{
    /// <summary>
    /// Interaction logic for MusicPlayerControl.xaml
    /// </summary>
    public partial class MusicPlayerControl : MainControlBase
    {
        private MusicPlayerMainControlViewModel viewModel;
        private System.Windows.Data.ListCollectionView queueView;

        public MusicPlayerControl()
        {
            InitializeComponent();
        }

        protected override async Task InitializeInternal()
        {
            this.DataContext = this.viewModel = new MusicPlayerMainControlViewModel((MainWindowViewModel)this.Window.ViewModel);
            this.queueView = new System.Windows.Data.ListCollectionView(this.viewModel.Songs);
            this.queueView.Filter = item => MatchesQueueSearch(item as MusicPlayerSong, this.QueueSearchTextBox.Text);
            this.QueueListBox.ItemsSource = this.queueView;

            GongSolutions.Wpf.DragDrop.DragDrop.SetIsDragSource(this.QueueListBox, true);
            GongSolutions.Wpf.DragDrop.DragDrop.SetIsDropTarget(this.QueueListBox, true);
            GongSolutions.Wpf.DragDrop.DragDrop.SetDropHandler(this.QueueListBox, MusicPlayerQueueDragDropHandler.Instance);

            this.TrackScrubber.PreviewMouseDown += TrackScrubber_PreviewMouseDown;
            this.TrackScrubber.PreviewMouseUp += TrackScrubber_PreviewMouseUp;
            this.TrackScrubber.ValueChanged += TrackScrubber_ValueChanged;
            this.viewModel.PropertyChanged += ViewModel_PropertyChanged;

            await this.viewModel.OnOpen();
            await base.InitializeInternal();
        }

        protected override async Task OnVisibilityChanged()
        {
            await this.viewModel.OnVisible();
        }

        internal static bool MatchesQueueSearch(MusicPlayerSong song, string search)
        {
            string query = search?.Trim() ?? string.Empty;
            return song != null && (query.Length == 0
                || (song.Title?.Contains(query, System.StringComparison.OrdinalIgnoreCase) ?? false)
                || (song.Artist?.Contains(query, System.StringComparison.OrdinalIgnoreCase) ?? false)
                || (System.IO.Path.GetFileName(song.FilePath)?.Contains(query, System.StringComparison.OrdinalIgnoreCase) ?? false));
        }

        private void QueueSearchTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (this.queueView != null)
            {
                this.queueView.Refresh();
                bool canReorder = string.IsNullOrWhiteSpace(this.QueueSearchTextBox.Text);
                GongSolutions.Wpf.DragDrop.DragDrop.SetIsDragSource(this.QueueListBox, canReorder);
                GongSolutions.Wpf.DragDrop.DragDrop.SetIsDropTarget(this.QueueListBox, canReorder);
            }
        }

        private void OnSongChangedCommand_EditClicked(object sender, RoutedEventArgs e)
        {
            CommandEditorWindow window = CommandEditorWindow.GetCommandEditorWindow(FrameworkElementHelpers.GetDataContext<CustomCommandModel>(sender));
            window.CommandSaved += (object s, CommandModelBase command) => { this.viewModel.OnSongChangedCommand = command; };
            window.ForceShow();
        }

        private async void QueueSongBody_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && sender is FrameworkElement element && element.DataContext is MusicPlayerSong song)
            {
                e.Handled = true;
                await ServiceManager.Get<IMusicPlayerService>().PlaySong(song);
            }
        }

        private void TrackScrubber_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (this.viewModel != null)
            {
                this.viewModel.IsScrubbing = true;
                this.viewModel.ScrubPosition = (int)this.TrackScrubber.Value;
            }
        }

        private async void TrackScrubber_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (this.viewModel != null && this.viewModel.IsScrubbing)
            {
                await this.viewModel.SeekTo((int)this.TrackScrubber.Value);
                this.viewModel.IsScrubbing = false;
            }
        }

        private void TrackScrubber_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.viewModel != null && this.viewModel.IsScrubbing)
            {
                this.viewModel.ScrubPosition = (int)e.NewValue;
            }
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(MusicPlayerMainControlViewModel.TrackPosition)))
            {
                this.Dispatcher.Invoke(() =>
                {
                    if (this.viewModel != null && !this.viewModel.IsScrubbing)
                    {
                        this.TrackScrubber.Value = this.viewModel.TrackPosition;
                    }
                });
            }
        }
    }
}

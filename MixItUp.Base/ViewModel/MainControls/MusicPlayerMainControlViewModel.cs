using MixItUp.Base.Model.Commands;
using MixItUp.Base.Services;
using MixItUp.Base.Util;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MixItUp.Base.ViewModel.MainControls
{
    public class MusicPlayerMainControlViewModel : WindowControlViewModelBase
    {
        public ThreadSafeObservableCollection<MusicPlayerSong> Songs { get { return ServiceManager.Get<IMusicPlayerService>().Songs; } }

        public bool MusicLoaded { get { return this.Songs != null && this.Songs.Count > 0; } }

        public bool MusicNotLoaded { get { return !this.MusicLoaded; } }

        public MusicPlayerSong CurrentSong { get { return ServiceManager.Get<IMusicPlayerService>().CurrentSong; } }

        public string CurrentlyPlayingSong
        {
            get
            {
                MusicPlayerSong song = this.CurrentSong;
                if (song != null)
                {
                    return song.ToString();
                }
                else
                {
                    return Resources.None;
                }
            }
        }

        public bool IsShuffleOn { get { return ServiceManager.Get<IMusicPlayerService>().Shuffle; } }

        public bool IsShuffleOff { get { return !this.IsShuffleOn; } }

        public bool IsRepeatOn { get { return ServiceManager.Get<IMusicPlayerService>().Repeat && !this.IsRepeatOne; } }

        public bool IsRepeatOne { get { return ServiceManager.Get<IMusicPlayerService>().RepeatOne; } }

        public bool IsRepeatOff { get { return !ServiceManager.Get<IMusicPlayerService>().Repeat; } }

        public int TrackDuration { get { return Math.Max((int)ServiceManager.Get<IMusicPlayerService>().CurrentDuration.TotalSeconds, 0); } }

        public int TrackPosition { get { return Math.Max((int)ServiceManager.Get<IMusicPlayerService>().CurrentPosition.TotalSeconds, 0); } }

        public string TrackPositionString { get { return this.FormatSeconds(this.IsScrubbing ? this.ScrubPosition : this.TrackPosition); } }

        public string TrackDurationString { get { return this.FormatSeconds(this.TrackDuration); } }

        public bool IsScrubbing
        {
            get { return this.isScrubbing; }
            set
            {
                this.isScrubbing = value;
                this.NotifyPropertyChanged();
                this.NotifyPropertyChanged(nameof(this.TrackPositionString));
            }
        }
        private bool isScrubbing;

        public int ScrubPosition
        {
            get { return this.scrubPosition; }
            set
            {
                this.scrubPosition = value;
                this.NotifyPropertyChanged();
                this.NotifyPropertyChanged(nameof(this.TrackPositionString));
            }
        }
        private int scrubPosition;

        public ICommand PreviousCommand { get; private set; }
        public ICommand PlayPauseCommand { get; private set; }
        public ICommand StopCommand { get; private set; }
        public ICommand NextCommand { get; private set; }
        public ICommand ToggleShuffleCommand { get; private set; }
        public ICommand ToggleRepeatCommand { get; private set; }

        public ICommand AddFileToQueueCommand { get; private set; }
        public ICommand AddFolderToQueueCommand { get; private set; }
        public ICommand AddPlaylistToQueueCommand { get; private set; }
        public ICommand ExportQueueCommand { get; private set; }
        public ICommand ClearQueueCommand { get; private set; }
        public ICommand RemoveSongCommand { get; private set; }
        public ICommand PlaySongCommand { get; private set; }

        public int Volume
        {
            get { return ServiceManager.Get<IMusicPlayerService>().Volume; }
            set
            {
                _ = ServiceManager.Get<IMusicPlayerService>().ChangeVolume(value);
                this.NotifyPropertyChanged();
            }
        }

        public ObservableCollection<string> AudioDevices { get; set; } = new ObservableCollection<string>();

        public string SelectedAudioDevice
        {
            get { return ChannelSession.Settings.MusicPlayerAudioOutput; }
            set
            {
                _ = ServiceManager.Get<IMusicPlayerService>().SetAudioOutputDevice(value);
                this.NotifyPropertyChanged();
            }
        }

        public CommandModelBase OnSongChangedCommand
        {
            get { return this.onSongChangedCommand; }
            set
            {
                this.onSongChangedCommand = value;
                this.NotifyPropertyChanged();
            }
        }
        private CommandModelBase onSongChangedCommand;

        private CancellationTokenSource playbackStatusCancellationTokenSource = new CancellationTokenSource();

        public MusicPlayerMainControlViewModel(MainWindowViewModel windowViewModel)
            : base(windowViewModel)
        {
            ServiceManager.Get<IMusicPlayerService>().SongChanged += MusicPlayerMainControlViewModel_SongChanged;
            ServiceManager.Get<IMusicPlayerService>().QueueChanged += MusicPlayerMainControlViewModel_QueueChanged;

            this.OnSongChangedCommand = ChannelSession.Settings.GetCommand(ChannelSession.Settings.MusicPlayerOnSongChangedCommandID);

            this.AudioDevices.AddRange(ServiceManager.Get<IAudioService>().GetSelectableAudioDevices(retainedDevice: ChannelSession.Settings.MusicPlayerAudioOutput));
            this.AudioDevices.Remove(ServiceManager.Get<IAudioService>().MixItUpOverlay);
            if (ChannelSession.Settings.MusicPlayerAudioOutput == null)
            {
                ChannelSession.Settings.MusicPlayerAudioOutput = ServiceManager.Get<IAudioService>().DefaultAudioDevice;
            }
            this.NotifyPropertyChanged(nameof(this.SelectedAudioDevice));

            this.PreviousCommand = this.CreateCommand(async () =>
            {
                await ServiceManager.Get<IMusicPlayerService>().Previous();
            });

            this.PlayPauseCommand = this.CreateCommand(async () =>
            {
                if (ServiceManager.Get<IMusicPlayerService>().State == MusicPlayerState.Playing)
                {
                    await ServiceManager.Get<IMusicPlayerService>().Pause();
                }
                else
                {
                    await ServiceManager.Get<IMusicPlayerService>().Play();
                }
            });

            this.StopCommand = this.CreateCommand(async () =>
            {
                await ServiceManager.Get<IMusicPlayerService>().Stop();
                this.RefreshPlaybackProperties();
            });

            this.NextCommand = this.CreateCommand(async () =>
            {
                await ServiceManager.Get<IMusicPlayerService>().Next();
            });

            this.ToggleShuffleCommand = this.CreateCommand(async () =>
            {
                await ServiceManager.Get<IMusicPlayerService>().SetShuffle(!ServiceManager.Get<IMusicPlayerService>().Shuffle);
                this.NotifyPropertyChanged(nameof(this.IsShuffleOn));
                this.NotifyPropertyChanged(nameof(this.IsShuffleOff));
            });

            this.ToggleRepeatCommand = this.CreateCommand(async () =>
            {
                IMusicPlayerService player = ServiceManager.Get<IMusicPlayerService>();
                if (player.RepeatOne)
                {
                    await player.SetRepeat(false);
                }
                else if (player.Repeat)
                {
                    await player.SetRepeatOne(true);
                }
                else
                {
                    await player.SetRepeat(true);
                }
                this.NotifyPropertyChanged(nameof(this.IsRepeatOn));
                this.NotifyPropertyChanged(nameof(this.IsRepeatOff));
                this.NotifyPropertyChanged(nameof(this.IsRepeatOne));
            });

            this.AddFileToQueueCommand = this.CreateCommand(async () =>
            {
                string filePath = ServiceManager.Get<IFileService>().ShowOpenFileDialog(Resources.SoundFileFormatFilter);
                if (!string.IsNullOrWhiteSpace(filePath))
                {
                    await ServiceManager.Get<IMusicPlayerService>().AddFilesToQueue(new string[] { filePath });
                }
            });

            this.AddFolderToQueueCommand = this.CreateCommand(async () =>
            {
                string folderPath = ServiceManager.Get<IFileService>().ShowOpenFolderDialog();
                if (!string.IsNullOrWhiteSpace(folderPath))
                {
                    await ServiceManager.Get<IMusicPlayerService>().AddFolderToQueue(folderPath);
                }
            });

            this.AddPlaylistToQueueCommand = this.CreateCommand(async () =>
            {
                string playlistPath = ServiceManager.Get<IFileService>().ShowOpenFileDialog(Resources.MusicPlayerPlaylistFileFormatFilter);
                if (!string.IsNullOrWhiteSpace(playlistPath))
                {
                    await ServiceManager.Get<IMusicPlayerService>().AddPlaylistToQueue(playlistPath);
                }
            });

            this.ExportQueueCommand = this.CreateCommand(async () =>
            {
                string filePath = ServiceManager.Get<IFileService>().ShowSaveFileDialog("queue.m3u", Resources.MusicPlayerM3UPlaylistFileFormatFilter);
                if (!string.IsNullOrWhiteSpace(filePath))
                {
                    await ServiceManager.Get<IMusicPlayerService>().ExportQueueToPlaylist(filePath);
                }
            });

            this.ClearQueueCommand = this.CreateCommand(async () =>
            {
                if (await DialogHelper.ShowConfirmation(Resources.MusicPlayerClearQueueConfirmation))
                {
                    await ServiceManager.Get<IMusicPlayerService>().ClearQueue();
                }
            });

            this.RemoveSongCommand = this.CreateCommand(async (parameter) =>
            {
                if (parameter is MusicPlayerSong song)
                {
                    await ServiceManager.Get<IMusicPlayerService>().RemoveFromQueue(song);
                }
            });

            this.PlaySongCommand = this.CreateCommand(async (parameter) =>
            {
                if (parameter is MusicPlayerSong song)
                {
                    await ServiceManager.Get<IMusicPlayerService>().PlaySong(song);
                }
            });

            AsyncRunner.RunAsyncBackground(this.PlaybackStatusBackground, this.playbackStatusCancellationTokenSource.Token);
        }

        public async Task SeekTo(int seconds)
        {
            await ServiceManager.Get<IMusicPlayerService>().Seek(TimeSpan.FromSeconds(seconds));
            this.RefreshPlaybackProperties();
        }

        protected override async Task OnOpenInternal()
        {
            if (this.Songs.Count == 0)
            {
                _ = ServiceManager.Get<IMusicPlayerService>().LoadSongs();
            }
            await base.OnOpenInternal();
        }

        protected override async Task OnVisibleInternal()
        {
            await base.OnVisibleInternal();
        }

        private async Task PlaybackStatusBackground(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(500);

                if (ServiceManager.Get<IMusicPlayerService>().State == MusicPlayerState.Playing)
                {
                    this.RefreshPlaybackProperties();
                }
            }
        }

        private void RefreshPlaybackProperties()
        {
            this.NotifyPropertyChanged(nameof(this.TrackPosition));
            this.NotifyPropertyChanged(nameof(this.TrackDuration));
            this.NotifyPropertyChanged(nameof(this.TrackPositionString));
            this.NotifyPropertyChanged(nameof(this.TrackDurationString));
        }

        private string FormatSeconds(int totalSeconds)
        {
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            return $"{minutes}:{seconds:D2}";
        }

        private void MusicPlayerMainControlViewModel_SongChanged(object sender, System.EventArgs e)
        {
            this.NotifyPropertyChanged(nameof(this.CurrentSong));
            this.NotifyPropertyChanged(nameof(this.CurrentlyPlayingSong));
            this.NotifyPropertyChanged(nameof(this.MusicLoaded));
            this.NotifyPropertyChanged(nameof(this.MusicNotLoaded));
            this.RefreshPlaybackProperties();
        }

        private void MusicPlayerMainControlViewModel_QueueChanged(object sender, System.EventArgs e)
        {
            this.NotifyPropertyChanged(nameof(this.CurrentSong));
            this.NotifyPropertyChanged(nameof(this.CurrentlyPlayingSong));
            this.NotifyPropertyChanged(nameof(this.MusicLoaded));
            this.NotifyPropertyChanged(nameof(this.MusicNotLoaded));
            this.RefreshPlaybackProperties();
        }
    }
}

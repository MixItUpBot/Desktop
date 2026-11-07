using ATL;
using ATL.Playlist;
using MixItUp.Base;
using MixItUp.Base.Model;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Services;
using MixItUp.Base.Util;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.WPF.Services
{
    public class WindowsMusicPlayerService : IMusicPlayerService
    {
        public const string M3UPlaylistFileExtension = ".m3u";
        public const string M3U8PlaylistFileExtension = ".m3u8";

        private const int MaximumHistorySize = 100;

        public event EventHandler SongChanged = delegate { };
        public event EventHandler QueueChanged = delegate { };

        public MusicPlayerState State { get; private set; }

        public int Volume
        {
            get { return ChannelSession.Settings.MusicPlayerVolume; }
            set { ChannelSession.Settings.MusicPlayerVolume = value; }
        }

        public MusicPlayerSong CurrentSong
        {
            get
            {
                if (0 <= this.currentSongIndex && this.currentSongIndex < this.songs.Count)
                {
                    return this.songs[this.currentSongIndex];
                }
                return null;
            }
        }

        public ThreadSafeObservableCollection<MusicPlayerSong> Songs { get { return this.songs; } }

        public bool Shuffle { get { return ChannelSession.Settings.MusicPlayerShuffle; } }

        public bool Repeat { get { return ChannelSession.Settings.MusicPlayerRepeat; } }

        public bool RepeatOne { get { return this.Repeat && ChannelSession.Settings.MusicPlayerRepeatOne; } }

        public TimeSpan CurrentPosition
        {
            get
            {
                try
                {
                    WaveStream waveStream = this.currentWaveStream;
                    if (waveStream != null && this.State != MusicPlayerState.Stopped)
                    {
                        return waveStream.CurrentTime;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
                return TimeSpan.Zero;
            }
        }

        public TimeSpan CurrentDuration
        {
            get
            {
                try
                {
                    WaveStream waveStream = this.currentWaveStream;
                    if (waveStream != null && this.State != MusicPlayerState.Stopped)
                    {
                        return waveStream.TotalTime;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }

                MusicPlayerSong song = this.CurrentSong;
                return (song != null) ? TimeSpan.FromSeconds(song.Length) : TimeSpan.Zero;
            }
        }

        private ThreadSafeObservableCollection<MusicPlayerSong> songs = new ThreadSafeObservableCollection<MusicPlayerSong>();
        private int currentSongIndex = 0;
        private bool stopOnSpecificSongCompletion = false;
        private MusicPlayerSong stopOnSpecificSong = null;

        private HashSet<MusicPlayerSong> playedSongs = new HashSet<MusicPlayerSong>();
        private List<MusicPlayerSong> playbackHistory = new List<MusicPlayerSong>();
        private bool playbackSessionActive = false;

        private CancellationTokenSource backgroundPlayThreadTokenSource = new CancellationTokenSource();
        private WaveOutEvent currentWaveOutEvent;
        private WaveStream currentWaveStream;

        private SemaphoreSlim sempahore = new SemaphoreSlim(1);

        private readonly SemaphoreSlim albumArtLock = new SemaphoreSlim(1, 1);
        private string albumArtCacheKey;
        private string albumArtCacheValue = string.Empty;

        public async Task<string> GetAlbumArt(MusicPlayerSong song)
        {
            if (string.IsNullOrEmpty(song?.FilePath)) { return string.Empty; }
            await this.albumArtLock.WaitAsync();
            try
            {
                FileInfo file = new FileInfo(song.FilePath);
                if (!file.Exists) { return string.Empty; }
                string key = file.FullName + "|" + file.LastWriteTimeUtc.Ticks + "|" + file.Length;
                if (key != this.albumArtCacheKey)
                {
                    string image = await Task.Run(() =>
                    {
                        Track track = new Track(file.FullName);
                        PictureInfo picture = track.EmbeddedPictures?.FirstOrDefault(p => p.PicType == PictureInfo.PIC_TYPE.Front) ?? track.EmbeddedPictures?.FirstOrDefault();
                        if (picture?.PictureData == null || picture.PictureData.Length == 0 || picture.MimeType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) != true)
                        {
                            return string.Empty;
                        }
                        return "data:" + picture.MimeType + ";base64," + Convert.ToBase64String(picture.PictureData);
                    });
                    this.albumArtCacheKey = key;
                    this.albumArtCacheValue = image;
                }
                return this.albumArtCacheValue;
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                return string.Empty;
            }
            finally { this.albumArtLock.Release(); }
        }

        public async Task Play()
        {
            if (this.songs.Count == 0)
            {
                await this.LoadSongs();
            }

            if (this.songs.Count > 0)
            {
                if (this.State == MusicPlayerState.Paused)
                {
                    try
                    {
                        await this.sempahore.WaitAsync();

                        this.State = MusicPlayerState.Playing;
                        if (this.currentWaveOutEvent != null)
                        {
                            this.currentWaveOutEvent.Play();
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(ex);
                    }
                    finally
                    {
                        this.sempahore.Release();
                    }
                }
                else if (this.State == MusicPlayerState.Stopped)
                {
                    try
                    {
                        await this.sempahore.WaitAsync();

                        this.State = MusicPlayerState.Playing;
                        this.PlayInternal(this.CurrentSong);
                        this.playbackSessionActive = true;
                        if (this.Shuffle && this.CurrentSong != null)
                        {
                            this.playedSongs.Add(this.CurrentSong);
                        }
                        this.PersistCurrentIndex();
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(ex);
                    }
                    finally
                    {
                        this.sempahore.Release();
                    }

                    DispatcherHelper.Dispatcher.Invoke(() => this.SongChanged.Invoke(this, new EventArgs()));

                    await ServiceManager.Get<CommandService>().Queue(ChannelSession.Settings.MusicPlayerOnSongChangedCommandID, new CommandParametersModel(ChannelSession.User, platform: StreamingPlatformTypeEnum.All));
                }
            }
        }

        public async Task Pause()
        {
            if (this.State == MusicPlayerState.Playing)
            {
                try
                {
                    await this.sempahore.WaitAsync();

                    this.State = MusicPlayerState.Paused;
                    if (this.currentWaveOutEvent != null)
                    {
                        this.currentWaveOutEvent.Pause();
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
                finally
                {
                    this.sempahore.Release();
                }
            }
        }

        public async Task Stop()
        {
            try
            {
                await this.sempahore.WaitAsync();

                this.State = MusicPlayerState.Stopped;

                if (this.currentWaveOutEvent != null)
                {
                    this.currentWaveOutEvent.Stop();
                }
                this.currentWaveOutEvent = null;
                this.currentWaveStream = null;

                if (this.backgroundPlayThreadTokenSource != null)
                {
                    this.backgroundPlayThreadTokenSource.Cancel();
                }
                this.backgroundPlayThreadTokenSource = null;
                this.stopOnSpecificSongCompletion = false;
                this.stopOnSpecificSong = null;
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            finally
            {
                this.sempahore.Release();
            }
        }

        public async Task Next()
        {
            MusicPlayerSong previousSong = this.CurrentSong;

            await this.Stop();

            bool startPlaying = false;
            try
            {
                await this.sempahore.WaitAsync();

                if (this.songs.Count > 0)
                {
                    if (previousSong != null)
                    {
                        this.AddToPlaybackHistory(previousSong);
                    }

                    if (this.Shuffle)
                    {
                        startPlaying = this.PickNextShuffleSong();
                    }
                    else
                    {
                        this.currentSongIndex++;
                        if (this.currentSongIndex >= this.songs.Count)
                        {
                            this.currentSongIndex = 0;
                            startPlaying = this.Repeat;
                        }
                        else
                        {
                            startPlaying = true;
                        }
                    }
                    this.PersistCurrentIndex();
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            finally
            {
                this.sempahore.Release();
            }

            if (startPlaying)
            {
                await this.Play();
            }
            else
            {
                DispatcherHelper.Dispatcher.Invoke(() => this.SongChanged.Invoke(this, new EventArgs()));
            }
        }

        public async Task Previous()
        {
            await this.Stop();

            try
            {
                await this.sempahore.WaitAsync();

                if (this.songs.Count > 0)
                {
                    if (this.Shuffle)
                    {
                        while (this.playbackHistory.Count > 0)
                        {
                            MusicPlayerSong song = this.playbackHistory[this.playbackHistory.Count - 1];
                            this.playbackHistory.RemoveAt(this.playbackHistory.Count - 1);

                            int index = this.songs.IndexOf(song);
                            if (index >= 0)
                            {
                                this.currentSongIndex = index;
                                this.playedSongs.Remove(song);
                                break;
                            }
                        }
                    }
                    else
                    {
                        this.currentSongIndex--;
                        if (this.currentSongIndex < 0)
                        {
                            this.currentSongIndex = this.Repeat ? Math.Max(this.songs.Count - 1, 0) : 0;
                        }
                    }
                    this.PersistCurrentIndex();
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            finally
            {
                this.sempahore.Release();
            }

            await this.Play();
        }

        public async Task ChangeVolume(int amount)
        {
            try
            {
                await this.sempahore.WaitAsync();

                this.Volume = amount;
                if (this.currentWaveStream != null && this.currentWaveStream is AudioFileReader)
                {
                    ((AudioFileReader)this.currentWaveStream).Volume = (ServiceManager.Get<IAudioService>() as WindowsAudioService).ConvertVolumeAmount(this.Volume);
                }
                else if (this.currentWaveOutEvent != null)
                {
                    this.currentWaveOutEvent.Volume = (ServiceManager.Get<IAudioService>() as WindowsAudioService).ConvertVolumeAmount(this.Volume);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            finally
            {
                this.sempahore.Release();
            }
        }

        public async Task SetAudioOutputDevice(string deviceName)
        {
            if (string.Equals(ChannelSession.Settings.MusicPlayerAudioOutput, deviceName, StringComparison.Ordinal))
            {
                return;
            }

            // Assigned before the first await so that the settings value is already current when the
            // view model raises its property change notification, otherwise the bound selection reverts.
            ChannelSession.Settings.MusicPlayerAudioOutput = deviceName;

            try
            {
                await this.sempahore.WaitAsync();

                this.SwitchOutputDeviceInternal();
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            finally
            {
                this.sempahore.Release();
            }
        }

        public async Task SetShuffle(bool enabled)
        {
            try
            {
                await this.sempahore.WaitAsync();

                ChannelSession.Settings.MusicPlayerShuffle = enabled;
                this.playedSongs.Clear();
                this.playbackHistory.Clear();
                if (enabled && this.CurrentSong != null && this.State != MusicPlayerState.Stopped)
                {
                    this.playedSongs.Add(this.CurrentSong);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            finally
            {
                this.sempahore.Release();
            }
        }

        public Task SetRepeat(bool enabled)
        {
            ChannelSession.Settings.MusicPlayerRepeat = enabled;
            ChannelSession.Settings.MusicPlayerRepeatOne = false;
            return Task.CompletedTask;
        }

        public Task SetRepeatOne(bool enabled)
        {
            ChannelSession.Settings.MusicPlayerRepeatOne = enabled;
            if (enabled)
            {
                ChannelSession.Settings.MusicPlayerRepeat = true;
            }
            return Task.CompletedTask;
        }

        public async Task Seek(TimeSpan position)
        {
            try
            {
                await this.sempahore.WaitAsync();

                WaveStream waveStream = this.currentWaveStream;
                if (waveStream != null && this.State != MusicPlayerState.Stopped)
                {
                    if (position < TimeSpan.Zero)
                    {
                        position = TimeSpan.Zero;
                    }
                    else if (position > waveStream.TotalTime)
                    {
                        position = waveStream.TotalTime;
                    }
                    waveStream.CurrentTime = position;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            finally
            {
                this.sempahore.Release();
            }
        }

        public async Task ChangeFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                return;
            }

            await this.Stop();

            try
            {
                await this.sempahore.WaitAsync();

                this.ClearQueueInternal();
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            finally
            {
                this.sempahore.Release();
            }

            await this.AddFolderToQueue(folderPath);
        }

        public async Task AddFilesToQueue(IEnumerable<string> filePaths)
        {
            if (filePaths == null)
            {
                return;
            }

            await Task.Run(async () =>
            {
                ISet<string> allowedFileExtensions = ServiceManager.Get<IAudioService>().ApplicableAudioFileExtensions;

                List<MusicPlayerSong> newSongs = new List<MusicPlayerSong>();
                foreach (string filePath in filePaths)
                {
                    if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath) && allowedFileExtensions.Contains(Path.GetExtension(filePath).ToLower()))
                    {
                        newSongs.Add(this.CreateSongFromFile(filePath));
                    }
                }

                if (newSongs.Count > 0)
                {
                    try
                    {
                        await this.sempahore.WaitAsync();

                        foreach (MusicPlayerSong song in newSongs)
                        {
                            this.songs.Add(song);
                        }
                        this.SaveQueueToSettings();
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(ex);
                    }
                    finally
                    {
                        this.sempahore.Release();
                    }
                }

                this.OnQueueChanged();
            });
        }

        public async Task AddFolderToQueue(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                return;
            }

            ISet<string> allowedFileExtensions = ServiceManager.Get<IAudioService>().ApplicableAudioFileExtensions;
            WindowsFileService fileService = ServiceManager.Get<IFileService>() as WindowsFileService;

            List<string> files = new List<string>();
            await this.AddFilesFromDirectory(fileService, allowedFileExtensions, files, folderPath);

            await this.AddFilesToQueue(files);
        }

        public async Task AddPlaylistToQueue(string playlistFilePath)
        {
            if (string.IsNullOrWhiteSpace(playlistFilePath) || !File.Exists(playlistFilePath))
            {
                return;
            }

            List<string> files = new List<string>();
            try
            {
                IPlaylistIO playlist = PlaylistIOFactory.GetInstance().GetPlaylistIO(playlistFilePath);
                if (playlist != null && playlist.FilePaths != null)
                {
                    files.AddRange(playlist.FilePaths);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            await this.AddFilesToQueue(files);
        }

        public async Task<bool> ExportQueueToPlaylist(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || this.songs.Count == 0)
            {
                return false;
            }

            return await Task.Run(() =>
            {
                try
                {
                    global::ATL.Settings.PlaylistWriteAbsolutePath = true;

                    IPlaylistIO playlist = PlaylistIOFactory.GetInstance().GetPlaylistIO(filePath);
                    if (playlist != null)
                    {
                        IList<Track> tracks = new List<Track>();
                        foreach (MusicPlayerSong song in this.songs.ToList())
                        {
                            try
                            {
                                tracks.Add(new Track(song.FilePath));
                            }
                            catch (Exception ex)
                            {
                                Logger.Log(ex);
                            }
                        }
                        playlist.Tracks = tracks;
                        return playlist.Save();
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
                return false;
            });
        }

        public async Task RemoveFromQueue(MusicPlayerSong song)
        {
            if (song == null)
            {
                return;
            }

            bool removingCurrent = (song == this.CurrentSong);
            bool wasPlaying = (this.State == MusicPlayerState.Playing);

            if (removingCurrent)
            {
                await this.Stop();
            }

            try
            {
                await this.sempahore.WaitAsync();

                int index = this.songs.IndexOf(song);
                if (index >= 0)
                {
                    this.songs.Remove(song);
                    this.playedSongs.Remove(song);
                    this.playbackHistory.RemoveAll(s => s == song);

                    if (!this.playbackSessionActive && this.State == MusicPlayerState.Stopped)
                    {
                        this.currentSongIndex = 0;
                    }
                    else
                    {
                        if (index < this.currentSongIndex)
                        {
                            this.currentSongIndex--;
                        }

                        if (this.currentSongIndex >= this.songs.Count)
                        {
                            this.currentSongIndex = 0;
                        }
                    }

                    this.SaveQueueToSettings();
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            finally
            {
                this.sempahore.Release();
            }

            if (removingCurrent && wasPlaying && this.songs.Count > 0)
            {
                await this.Play();
            }

            this.OnQueueChanged();
        }

        public async Task MoveInQueue(int oldIndex, int newIndex)
        {
            try
            {
                await this.sempahore.WaitAsync();

                if (oldIndex >= 0 && oldIndex < this.songs.Count && newIndex >= 0 && newIndex < this.songs.Count && oldIndex != newIndex)
                {
                    this.songs.Move(oldIndex, newIndex);

                    if (!this.playbackSessionActive && this.State == MusicPlayerState.Stopped)
                    {
                        this.currentSongIndex = 0;
                    }
                    else if (oldIndex == this.currentSongIndex)
                    {
                        this.currentSongIndex = newIndex;
                    }
                    else if (oldIndex < this.currentSongIndex && newIndex >= this.currentSongIndex)
                    {
                        this.currentSongIndex--;
                    }
                    else if (oldIndex > this.currentSongIndex && newIndex <= this.currentSongIndex)
                    {
                        this.currentSongIndex++;
                    }

                    this.SaveQueueToSettings();
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            finally
            {
                this.sempahore.Release();
            }

            this.OnQueueChanged();
        }

        public async Task ClearQueue()
        {
            await this.Stop();

            try
            {
                await this.sempahore.WaitAsync();

                this.ClearQueueInternal();
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            finally
            {
                this.sempahore.Release();
            }

            this.OnQueueChanged();
        }

        public async Task LoadSongs()
        {
            await Task.Run(async () =>
            {
                if (ChannelSession.Settings.MusicPlayerQueue.Count == 0 && ChannelSession.Settings.MusicPlayerFolders.Count > 0)
                {
                    ISet<string> allowedFileExtensions = ServiceManager.Get<IAudioService>().ApplicableAudioFileExtensions;
                    WindowsFileService fileService = ServiceManager.Get<IFileService>() as WindowsFileService;

                    List<string> files = new List<string>();
                    foreach (string folder in ChannelSession.Settings.MusicPlayerFolders)
                    {
                        if (Directory.Exists(folder))
                        {
                            await this.AddFilesFromDirectory(fileService, allowedFileExtensions, files, folder);
                        }
                    }
                    ChannelSession.Settings.MusicPlayerQueue.AddRange(files);
                    ChannelSession.Settings.MusicPlayerFolders.Clear();
                }

                List<MusicPlayerSong> tempSongs = new List<MusicPlayerSong>();
                foreach (string file in ChannelSession.Settings.MusicPlayerQueue.ToList())
                {
                    if (!string.IsNullOrWhiteSpace(file) && File.Exists(file))
                    {
                        tempSongs.Add(this.CreateSongFromFile(file));
                    }
                }

                try
                {
                    await this.sempahore.WaitAsync();

                    this.songs.Clear();
                    this.playedSongs.Clear();
                    this.playbackHistory.Clear();
                    foreach (MusicPlayerSong song in tempSongs)
                    {
                        this.songs.Add(song);
                    }

                    this.currentSongIndex = MathHelper.Clamp(ChannelSession.Settings.MusicPlayerQueueCurrentIndex, 0, Math.Max(this.songs.Count - 1, 0));
                    this.playbackSessionActive = (this.currentSongIndex > 0);

                    this.SaveQueueToSettings();
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
                finally
                {
                    this.sempahore.Release();
                }

                this.OnQueueChanged();
            });
        }

        public async Task PlaySong(MusicPlayerSong song)
        {
            if (song == null)
            {
                return;
            }

            MusicPlayerSong previousSong = this.CurrentSong;

            await this.Stop();

            int index = this.songs.IndexOf(song);
            if (index >= 0)
            {
                if (previousSong != null && previousSong != song)
                {
                    this.AddToPlaybackHistory(previousSong);
                }

                this.currentSongIndex = index;
                await this.Play();
            }
        }

        public async Task<MusicPlayerSong> SearchAndPlaySong(string searchText, bool stopOnCompletion)
        {
            MusicPlayerSong song = null;

            var songs = this.songs.Where(s => !string.IsNullOrEmpty(s.Title) && s.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase));
            if (songs != null && songs.Count() > 0)
            {
                song = songs.OrderBy(s => s.Title.Length).First();
            }

            if (song != null)
            {
                MusicPlayerSong previousSong = this.CurrentSong;

                await this.Stop();

                if (previousSong != null)
                {
                    this.AddToPlaybackHistory(previousSong);
                }

                this.currentSongIndex = this.songs.IndexOf(song);
                this.stopOnSpecificSongCompletion = stopOnCompletion;
                this.stopOnSpecificSong = song;
                await this.Play();
            }

            return song;
        }

        private void PlayInternal(MusicPlayerSong song)
        {
            if (song == null)
            {
                this.State = MusicPlayerState.Stopped;
                return;
            }

            if (this.backgroundPlayThreadTokenSource != null)
            {
                this.backgroundPlayThreadTokenSource.Cancel();
            }
            this.backgroundPlayThreadTokenSource = new CancellationTokenSource();

            WindowsAudioService audioService = ServiceManager.Get<IAudioService>() as WindowsAudioService;
            Tuple<WaveOutEvent, WaveStream> output = audioService.PlayWithOutput(song.FilePath, this.Volume, ChannelSession.Settings.MusicPlayerAudioOutput);
            if (output != null)
            {
                this.currentWaveOutEvent = output.Item1;
                this.currentWaveStream = output.Item2;
                Task backgroundPlayThreadTask = Task.Run(async () => await this.PlayBackground(this.currentWaveOutEvent, song), this.backgroundPlayThreadTokenSource.Token);
            }
            else
            {
                this.State = MusicPlayerState.Stopped;
            }
        }

        private void SwitchOutputDeviceInternal()
        {
            MusicPlayerSong song = this.CurrentSong;
            if (song == null || this.State == MusicPlayerState.Stopped)
            {
                return;
            }

            MusicPlayerState previousState = this.State;
            TimeSpan previousPosition = TimeSpan.Zero;
            if (this.currentWaveStream != null)
            {
                previousPosition = this.currentWaveStream.CurrentTime;
            }

            WaveOutEvent previousWaveOutEvent = this.currentWaveOutEvent;
            this.currentWaveOutEvent = null;
            this.currentWaveStream = null;
            if (previousWaveOutEvent != null)
            {
                previousWaveOutEvent.Stop();
            }

            this.PlayInternal(song);

            if (this.currentWaveOutEvent != null)
            {
                if (previousState == MusicPlayerState.Paused)
                {
                    this.currentWaveOutEvent.Pause();
                }

                if (this.currentWaveStream != null && previousPosition > TimeSpan.Zero && previousPosition < this.currentWaveStream.TotalTime)
                {
                    this.currentWaveStream.CurrentTime = previousPosition;
                }
            }
        }

        private async Task PlayBackground(WaveOutEvent waveOutEvent, MusicPlayerSong song)
        {
            using (waveOutEvent)
            {
                while (waveOutEvent != null && (waveOutEvent.PlaybackState == PlaybackState.Playing || waveOutEvent.PlaybackState == PlaybackState.Paused))
                {
                    await Task.Delay(500);
                }
                waveOutEvent.Dispose();

                if (this.currentWaveOutEvent == waveOutEvent && this.CurrentSong == song && this.State == MusicPlayerState.Playing)
                {
                    if (this.stopOnSpecificSongCompletion && song == this.stopOnSpecificSong)
                    {
                        await this.Stop();
                    }
                    else if (this.RepeatOne)
                    {
                        await this.sempahore.WaitAsync();
                        try
                        {
                            if (this.currentWaveOutEvent == waveOutEvent && this.CurrentSong == song && this.State == MusicPlayerState.Playing)
                            {
                                this.currentWaveStream?.Dispose();
                                this.currentWaveStream = null;
                                this.PlayInternal(song);
                            }
                        }
                        finally
                        {
                            this.sempahore.Release();
                        }
                    }
                    else
                    {
                        await this.Next();
                    }
                }
            }
        }

        private bool PickNextShuffleSong()
        {
            MusicPlayerSong current = this.CurrentSong;
            if (current != null)
            {
                this.playedSongs.Add(current);
            }

            List<MusicPlayerSong> candidates = this.songs.Where(s => !this.playedSongs.Contains(s)).ToList();
            if (candidates.Count == 0)
            {
                if (!this.Repeat)
                {
                    this.currentSongIndex = 0;
                    return false;
                }

                this.playedSongs.Clear();
                candidates = this.songs.Where(s => s != current).ToList();
                if (candidates.Count == 0)
                {
                    candidates = this.songs.ToList();
                }
            }

            if (candidates.Count == 0)
            {
                return false;
            }

            MusicPlayerSong next = candidates[RandomHelper.GenerateRandomNumber(candidates.Count)];
            this.currentSongIndex = this.songs.IndexOf(next);
            return true;
        }

        private MusicPlayerSong CreateSongFromFile(string filePath)
        {
            MusicPlayerSong song = new MusicPlayerSong()
            {
                FilePath = filePath,
                Title = Path.GetFileNameWithoutExtension(filePath)
            };

            try
            {
                Track track = new Track(filePath);
                if (!string.IsNullOrWhiteSpace(track.Title))
                {
                    song.Title = track.Title;
                }
                if (!string.IsNullOrWhiteSpace(track.Artist))
                {
                    song.Artist = track.Artist;
                }
                song.Length = track.Duration;
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            return song;
        }

        private void ClearQueueInternal()
        {
            this.songs.Clear();
            this.playedSongs.Clear();
            this.playbackHistory.Clear();
            this.currentSongIndex = 0;
            this.playbackSessionActive = false;
            this.SaveQueueToSettings();
        }

        private void AddToPlaybackHistory(MusicPlayerSong song)
        {
            this.playbackHistory.Add(song);
            if (this.playbackHistory.Count > MaximumHistorySize)
            {
                this.playbackHistory.RemoveAt(0);
            }
        }

        private void SaveQueueToSettings()
        {
            ChannelSession.Settings.MusicPlayerQueue.Clear();
            ChannelSession.Settings.MusicPlayerQueue.AddRange(this.songs.Select(s => s.FilePath));
            this.PersistCurrentIndex();
        }

        private void PersistCurrentIndex()
        {
            ChannelSession.Settings.MusicPlayerQueueCurrentIndex = this.currentSongIndex;
        }

        private void OnQueueChanged()
        {
            DispatcherHelper.Dispatcher.Invoke(() => this.QueueChanged.Invoke(this, new EventArgs()));
        }

        private async Task AddFilesFromDirectory(WindowsFileService fileService, ISet<string> allowedFileExtensions, List<string> files, string path)
        {
            foreach (string file in await fileService.GetFilesInDirectory(path))
            {
                string extension = Path.GetExtension(file).ToLower();
                if (allowedFileExtensions.Contains(extension))
                {
                    files.Add(file);
                }
            }

            foreach (string subFolder in await fileService.GetFoldersInDirectory(path))
            {
                await this.AddFilesFromDirectory(fileService, allowedFileExtensions, files, subFolder);
            }
        }
    }
}

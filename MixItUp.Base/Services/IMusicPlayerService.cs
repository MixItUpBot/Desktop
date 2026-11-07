using MixItUp.Base.Util;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MixItUp.Base.Services
{
    public enum MusicPlayerState
    {
        Stopped,
        Playing,
        Paused,
    }

    public class MusicPlayerSong
    {
        public string FilePath { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        public int Length { get; set; }

        public string LengthString
        {
            get
            {
                int minutes = this.Length / 60;
                int seconds = this.Length % 60;
                string secondsText = seconds < 10 ? "0" + seconds : seconds.ToString();
                return $"{minutes}:{secondsText}";
            }
        }

        public string QueueDetailsString
        {
            get
            {
                if (!string.IsNullOrEmpty(this.Artist))
                {
                    return $"{this.LengthString} / {this.Artist}";
                }
                return this.LengthString;
            }
        }

        public override string ToString()
        {
            if (!string.IsNullOrEmpty(this.Artist))
            {
                return $"{this.Artist} - {this.Title}";
            }
            else
            {
                return this.Title;
            }
        }
    }

    public interface IMusicPlayerService
    {
        event EventHandler SongChanged;

        event EventHandler QueueChanged;

        MusicPlayerState State { get; }

        int Volume { get; }

        MusicPlayerSong CurrentSong { get; }

        ThreadSafeObservableCollection<MusicPlayerSong> Songs { get; }

        bool Shuffle { get; }

        bool Repeat { get; }

        bool RepeatOne { get; }

        TimeSpan CurrentPosition { get; }

        TimeSpan CurrentDuration { get; }

        Task<string> GetAlbumArt(MusicPlayerSong song);

        Task Play();

        Task Pause();

        Task Stop();

        Task Next();

        Task Previous();

        Task ChangeVolume(int amount);

        Task SetAudioOutputDevice(string deviceName);

        Task SetShuffle(bool enabled);

        Task SetRepeat(bool enabled);

        Task SetRepeatOne(bool enabled);

        Task Seek(TimeSpan position);

        Task ChangeFolder(string folderPath);

        Task AddFilesToQueue(IEnumerable<string> filePaths);

        Task AddFolderToQueue(string folderPath);

        Task AddPlaylistToQueue(string playlistFilePath);

        Task<bool> ExportQueueToPlaylist(string filePath);

        Task RemoveFromQueue(MusicPlayerSong song);

        Task MoveInQueue(int oldIndex, int newIndex);

        Task ClearQueue();

        Task LoadSongs();

        Task PlaySong(MusicPlayerSong song);

        Task<MusicPlayerSong> SearchAndPlaySong(string searchText, bool stopOnCompletion);
    }
}

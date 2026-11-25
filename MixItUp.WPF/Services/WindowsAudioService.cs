using MixItUp.Base;
using MixItUp.Base.Services;
using MixItUp.Base.Util;
using NAudio;
using NAudio.Wave;
using NAudio.Vorbis;
using MixItUp.Base.Web;
using System.Net.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.WPF.Services
{
    public class WindowsAudioService : IAudioService
    {
        public string DefaultAudioDevice { get { return MixItUp.Base.Resources.DefaultOutput; } }
        public string MixItUpOverlay { get { return MixItUp.Base.Resources.MixItUpOverlay; } }

        public ISet<string> ApplicableAudioFileExtensions => new HashSet<string>() { ".mp3", ".wav", ".flac", ".mp4", ".m4a", ".aac", ".wma", ".ogg" };

        private readonly Dictionary<CancellationTokenSource, Task> audioTasks = new Dictionary<CancellationTokenSource, Task>();
        private static readonly HttpClient audioClient = HttpConnectionFactory.CreateClient();

        private HashSet<Guid> activeOverlaySounds = new HashSet<Guid>();
        private static readonly TimeSpan WaitForFinishTimeout = TimeSpan.FromMinutes(5);

        public async Task Play(string filePath, int volume, string deviceName, bool waitForFinish = false)
        {
            try
            {
                if (!string.IsNullOrEmpty(filePath))
                {
                    deviceName = this.GetOutputDeviceName(deviceName);
                    if (this.MixItUpOverlay.Equals(deviceName))
                    {
                        await this.PlayOnOverlay(filePath, volume, waitForFinish);
                    }
                    else
                    {
                        await this.PlayNative(token => this.OpenAudio(filePath, token), deviceName, volume, waitForFinish);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        public async Task PlayMP3Stream(Stream stream, int volume, string deviceName, bool waitForFinish = false)
        {
            try
            {
                if (stream != null && stream.Length > 0)
                {
                    deviceName = this.GetOutputDeviceName(deviceName);
                    if (this.MixItUpOverlay.Equals(deviceName))
                    {
                        string tempFilePath = Path.Combine(ServiceManager.Get<IFileService>().GetTempFolder(), $"{Guid.NewGuid()}.mp3");
                        await ServiceManager.Get<IFileService>().SaveFile(tempFilePath, stream);
                        await this.PlayOnOverlay(tempFilePath, volume, waitForFinish);
                    }
                    else
                    {
                        await this.PlayWaveStream(new StreamMediaFoundationReader(stream), deviceName, volume, waitForFinish);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        public async Task PlayPCMStream(Stream stream, int volume, string deviceName, bool waitForFinish = false)
        {
            try
            {
                if (stream != null && stream.Length > 0)
                {
                    deviceName = this.GetOutputDeviceName(deviceName);
                    if (this.MixItUpOverlay.Equals(deviceName))
                    {
                        Logger.ForceLog(LogLevel.Error, "Mix It Up Overlay not supported for playing PCM streams");
                        if (ChannelSession.IsDebug())
                        {
                            throw new Exception("Mix It Up Overlay not supported for playing PCM streams");
                        }
                        return;
                    }
                    await this.PlayWaveStream(new RawSourceWaveStream(stream, new WaveFormat(88200, 16, 1)), deviceName, volume, waitForFinish);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        public async Task PlayOnOverlay(string filePath, double volume, bool waitForFinish = false)
        {
            OverlayEndpointV3Service overlay = ServiceManager.Get<OverlayV3Service>().GetDefaultOverlayEndpointService();
            if (overlay != null)
            {
                Guid id = Guid.NewGuid();
                lock (this.activeOverlaySounds) { this.activeOverlaySounds.Add(id); }
                try { await overlay.PlayAudio(filePath, volume, id); }
                catch { this.OverlaySoundFinished(id); throw; }

                if (waitForFinish)
                {
                    bool contains = false;
                    TimeSpan timeout = WaitForFinishTimeout;
                    try
                    {
                        if (File.Exists(filePath)) { timeout = GetWaitForFinishTimeout(TimeSpan.FromSeconds(new ATL.Track(filePath).Duration)); }
                    }
                    catch (Exception ex) { Logger.Log(ex); }
                    var elapsed = System.Diagnostics.Stopwatch.StartNew();
                    do
                    {
                        await Task.Delay(100);

                        lock (this.activeOverlaySounds)
                        {
                            contains = this.activeOverlaySounds.Contains(id);
                        }

                    } while (contains && elapsed.Elapsed < timeout);
                    if (contains)
                    {
                        Logger.Log(LogLevel.Warning, "Timed out waiting for overlay audio to finish");
                    }
                }
            }
        }

        public async Task PlayNotification(string filePath, int volume)
        {
            try
            {
                if (!string.IsNullOrEmpty(filePath))
                {
                    if (ChannelSession.Settings.NotificationCooldownAmount > 0)
                    {
                        if (ChannelSession.Settings.NotificationLastTrigger.TotalSecondsFromNow() < ChannelSession.Settings.NotificationCooldownAmount)
                        {
                            return;
                        }
                        ChannelSession.Settings.NotificationLastTrigger = DateTimeOffset.Now;
                    }
                    await this.Play(filePath, volume, ChannelSession.Settings.NotificationsAudioOutput);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        public async Task StopAllSounds()
        {
            Task[] stopping;
            lock (this.audioTasks)
            {
                stopping = this.audioTasks.Values.ToArray();
                foreach (var source in this.audioTasks.Keys.ToArray()) { source.Cancel(); }
            }
            await Task.WhenAll(stopping);

            OverlayEndpointV3Service overlay = ServiceManager.Get<OverlayV3Service>().GetDefaultOverlayEndpointService();
            if (overlay != null)
            {
                foreach (var id in this.activeOverlaySounds.ToList())
                {
                    await overlay.Function(id.ToString(), "remove", new Dictionary<string, object>());
                }

                lock (this.activeOverlaySounds)
                {
                    this.activeOverlaySounds.Clear();
                }
            }
        }

        public void OverlaySoundFinished(Guid id)
        {
            lock (this.activeOverlaySounds)
            {
                this.activeOverlaySounds.Remove(id);
            }
        }

        public IEnumerable<string> GetSelectableAudioDevices(bool includeOverlay = false, string retainedDevice = null)
        {
            List<string> audioOptions = new List<string>();
            audioOptions.Add(this.DefaultAudioDevice);
            audioOptions.Add(this.MixItUpOverlay);
            audioOptions.AddRange(ServiceManager.Get<IAudioService>().GetOutputDevices());

            if (!string.IsNullOrEmpty(retainedDevice) && !audioOptions.Contains(retainedDevice))
            {
                audioOptions.Add(retainedDevice);
            }

            return audioOptions;
        }

        public IEnumerable<string> GetOutputDevices()
        {
            List<string> results = new List<string>();
            for (int i = 0; i < WaveOut.DeviceCount; i++)
            {
                try
                {
                    WaveOutCapabilities capabilities = WaveOut.GetCapabilities(i);
                    results.Add(capabilities.ProductName);
                }
                catch (Exception ex) { Logger.Log(ex); }
            }
            return results;
        }

        public Tuple<WaveOutEvent, WaveStream> PlayWithOutput(string filePath, int volume, string deviceName)
        {
            try
            {
                var stream = this.OpenAudio(filePath, CancellationToken.None).GetAwaiter().GetResult();
                return Tuple.Create(this.StartOutput(stream, deviceName, volume), stream);
            }
            catch (Exception ex) { Logger.Log(ex); return null; }
        }

        private static WaveStream OpenLocalAudio(string path)
        {
            return string.Equals(Path.GetExtension(path), ".ogg", StringComparison.OrdinalIgnoreCase)
                ? new VorbisWaveReader(path) : new AudioFileReader(path);
        }

        private async Task<WaveStream> OpenAudio(string path, CancellationToken token)
        {
            if (!Uri.TryCreate(path, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                return OpenLocalAudio(path);
            }
            if (!string.Equals(Path.GetExtension(uri.AbsolutePath), ".ogg", StringComparison.OrdinalIgnoreCase))
            {
                return new MediaFoundationReader(path);
            }

            using var response = await audioClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var buffer = new MemoryStream();
            try
            {
                await response.Content.CopyToAsync(buffer, token).ConfigureAwait(false);
                buffer.Position = 0;
                return new VorbisWaveReader(buffer, true);
            }
            catch { buffer.Dispose(); throw; }
        }

        private WaveOutEvent StartOutput(WaveStream stream, string deviceName, int volume)
        {
            WaveOutEvent output = null;
            try
            {
                output = this.CreateWaveOutEvent(deviceName);
                try
                {
                    output.Init(stream);
                }
                catch (MmException ex) when (output.DeviceNumber >= 0 &&
                    ex.Result is MmResult.BadDeviceId or MmResult.NoDriver or MmResult.AlreadyAllocated)
                {
                    Logger.Log(LogLevel.Warning, $"Audio output device \"{deviceName}\" became unavailable ({ex.Result}), using the default output for this sound.");
                    output.Dispose();
                    output = new WaveOutEvent();
                    output.Init(stream);
                }
                if (stream is AudioFileReader file) { file.Volume = this.ConvertVolumeAmount(volume); }
                else { output.Volume = this.ConvertVolumeAmount(volume); }
                output.Play();
                return output;
            }
            catch { output?.Dispose(); stream.Dispose(); throw; }
        }

        public float ConvertVolumeAmount(int amount) { return MathHelper.Clamp(amount, 0, 100) / 100.0f; }

        public string GetOutputDeviceName(string deviceName)
        {
            if (string.IsNullOrEmpty(deviceName))
            {
                return ChannelSession.Settings.DefaultAudioOutput;
            }
            return deviceName;
        }

        private WaveOutEvent CreateWaveOutEvent(string deviceName)
        {
            int deviceNumber = -1;
            if (!string.IsNullOrEmpty(deviceName))
            {
                deviceNumber = this.GetOutputDeviceID(deviceName);
            }
            return (deviceNumber < 0) ? new WaveOutEvent() : new WaveOutEvent() { DeviceNumber = deviceNumber };
        }

        private static TimeSpan GetWaitForFinishTimeout(TimeSpan duration)
        {
            return duration > WaitForFinishTimeout ? duration + TimeSpan.FromSeconds(30) : WaitForFinishTimeout;
        }

        private async Task PlayNative(Func<CancellationToken, Task<WaveStream>> open, string deviceName, int volume, bool waitForFinish)
        {
            var source = new CancellationTokenSource();
            var finished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (this.audioTasks) { this.audioTasks.Add(source, finished.Task); }
            bool started = false;
            try
            {
                var stream = await open(source.Token);
                if (source.IsCancellationRequested) { stream.Dispose(); return; }
                var output = this.StartOutput(stream, deviceName, volume);
                started = true;
                var timeout = GetWaitForFinishTimeout(stream.TotalTime);
                var playback = FinishPlayback();
                async Task FinishPlayback()
                {
                    try
                    {
                        using (stream)
                        using (output)
                        {
                            while (output.PlaybackState == PlaybackState.Playing) { await Task.Delay(50, source.Token); }
                        }
                    }
                    catch (OperationCanceledException) when (source.IsCancellationRequested) { }
                    catch (Exception ex) { Logger.Log(ex); }
                    finally { Complete(); }
                }
                if (waitForFinish && await Task.WhenAny(playback, Task.Delay(timeout)) != playback)
                {
                    Logger.Log(LogLevel.Warning, "Timed out waiting for audio to finish");
                }
            }
            catch (OperationCanceledException) when (source.IsCancellationRequested) { }
            finally { if (!started) { Complete(); } }

            void Complete()
            {
                lock (this.audioTasks) { this.audioTasks.Remove(source); }
                finished.TrySetResult(true);
                source.Dispose();
            }
        }

        private Task PlayWaveStream(WaveStream stream, string deviceName, int volume, bool waitForFinish = false)
        {
            return this.PlayNative(token => Task.FromResult(stream), deviceName, volume, waitForFinish);
        }

        private int GetOutputDeviceID(string deviceName)
        {
            if (!string.IsNullOrEmpty(deviceName))
            {
                for (int i = 0; i < WaveOut.DeviceCount; i++)
                {
                    try
                    {
                        WaveOutCapabilities capabilities = WaveOut.GetCapabilities(i);
                        if (deviceName.Equals(capabilities.ProductName))
                        {
                            return i;
                        }
                    }
                    catch (Exception ex) { Logger.Log(ex); }
                }

                // The named device is gone and playback is about to fall back to the default output device.
                // The two sentinel options never match a real device, so they are not a resolution failure.
                if (!deviceName.Equals(this.DefaultAudioDevice) && !deviceName.Equals(this.MixItUpOverlay))
                {
                    Logger.Log(LogLevel.Warning, $"Audio output device \"{deviceName}\" could not be found, falling back to the default output device");
                }
            }
            return -1;
        }
    }
}

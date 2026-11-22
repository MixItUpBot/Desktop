using MixItUp.Base;
using MixItUp.Base.Model.API.Files.V2;
using MixItUp.Base.Services;
using MixItUp.Base.Util;
using MixItUp.Base.Web;
using MixItUp.Updates;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace MixItUp.WPF.Services
{
    internal static class BackgroundUpdates
    {
        private static readonly string Root = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
        private static readonly string Cache = Path.Combine(Root, PendingUpdate.CacheName);
        private static readonly TimeSpan PollInterval = UpdateCheckSchedule.Interval;
        private static readonly CancellationTokenSource Lifetime = new CancellationTokenSource();
        private static bool restartRequested;

        internal static event EventHandler ReadyChanged;
        internal static bool IsReady { get; private set; }

        private static bool HasStagedUpdate()
        {
            var pending = PendingUpdate.Read(Cache);
            return pending != null && pending.Channel == Channel && Version.Parse(pending.Version) > VersionHelper.GetCurrentVersion()
                && File.Exists(Path.Combine(Cache, pending.ArchiveName));
        }

        private static void RefreshReady()
        {
            bool ready = false;
            try { ready = HasStagedUpdate(); }
            catch (Exception ex) { Logger.Log(ex); }
            if (ready != IsReady)
            {
                IsReady = ready;
                ReadyChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        internal static void RestartIfReady()
        {
            try
            {
                if (!HasStagedUpdate() || restartRequested) return;
                if (Application.Current.MainWindow is MainWindow main) { restartRequested = true; main.RestartForUpdate(); }
                else if (Application.Current.MainWindow is LoginWindow login) { restartRequested = true; login.RestartForUpdate(); }
            }
            catch (Exception ex) { Logger.Log(ex); }
        }

        private static string Channel => BuildChannelHelper.GetUpdateChannel(ChannelSession.AppSettings.UpdateChannel,
            ChannelSession.AppSettings.PreviewProgram, ChannelSession.AppSettings.TestBuild);

        public static void Start()
        {
            if (!BuildChannelHelper.BYPASS_UPDATE_CHECK) { _ = Task.Run(PollAsync); }
        }

        public static void Stop() => Lifetime.Cancel();

        private static async Task PollAsync()
        {
            var token = Lifetime.Token;
            RefreshReady();
            while (!token.IsCancellationRequested)
            {
                TimeSpan delay = PollInterval;
                try { delay = await CheckAsync(token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
                catch (Exception ex) { Logger.Log(ex); }
                RefreshReady();
                try { delay = await CheckSavedDeadline(Channel, delay).ConfigureAwait(false); }
                catch (Exception ex) { Logger.Log(ex); }
                try { await Task.Delay(delay, token).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
            }
        }

        private static async Task<TimeSpan> CheckAsync(CancellationToken token)
        {
            string channel = Channel;
            Directory.CreateDirectory(Cache);
            if ((File.GetAttributes(Cache) & FileAttributes.ReparsePoint) != 0) { throw new IOException("Linked update directory."); }
            using var updateLock = File.Open(Path.Combine(Cache, "apply.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            TimeSpan delay = UpdateCheckSchedule.Reserve(Cache, DateTimeOffset.UtcNow);
            if (delay > TimeSpan.Zero) { return delay; }
            var service = ServiceManager.Get<MixItUpService>();
            var check = await service.FetchVersionCheckAsync(channel).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (channel != Channel || check == null) { return PollInterval; }
            if (check.channel != channel || check.app != "mixitup-desktop" || check.os != "windows" || check.arch != "x64"
                || !Version.TryParse(check.latestVersion, out _) || !Version.TryParse(check.minimumVersion, out _)) { return PollInterval; }

            Version current = VersionHelper.GetCurrentVersion();
            Version minimum = check.GetNormalizedMinimumVersion();
            Version latest = check.GetNormalizedLatestVersion();
            if (latest < minimum) { return PollInterval; }
            PendingUpdate pending = ReadPending();
            string clock = Path.Combine(Cache, "required-" + channel);
            if (check.updatePaused || current >= minimum) { File.Delete(clock); }
            if (!check.updatePaused && current < minimum)
            {
                string[] saved = File.Exists(clock) ? File.ReadAllLines(clock) : Array.Empty<string>();
                if (saved.Length == 0 || !long.TryParse(saved[0], out long observed))
                {
                    observed = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                }
                if (saved.Length != 2 || saved[1] != check.minimumVersion)
                {
                    File.WriteAllLines(clock + ".tmp", new[] { observed.ToString(CultureInfo.InvariantCulture), check.minimumVersion });
                    File.Move(clock + ".tmp", clock, true);
                }
            }
            if (check.updatePaused || current >= latest)
            {
                Discard(pending);
                return PollInterval;
            }
            if (pending?.Channel != channel || pending.Version != check.latestVersion || !File.Exists(Path.Combine(Cache, pending.ArchiveName)))
            {
                UpdateVersionManifestModel manifest = await service.FetchVersionManifestAsync(channel, check.latestVersion).ConfigureAwait(false);
                if (manifest == null || manifest.version != check.latestVersion) { return PollInterval; }
                await StageAsync(channel, manifest, pending, token).ConfigureAwait(false);
            }
            return PollInterval;
        }

        private static async Task<TimeSpan> CheckSavedDeadline(string channel, TimeSpan nextPoll)
        {
            string clock = Path.Combine(Cache, "required-" + channel);
            if (!File.Exists(clock)) return nextPoll;
            string[] saved = File.ReadAllLines(clock);
            PendingUpdate ready = ReadPending();
            if (saved.Length == 2 && long.TryParse(saved[0], out long observedAt) && Version.TryParse(saved[1], out Version required)
                && VersionHelper.GetCurrentVersion() < required
                && ready?.Channel == channel && Version.Parse(ready.Version) >= required && File.Exists(Path.Combine(Cache, ready.ArchiveName)))
            {
                TimeSpan remaining = await RestartWhenDue(DateTimeOffset.FromUnixTimeSeconds(observedAt).AddHours(24)).ConfigureAwait(false);
                if (remaining > TimeSpan.Zero && remaining < nextPoll) return remaining;
            }
            return nextPoll;
        }

        private static async Task<TimeSpan> RestartWhenDue(DateTimeOffset deadline)
        {
            TimeSpan remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero && !restartRequested)
            {
                await Application.Current.Dispatcher.InvokeAsync(RestartIfReady);
            }
            return remaining;
        }

        private static PendingUpdate ReadPending()
        {
            try { return PendingUpdate.Read(Cache); }
            catch (Exception error) when (error is InvalidDataException || error is System.Runtime.Serialization.SerializationException)
            {
                Logger.Log(error);
                File.Delete(Path.Combine(Cache, PendingUpdate.StateName));
                return null;
            }
        }

        private static async Task StageAsync(string channel, UpdateVersionManifestModel manifest, PendingUpdate old, CancellationToken token)
        {
            var download = manifest.GetDownloadStep();
            var verify = manifest.GetVerifyStep();
            var update = new PendingUpdate { Channel = channel, Version = manifest.version, Sha256 = verify?.value, EulaVersion = manifest.eulaVersion };
            string filename = download?.target;
            if (!update.IsValid() || string.IsNullOrWhiteSpace(filename) || filename != Path.GetFileName(filename)
                || filename != verify.target || filename != manifest.GetExtractStep()?.target) { throw new InvalidDataException("Invalid update manifest."); }
            string url = BuildChannelHelper.API_FILES_UPDATE_ROOT + "/" + channel + "/" + update.Version + "/";
            string archive = Path.Combine(Cache, update.ArchiveName);
            string temporary = archive + ".part";
            try
            {
                using var client = HttpConnectionFactory.CreateClient();
                using (var response = await client.GetAsync(url + Uri.EscapeDataString(filename), HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    using var file = new FileStream(temporary, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
                    await response.Content.CopyToAsync(file, token).ConfigureAwait(false);
                    file.Position = 0;
                    string digest = Convert.ToHexString(await SHA256.HashDataAsync(file, token).ConfigureAwait(false));
                    if (!string.Equals(digest, update.Sha256, StringComparison.OrdinalIgnoreCase)) { throw new InvalidDataException("Update checksum mismatch."); }
                    file.Flush(true);
                }
                if (!update.LicenseAccepted(Root))
                {
                    string license = await client.GetStringAsync(url + "eula", token).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(license)) { throw new InvalidDataException("Missing update license."); }
                    await File.WriteAllTextAsync(archive + ".eula", license, token).ConfigureAwait(false);
                }
                token.ThrowIfCancellationRequested();
                if (Channel != channel) { return; }
                File.Move(temporary, archive, true);
                string state = Path.Combine(Cache, PendingUpdate.StateName);
                using (FileStream stream = File.Create(state + ".tmp")) { update.Write(stream); stream.Flush(true); }
                File.Move(state + ".tmp", state, true);
                if (old != null && old.ArchiveName != update.ArchiveName) { DeleteArchive(old); }
            }
            finally { File.Delete(temporary); }
        }

        private static void DeleteArchive(PendingUpdate update)
        {
            string archive = Path.Combine(Cache, update.ArchiveName);
            File.Delete(archive);
            File.Delete(archive + ".eula");
        }

        private static void Discard(PendingUpdate update)
        {
            File.Delete(Path.Combine(Cache, PendingUpdate.StateName));
            if (update != null) { DeleteArchive(update); }
        }

        public static bool TryApplyAtStartup(string[] arguments)
        {
            if (arguments.Contains("--skip-background-update") || !Directory.Exists(Cache)) { return false; }
            try
            {
                if ((File.GetAttributes(Cache) & FileAttributes.ReparsePoint) != 0) { return false; }
                if (!File.Exists(Path.Combine(Cache, "rollback.txt")))
                {
                    PendingUpdate update = ReadPending();
                    if (update == null || update.Channel != Channel || Version.Parse(update.Version) <= VersionHelper.GetCurrentVersion()) { return false; }
                    if (!update.LicenseAccepted(Root))
                    {
                        string license = File.ReadAllText(Path.Combine(Cache, update.ArchiveName + ".eula"));
                        if (!Windows.LicenseAgreementWindow.ConfirmAgreement(license, update.EulaVersion)) { return true; }
                    }
                }
                // The helper runs from a transient copy so it can replace its installed binary as well.
                foreach (string previous in Directory.GetFiles(Cache, "apply-*.exe"))
                {
                    try { File.Delete(previous); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                }
                string copy = Path.Combine(Cache, "apply-" + Environment.ProcessId + ".exe");
                File.Copy(Path.Combine(Root, PendingUpdate.HelperName), copy, true);
                var start = new ProcessStartInfo(copy) { WorkingDirectory = Root, UseShellExecute = false, CreateNoWindow = true };
                start.ArgumentList.Add(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
                foreach (string argument in arguments) { start.ArgumentList.Add(argument); }
                using (Process.Start(start)) { }
                return true;
            }
            catch (Exception ex) { Logger.Log(ex); return false; }
        }

        public static void Relaunch(Guid? profile)
        {
            var start = new ProcessStartInfo(Path.Combine(Root, "MixItUp.exe")) { WorkingDirectory = Root, UseShellExecute = false };
            if (profile.HasValue) { start.ArgumentList.Add("--profile=" + profile.Value.ToString("D")); }
            using (Process.Start(start)) { }
        }
    }
}

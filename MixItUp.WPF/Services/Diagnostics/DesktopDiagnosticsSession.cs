#nullable enable
using MixItUp.Base;
using MixItUp.Base.Model.Settings;
using MixItUp.Base.Services;
using MixItUp.Base.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.WPF.Services.Diagnostics;

public sealed class DesktopDiagnosticsSession : IDisposable
{
    private readonly HttpClient http = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(3) };
    private string? root, boundCode;
    private List<PreparedFile>? files;
    private bool needsElevation;

    public async Task<bool> Run(string code, Func<Task<bool>> confirmPartial, CancellationToken cancellation)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(code, "^[A-Z0-9]{6}$")) throw new ArgumentException("Invalid support code");
        if (files == null || code != boundCode)
        {
            using var check = await http.GetAsync($"https://support.mixitup.bot/api/v1/diagnostics/{code}", cancellation);
            check.EnsureSuccessStatusCode();
            using var grant = JsonDocument.Parse(await check.Content.ReadAsStringAsync(cancellation));
            if (grant.RootElement.GetProperty("scope").GetString() != "diagnostics") throw new InvalidOperationException("Invalid support grant");
            Cleanup();
            root = Path.Combine(Path.GetTempPath(), "MixItUpDiagnostics-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            Collector.RestrictDirectory(root);
            boundCode = code;
            var evidence = Path.Combine(root, "source");
            Directory.CreateDirectory(evidence);
            string? profile = ChannelSession.Settings == null ? null : Path.GetFullPath(ChannelSession.Settings.SettingsFilePath);
            await Task.Run(FileLoggerHandler.Flush, cancellation);
            await SettingsService.WithFileSnapshot(() => ((WindowsDatabaseService)ServiceManager.Get<IDatabaseService>()).WithFileSnapshot(
                () => WindowsFileService.WithFileSnapshot(() => Task.Run(() => CopyEvidence(evidence, profile, cancellation), cancellation))));
            var collector = new Collector();
            var quietProgress = new Progress<string>();
            var paths = await Task.Run(() => collector.Collect(AppContext.BaseDirectory, root, quietProgress, cancellation,
                selectedProfile: profile == null ? null : Path.Combine(evidence, "Settings", Path.GetFileName(profile)), evidenceRoot: evidence), cancellation);
            var prepared = new List<PreparedFile>();
            foreach (var path in paths)
            {
                await using var stream = File.OpenRead(path);
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellation)).ToLowerInvariant();
                prepared.Add(new(prepared.Count, Path.GetFileName(path), path, stream.Length, hash));
            }
            files = prepared;
            needsElevation = collector.NeedsElevation;
        }
        if (needsElevation && !await confirmPartial()) return false;
        await new DiagnosticsUploader(http).Upload(code, files, new Progress<string>(), cancellation);
        Cleanup();
        return true;
    }

    private static void CopyEvidence(string destination, string? profile, CancellationToken cancellation)
    {
        var sources = new List<(string Source, string Relative)>();
        string application = Path.GetFullPath("ApplicationSettings.json");
        if (File.Exists(application)) sources.Add((application, "ApplicationSettings.json"));
        if (profile != null)
        {
            sources.Add((profile, Path.Combine("Settings", Path.GetFileName(profile))));
            var database = Path.ChangeExtension(profile, ".db3");
            foreach (var suffix in new[] { "", "-wal", "-journal" })
                if (File.Exists(database + suffix)) sources.Add((database + suffix, Path.Combine("Settings", Path.GetFileName(database + suffix))));
        }
        foreach (var log in Collector.SelectLogs(Path.GetFullPath("Logs"))) sources.Add((log, Path.Combine("Logs", Path.GetFileName(log))));
        long total = 0;
        var held = new List<(FileStream Stream, string Relative)>();
        try
        {
            foreach (var source in sources)
            {
                cancellation.ThrowIfCancellationRequested();
                var stream = OriginalFileCopy.OpenStable(source.Source, 128L * 1024 * 1024);
                held.Add((stream, source.Relative));
                total += stream.Length;
                if (total > 512L * 1024 * 1024) throw new IOException("Diagnostics exceed the collection size limit");
            }
            foreach (var source in held)
            {
                string target = Path.Combine(destination, source.Relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                OriginalFileCopy.Copy(source.Stream, target, cancellation);
            }
        }
        finally { foreach (var source in held) source.Stream.Dispose(); }
    }

    private void Cleanup()
    {
        if (root != null)
        {
            try
            {
                string full = Path.GetFullPath(root);
                if (Path.GetDirectoryName(full) == Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) &&
                    Path.GetFileName(full).StartsWith("MixItUpDiagnostics-", StringComparison.Ordinal)) Directory.Delete(full, recursive: true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        root = boundCode = null;
        files = null;
        needsElevation = false;
    }

    public void Dispose() { Cleanup(); http.Dispose(); }
}

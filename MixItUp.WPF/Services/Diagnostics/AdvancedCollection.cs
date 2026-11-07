#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace MixItUp.WPF.Services.Diagnostics;

public sealed partial class Collector
{
    private readonly List<(string Product, string Directory, string Source)> recordedLocations = [];
    private async Task Hardware()
    {
        if (!OperatingSystem.IsWindows()) return;
        var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe")) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        const string script = """
[Console]::OutputEncoding=[Text.UTF8Encoding]::new()
$ErrorActionPreference='Stop'
function Read-Section($action) { try { @(& $action) } catch { @{status='unavailable';error=$_.Exception.Message;denied=($_.CategoryInfo.Category -eq 'PermissionDenied')} } }
@{
 cpu=Read-Section { Get-CimInstance Win32_Processor | Select-Object Name,NumberOfCores,NumberOfLogicalProcessors }
 gpu=Read-Section { Get-CimInstance Win32_VideoController | Select-Object Name,DriverVersion,DriverDate }
 os=Read-Section { Get-CimInstance Win32_OperatingSystem | Select-Object Caption,Version,BuildNumber,TotalVisibleMemorySize,FreePhysicalMemory,LastBootUpTime }
 hotfixes=Read-Section { Get-HotFix | Sort-Object InstalledOn -Descending | Select-Object -First 64 HotFixID,Description,InstalledOn }
 drivers=Read-Section { Get-CimInstance Win32_PnPSignedDriver | Where-Object { $_.DeviceClass -in @('DISPLAY','MEDIA','NET') } | Select-Object -First 80 DeviceName,DeviceClass,Manufacturer,DriverVersion,DriverDate,InfName }
 securityProducts=Read-Section { Get-CimInstance -Namespace root/SecurityCenter2 -ClassName AntiVirusProduct | Select-Object -First 16 displayName,productState }
 defender=Read-Section { Get-MpComputerStatus | Select-Object AMServiceEnabled,AntivirusEnabled,AntispywareEnabled,RealTimeProtectionEnabled,AntivirusSignatureVersion,AntivirusSignatureLastUpdated }
} | ConvertTo-Json -Depth 6 -Compress
""";
        foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-Command", script }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new IOException("Windows hardware query could not start.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromSeconds(25));
        async Task<string> ReadBounded(StreamReader reader)
        {
            var text = new StringBuilder(); var buffer = new char[4096]; int count;
            while ((count = await reader.ReadAsync(buffer, deadline.Token)) != 0) { text.Append(buffer, 0, count); if (text.Length > 1024 * 1024) throw new IOException("Hardware query output exceeded its limit."); }
            return text.ToString();
        }
        try
        {
            var stdout = ReadBounded(process.StandardOutput); var stderr = ReadBounded(process.StandardError);
            await Task.WhenAll(process.WaitForExitAsync(deadline.Token), stdout, stderr);
            if (process.ExitCode != 0) throw new IOException("Windows hardware query failed: " + await stderr);
            using var json = JsonDocument.Parse(await stdout);
            if (json.RootElement.EnumerateObject().Any(p => p.Value.ValueKind == JsonValueKind.Object && p.Value.TryGetProperty("denied", out var denied) && denied.ValueKind == JsonValueKind.True)) NeedsElevation = true;
            Save("hardware.json", json.RootElement);
        }
        finally { if (!process.HasExited) process.Kill(true); }
    }
    private void RecentApplications()
    {
        if (!OperatingSystem.IsWindows()) return;
        var changes = new List<object>();
        foreach (var (log, query) in new[] {
            ("Application", "*[System[TimeCreated[timediff(@SystemTime) <= 604800000] and Provider[@Name='MsiInstaller']]]"),
            ("Microsoft-Windows-AppXDeploymentServer/Operational", "*[System[TimeCreated[timediff(@SystemTime) <= 604800000]]]") })
        {
            try
            {
                using var reader = new EventLogReader(new EventLogQuery(log, PathType.LogName, query) { ReverseDirection = true });
                for (var n = 0; n < 100; n++)
                {
                    token.ThrowIfCancellationRequested(); using var entry = reader.ReadEvent(TimeSpan.FromSeconds(2)); if (entry == null) break;
                    changes.Add(new { source = log, entry.Id, entry.TimeCreated, entry.ProviderName, description = entry.FormatDescription() ?? "" });
                }
            }
            catch (Exception e) when (e is not OperationCanceledException) { NoteAccess(e); changes.Add(new { source = log, status = "unavailable", reason = e.Message }); }
        }
        Save("recent-application-events.json", new { lookbackDays = 7, maxEventsPerSource = 100, events = changes });
        var installed = new List<object>();
        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            var source = $"{hive}/{view}/Software/Microsoft/Windows/CurrentVersion/Uninstall";
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, view).OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall", false);
                foreach (var name in (root?.GetSubKeyNames() ?? []).Take(500))
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        using var key = root!.OpenSubKey(name, false); var product = key?.GetValue("DisplayName") as string;
                        if (string.IsNullOrEmpty(product)) continue;
                        var directory = key?.GetValue("InstallLocation") as string;
                        if (IsIntegration(product) && !string.IsNullOrWhiteSpace(directory)) recordedLocations.Add((product, directory, source + "/InstallLocation"));
                        var icon = key?.GetValue("DisplayIcon") as string;
                        if (IsIntegration(product) && icon != null)
                        {
                            var match = Regex.Match(icon, "^\\\"?([A-Za-z]:[^\\\"]+?\\.exe)", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
                            if (match.Success) recordedLocations.Add((product, match.Groups[1].Value, source + "/DisplayIcon"));
                        }
                        var date = key?.GetValue("InstallDate") as string;
                        if (!DateTime.TryParseExact(date, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsed) || parsed < DateTime.Today.AddDays(-7)) continue;
                        installed.Add(new { source, product = product, version = key?.GetValue("DisplayVersion")?.ToString() ?? "", publisher = key?.GetValue("Publisher")?.ToString() ?? "", installDate = date });
                    }
                    catch (UnauthorizedAccessException e) { NoteAccess(e); installed.Add(new { source, status = "inaccessible-entry" }); }
                }
            }
            catch (Exception e) when (e is not OperationCanceledException) { NoteAccess(e); installed.Add(new { source, status = "unavailable", reason = e.Message }); }
        }
        Save("recent-application-registry.json", new { lookbackDays = 7, maxEntriesExaminedPerView = 500, applications = installed });
    }
    private static bool IsIntegration(string product) => IntegrationCatalog.Match(product) != null;
    private void LocalIntegrations()
    {
        if (!OperatingSystem.IsWindows()) return;
        var candidates = new List<(string Product, string File, string Source)>();
        var executables = IntegrationCatalog.All.ToDictionary(i=>i.Name,i=>i.Executables);
        foreach (var basePath in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs") }.Distinct())
        foreach (var integration in IntegrationCatalog.All)
        foreach (var folder in integration.Folders)
        foreach (var exe in integration.Executables)
            candidates.Add((integration.Name, Path.Combine(basePath, folder, exe), "expected-path"));
        foreach (var (product, directory, source) in recordedLocations.Take(200))
        {
            var kind = IntegrationCatalog.Match(product)!.Name;
            if (directory.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) candidates.Add((kind, directory, source));
            else foreach (var exe in executables[kind]) candidates.Add((kind, Path.Combine(directory, exe), source));
        }
        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        foreach (var (product, files) in executables)
        foreach (var exe in files.Where(f => !f.Contains('\\')))
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, view); using var key = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + exe, false);
                if (key?.GetValue("") is string file) candidates.Add((product, file.Trim('"'), $"{hive}/{view}/App Paths/{exe}"));
            }
            catch (UnauthorizedAccessException) { results.Add(new { integration = product, source = "App Paths", status = "inaccessible" }); }
        }
        var steamRoots = new List<string> { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam") };
        try { using var steam = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam", false); if (steam?.GetValue("SteamPath") is string directory) steamRoots.Add(directory); } catch (UnauthorizedAccessException) { }
        foreach (var steam in steamRoots.Distinct().Take(3).ToArray())
        {
            if (!LocalPath(steam)) continue;
            var libraries = new List<string> { steam };
            try
            {
                var config = Path.Combine(steam, @"steamapps\libraryfolders.vdf");
                if (File.Exists(config) && new FileInfo(config).Length < 128 * 1024 && !HasLink(Path.GetDirectoryName(config)!))
                    foreach (Match match in Regex.Matches(File.ReadAllText(config), "\\\"path\\\"\\s+\\\"([^\\\"]+)\\\"", RegexOptions.None, TimeSpan.FromSeconds(1))) libraries.Add(match.Groups[1].Value.Replace(@"\\", @"\"));
            }
            catch (Exception e) when (e is not OperationCanceledException) { results.Add(new { integration = "VTube Studio", source = "Steam library configuration", error = e.Message }); }
            foreach (var library in libraries.Distinct().Take(20))
            foreach(var integration in IntegrationCatalog.All)
            foreach(var folder in integration.SteamFolders)
            foreach(var exe in integration.Executables)
                candidates.Add((integration.Name, Path.Combine(library, "steamapps", "common", folder, exe), "steam-library"));
        }
        var rows = new List<object>();
        foreach (var (product, file, source) in candidates.Distinct().Take(2000))
        {
            token.ThrowIfCancellationRequested();
            var status = "not-found"; string? version = null; DateTime? modified = null;
            try
            {
                if (!LocalPath(file)) status = "remote-or-invalid-path-skipped";
                else { var info = new FileInfo(file); if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0 || HasLink(info.DirectoryName!)) status = "linked-path-skipped";
                    else { status = "found"; version = FileVersionInfo.GetVersionInfo(file).FileVersion; modified = info.LastWriteTimeUtc; } }
            }
            catch (FileNotFoundException) { } catch (DirectoryNotFoundException) { }
            catch (UnauthorizedAccessException) { status = "inaccessible"; }
            catch (Exception e) { status = "unavailable: " + e.Message; }
            rows.Add(new { product, path = file, source, status, version, lastWriteTimeUtc = modified });
        }
        Save("local-integrations.json", new { integrations = IntegrationCatalog.All.Select(i=>new { product=i.Name, source=i.Source }).ToArray(), maxCandidates=2000, candidates = rows });
    }
    private static bool LocalPath(string path) => Regex.IsMatch(path, @"^[A-Za-z]:[\\/]", RegexOptions.None, TimeSpan.FromSeconds(1)) && !path.Contains("..", StringComparison.Ordinal);
}

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
using System.IO.Compression;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;

namespace MixItUp.WPF.Services.Diagnostics;

public sealed partial class Collector
{
    private const long MaxFile = 128 * 1024 * 1024, MaxTotal = 512 * 1024 * 1024;
    private long collected;
    public bool NeedsElevation { get; private set; }
    private void NoteAccess(Exception error)
    {
        if (error is UnauthorizedAccessException or System.Security.SecurityException || error is System.Diagnostics.Eventing.Reader.EventLogException eventError && (eventError.HResult & 0xffff) == 5) NeedsElevation = true;
    }
    private readonly List<object> results = [];
    private string output = "";
    private CancellationToken token;
    public static readonly string[] Hosts = ["desktop.api.mixitup.bot", "files.mixitup.bot", "support.mixitup.bot", "util.mixitup.bot", "mixitup.bot", "api.twitch.tv", "id.twitch.tv", "www.googleapis.com", "api.kick.com", "id.kick.com", "api.velora.tv", "velora.tv", "vpzone.tv", "discord.com", "api.streamelements.com", "streamlabs.com", "api.elevenlabs.io"];
    public static string? FindInstallation(string localAppData, string runningDirectory)
        => new[] { Path.Combine(localAppData, "MixItUp"), runningDirectory }.FirstOrDefault(p => File.Exists(Path.Combine(p, "MixItUp.exe")) && !HasLink(p));
    public static bool HasLink(string path)
    {
        for (var current = new DirectoryInfo(Path.GetFullPath(path)); current != null; current = current.Parent)
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0) return true;
        return false;
    }
    public static void RestrictDirectory(string path)
    {
        if (!OperatingSystem.IsWindows()) return;
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(true, false);
        security.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(path).SetAccessControl(security);
    }
    public IReadOnlyList<string> Collect(string installation, string staging, IProgress<string> progress, CancellationToken ct, bool includeMachine = true, string? selectedProfile = null, string? evidenceRoot = null)
    {
        token = ct; output = Path.Combine(staging, "report"); Directory.CreateDirectory(output);
        void Step(string name, Action action)
        {
            ct.ThrowIfCancellationRequested(); progress.Report(name + "...");
            try { action(); results.Add(new { step = name, status = "complete" }); }
            catch (OperationCanceledException) { throw; }
            catch (OriginalFileCopyException) { throw; }
            catch (Exception e) { NoteAccess(e); results.Add(new { step = name, status = "partial", error = e.Message, accessDenied = e is UnauthorizedAccessException or System.Security.SecurityException }); }
        }
        if (includeMachine) {
        Step("Reading PC information", () => Save("pc.json", new { os = Environment.OSVersion.ToString(), architecture = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString(), processors = Environment.ProcessorCount, memoryBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes, framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, utility = "0.2.3", elevated = OperatingSystem.IsWindows() && new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator) }));
        Step("Reading network configuration", Network);
        Step("Checking service DNS and TLS", () => Connectivity().GetAwaiter().GetResult());
        Step("Gathering PC info", () => Hardware().GetAwaiter().GetResult());
        }
        Step("Reading installed file inventory", () => Inventory(installation, includeMachine));
        Step("Copying original settings and database", () => Settings(evidenceRoot ?? installation, staging, selectedProfile));
        Step("Collecting the seven newest log files", () => Logs(evidenceRoot ?? installation));
        if (includeMachine) {
        Step("Reading relevant Windows events", Events);
        Step("Reading Windows runtime and TLS configuration", RegistrySettings);
        Step("Reading recent application changes", RecentApplications);
        Step("Checking expected local integration locations", LocalIntegrations);
        }
        Save("collection.json", new { capturedAtUtc = DateTime.UtcNow, results, limits = new { maxFileBytes = MaxFile, maxCollectedBytes = MaxTotal, logFiles = 7, events = 200 } });
        ct.ThrowIfCancellationRequested(); progress.Report("Preparing readable diagnostics files...");
        var dump = new JsonObject();
        foreach (var file in Directory.EnumerateFiles(output, "*.json").Where(f => Path.GetFileName(f) != "settings.json" && Path.GetFileName(f) != "ApplicationSettings.json").Order())
        {
            ct.ThrowIfCancellationRequested(); dump[Path.GetFileNameWithoutExtension(file)] = JsonNode.Parse(File.ReadAllText(file));
        }
        var submission = Path.Combine(staging, "submission"); Directory.CreateDirectory(submission);
        File.WriteAllText(Path.Combine(submission, "diagnostics.json"), dump.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        var databaseDirectory = Path.Combine(output, "original-database");
        if (Directory.Exists(databaseDirectory))
        {
            var archivePath = Path.Combine(submission, "databases.zip");
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
                foreach (var file in Directory.GetFiles(databaseDirectory)) { ct.ThrowIfCancellationRequested(); archive.CreateEntryFromFile(file, Path.GetFileName(file), CompressionLevel.Fastest); }
            using var verify = ZipFile.OpenRead(archivePath);
            foreach (var entry in verify.Entries)
            {
                ct.ThrowIfCancellationRequested(); using var zipped = entry.Open(); using var original = File.OpenRead(Path.Combine(databaseDirectory, entry.FullName));
                if (!SHA256.HashData(zipped).SequenceEqual(SHA256.HashData(original))) throw new OriginalFileCopyException("The database ZIP did not preserve the original bytes. No upload will start.");
            }
        }
        foreach (var file in Directory.EnumerateFiles(output).Where(f => Path.GetFileName(f) is "settings.json" or "ApplicationSettings.json" || Path.GetFileName(f).StartsWith("log-"))) File.Copy(file,Path.Combine(submission,Path.GetFileName(file)));
        var files = Directory.GetFiles(submission).OrderBy(f => Path.GetFileName(f) == "diagnostics.json" ? 0 : 1).ThenBy(Path.GetFileName,StringComparer.Ordinal).ToArray();
        if (files.Length > 11 || files.Sum(f => new FileInfo(f).Length) > MaxTotal || files.Any(f => new FileInfo(f).Length > MaxFile)) throw new IOException("The diagnostics submission exceeded its limits.");
        return files;
    }
    private void Save(string name, object value) => Write(name, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
    private void Write(string name, string text)
    {
        token.ThrowIfCancellationRequested();
        var bytes = Encoding.UTF8.GetBytes(text);
        if (bytes.Length > MaxFile || collected + bytes.Length > MaxTotal) throw new IOException("Collection size limit reached. Remaining details were skipped.");
        File.WriteAllBytes(Path.Combine(output, name), bytes); collected += bytes.Length;
    }
    private void Network()
    {
        var interfaces = NetworkInterface.GetAllNetworkInterfaces().Take(32).Select(n => {
            var p = n.GetIPProperties();
            return new { type = n.NetworkInterfaceType.ToString(), status = n.OperationalStatus.ToString(), n.Speed,
                addresses = p.UnicastAddresses.Select(a => a.Address.ToString()).ToArray(), dns = p.DnsAddresses.Select(a => a.ToString()).ToArray(), gateways = p.GatewayAddresses.Select(a => a.Address.ToString()).ToArray() };
        }).ToArray();
        Save("network.json", interfaces);
    }
    private async Task Connectivity()
    {
        var rows = new List<object>();
        foreach (var host in Hosts)
        {
            token.ThrowIfCancellationRequested();
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromSeconds(8));
            var result = new Dictionary<string, object?> { ["host"] = host };
            try
            {
                result["addresses"] = (await Dns.GetHostAddressesAsync(host, deadline.Token)).Select(a => a.ToString()).ToArray();
                using var client = new TcpClient(); await client.ConnectAsync(host, 443, deadline.Token);
                using var tls = new SslStream(client.GetStream(), false, (_, cert, chain, errors) => {
                    result["tlsErrors"] = errors.ToString();
                    if (cert != null) { using var certificate = new X509Certificate2(cert); result["certificate"] = new { certificate.Subject, certificate.Issuer, certificate.NotBefore, certificate.NotAfter, certificate.Thumbprint }; }
                    result["chainStatus"] = chain?.ChainStatus.Select(s => s.Status.ToString()).ToArray();
                    return errors == SslPolicyErrors.None;
                });
                await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = host, CertificateRevocationCheckMode = X509RevocationMode.NoCheck }, deadline.Token);
                result["protocol"] = tls.SslProtocol.ToString();
                var request = Encoding.ASCII.GetBytes($"HEAD / HTTP/1.1\r\nHost: {host}\r\nConnection: close\r\nUser-Agent: MixItUp-Diagnostics/0.1\r\n\r\n");
                await tls.WriteAsync(request, deadline.Token);
                var buffer = new byte[512]; var count = await tls.ReadAsync(buffer, deadline.Token);
                result["http"] = Encoding.ASCII.GetString(buffer, 0, count).Split('\n')[0].Trim();
            }
            catch (Exception e) when (!token.IsCancellationRequested) { result["error"] = e.Message; }
            rows.Add(result);
        }
        Save("connectivity.json", rows);
    }
    private IEnumerable<string> Files(string directory, int max = 3000)
    {
        if (!Directory.Exists(directory) || HasLink(directory)) yield break;
        var options = new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 5, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = true };
        foreach (var file in Directory.EnumerateFiles(directory, "*", options).Take(max)) { token.ThrowIfCancellationRequested(); yield return file; }
    }
    private void Inventory(string installation, bool verifySignatures)
    {
        var files = Files(installation).Where(file => !Path.GetRelativePath(installation, file).StartsWith(".diagnostics-", StringComparison.OrdinalIgnoreCase)).ToArray();
        var signatures = verifySignatures ? SignatureInventory.Collect(files, token).GetAwaiter().GetResult() : null;
        var hashes = FileHashInventory.Collect(files, token).GetAwaiter().GetResult();
        var rows = new List<object>();
        for (int index = 0; index < files.Length; index++)
        {
            token.ThrowIfCancellationRequested(); var file = files[index]; var info = new FileInfo(file);
            long? length = null; DateTime? modified = null; string? metadataError = null;
            try { length = info.Length; modified = info.LastWriteTimeUtc; } catch (Exception error) { metadataError = error.Message; NoteAccess(error); }
            string? version = null;
            if (info.Extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) || info.Extension.Equals(".dll", StringComparison.OrdinalIgnoreCase))
                try { version = FileVersionInfo.GetVersionInfo(file).FileVersion; } catch { }
            rows.Add(new { path = Path.GetRelativePath(installation, file), Length = length, LastWriteTimeUtc = modified, metadataError, version, hash = hashes[index],
                signature = signatures?[index] ?? (SignatureInventory.IsApplicable(file) ? SignatureInventory.Unavailable("machine-checks-disabled") : SignatureInventory.NotApplicable()) });
        }
        Save("inventory.json", rows);
    }
    public static string[] Profiles(string installation)
    {
        var folder = Path.Combine(installation,"Settings");
        if (!Directory.Exists(folder) || HasLink(folder)) return [];
        return Directory.EnumerateFiles(folder,"*.miu3",SearchOption.TopDirectoryOnly)
            .Where(f => (File.GetAttributes(f) & FileAttributes.ReparsePoint) == 0).OrderBy(Path.GetFileName,StringComparer.Ordinal).Take(100).ToArray();
    }
    private void KeepOriginal(string source, string destination, long limit = MaxFile)
    {
        using var file = OriginalFileCopy.OpenStable(source, limit);
        if (collected + file.Length > MaxTotal) throw new OriginalFileCopyException("The original files exceed the collection size limit. Nothing will be truncated or uploaded.");
        var receipt = OriginalFileCopy.Copy(file, destination, token);
        collected += receipt.Bytes; results.Add(receipt);
    }
    private void Settings(string installation, string staging, string? selectedProfile)
    {
        var profiles = Profiles(installation);
        if (selectedProfile == null && profiles.Length > 1) throw new OriginalFileCopyException("Select the profile to diagnose.");
        selectedProfile ??= profiles.SingleOrDefault();
        if (selectedProfile != null && !profiles.Contains(selectedProfile,StringComparer.OrdinalIgnoreCase)) throw new OriginalFileCopyException("Selected profile is no longer available.");
        var application = Path.Combine(installation, "ApplicationSettings.json");
        if (File.Exists(application)) KeepOriginal(application, Path.Combine(output, "ApplicationSettings.json"));
        else results.Add(new { file = "ApplicationSettings.json", status = "not-found" });
        if (selectedProfile == null) { results.Add(new { step = "profile", status = "not-found" }); return; }
        KeepOriginal(selectedProfile, Path.Combine(output, "settings.json"));
        var database = Path.ChangeExtension(selectedProfile, ".db3");
        if (!File.Exists(database)) { results.Add(new { file = Path.GetFileName(database), status = "not-found" }); return; }
        var destination = Path.Combine(output, "original-database"); Directory.CreateDirectory(destination);
        var held = new List<FileStream>();
        try
        {
            // Keep the database and any journal locked together. Never open these copies in SQLite.
            held.Add(OriginalFileCopy.OpenStable(database, 128 * 1024 * 1024));
            foreach (var suffix in new[] { "-wal", "-journal" })
                if (File.Exists(database + suffix)) held.Add(OriginalFileCopy.OpenStable(database + suffix, 128 * 1024 * 1024));
            if (collected + held.Sum(f => f.Length) > MaxTotal) throw new OriginalFileCopyException("The original database exceeds the collection size limit. Nothing will be truncated or uploaded.");
            foreach (var file in held)
            {
                var receipt = OriginalFileCopy.Copy(file, Path.Combine(destination, Path.GetFileName(file.Name)), token);
                collected += receipt.Bytes; results.Add(receipt);
            }
        }
        finally { foreach (var file in held) file.Dispose(); }
    }
    public static string[] SelectLogs(string directory)
    {
        if(!Directory.Exists(directory) || HasLink(directory)) return [];
        return Directory.EnumerateFiles(directory,"MixItUpLog-*.txt",SearchOption.TopDirectoryOnly)
            .Where(f => (File.GetAttributes(f) & FileAttributes.ReparsePoint) == 0)
            .Select(f => new { File=f, Stamp=Path.GetFileNameWithoutExtension(f)[11..] })
            .Where(f => DateTime.TryParseExact(f.Stamp,"yyyy-MM-dd-HH-mm-ss",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out _))
            .OrderByDescending(f => f.Stamp,StringComparer.Ordinal).ThenBy(f => f.File,StringComparer.Ordinal).Take(7).Select(f => f.File).ToArray();
    }
    private void Logs(string installation)
    {
        var selected = SelectLogs(Path.Combine(installation, "Logs"));
        for (int index = 0; index < selected.Length; index++)
        {
            token.ThrowIfCancellationRequested();
            KeepOriginal(selected[index], Path.Combine(output, $"log-{index}.txt"));
        }
        results.Add(new { step = "logs", uploadedFiles = selected.Length, requestedFiles = 7, selection = "Seven newest filename timestamps, regardless of date. Original bytes, without filtering or truncation." });
    }
    private void Events()
    {
        if (!OperatingSystem.IsWindows()) return;
        var query = new EventLogQuery("Application", PathType.LogName,
            "*[System[TimeCreated[timediff(@SystemTime) <= 604800000] and (Provider[@Name='Application Error'] or Provider[@Name='.NET Runtime'] or Provider[@Name='Windows Error Reporting'])]]") { ReverseDirection = true };
        using var reader = new EventLogReader(query); var entries = new List<object>();
        for (int n = 0; n < 200; n++)
        {
            token.ThrowIfCancellationRequested(); using var entry = reader.ReadEvent(TimeSpan.FromSeconds(2)); if (entry == null) break;
            var description = entry.FormatDescription() ?? "";
            if (description.Contains("MixItUp", StringComparison.OrdinalIgnoreCase)) entries.Add(new { entry.Id, entry.TimeCreated, entry.ProviderName, description = description });
        }
        Save("events.json", entries);
    }
    private void RegistrySettings()
    {
        if (!OperatingSystem.IsWindows()) return;
        var entries = new List<object>();
        var locations = new[] {
            (@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full", new[] { "Release", "Version", "Install" }),
            (@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", new[] { "ProductName", "CurrentBuildNumber", "UBR", "DisplayVersion" }),
            (@"SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.2\Client", new[] { "Enabled", "DisabledByDefault" }),
            (@"SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.3\Client", new[] { "Enabled", "DisabledByDefault" }) };
        foreach (var (path, values) in locations)
        {
            token.ThrowIfCancellationRequested(); using var key = Registry.LocalMachine.OpenSubKey(path, false);
            entries.Add(new { path, values = values.ToDictionary(v => v, v => key?.GetValue(v)?.ToString()) });
        }
        using var proxy = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings", false);
        entries.Add(new { path = "Internet Settings", proxyEnabled = proxy?.GetValue("ProxyEnable"), proxyServer = proxy?.GetValue("ProxyServer"), autoConfigUrl = proxy?.GetValue("AutoConfigURL") });
        Save("registry.json", entries);
    }
}

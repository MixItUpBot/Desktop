using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace MixItUp.Updates
{
    internal static class Program
    {
        private const string Journal = "rollback.txt";
        private static readonly string[] Protected = { PendingUpdate.CacheName, "Settings", "Backups", "Logs", "Profiles", "Users", "ApplicationSettings.json", "ApplicationSettings.xml", PendingUpdate.EulaReceipt };

        private static int Main(string[] args)
        {
            if (args.Length < 1 || !int.TryParse(args[0], out int pid)) { return 2; }
            string cache = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            if (!string.Equals(Path.GetFileName(cache), PendingUpdate.CacheName, StringComparison.OrdinalIgnoreCase)) { return 2; }
            string root = Path.GetDirectoryName(cache);
            string app = Path.Combine(root, "MixItUp.exe");
            if (!File.Exists(app) || IsLink(cache)) { return 2; }
            FileStream updateLock;
            try { updateLock = File.Open(Path.Combine(cache, "apply.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) { return 0; } // Another bootstrapper owns this activation.
            using (updateLock)
            {
                var launchArguments = args.Skip(1).ToList();
                launchArguments.Add("--skip-background-update");
                var launch = new ProcessStartInfo(app, string.Join(" ", launchArguments.Select(Quote))) { WorkingDirectory = root, UseShellExecute = false };
                try
                {
                    try
                    {
                        using (Process parent = Process.GetProcessById(pid))
                        {
                            if (!string.Equals(parent.MainModule.FileName, app, StringComparison.OrdinalIgnoreCase) || !parent.WaitForExit(60000)) { return 1; }
                        }
                    }
                    catch (ArgumentException) { } // Parent exited before the helper opened it.
                    for (int attempt = 0; IsRunning(app); attempt++)
                    {
                        if (attempt >= 20) { return 1; }
                        Thread.Sleep(250);
                    }
                    Recover(root, cache);
                    PendingUpdate update = PendingUpdate.Read(cache);
                    if (update != null && update.LicenseAccepted(root)) { Apply(root, cache, update); }
                    using (Process.Start(launch)) { }
                    File.Delete(Path.Combine(cache, Journal));
                    if (update != null)
                    {
                        File.Delete(Path.Combine(cache, PendingUpdate.StateName));
                        File.Delete(Path.Combine(cache, update.ArchiveName));
                        File.Delete(Path.Combine(cache, update.ArchiveName + ".eula"));
                    }
                    RemoveWorkDirectory(Path.Combine(cache, "previous"));
                    RemoveWorkDirectory(Path.Combine(cache, "incoming"));
                    return 0;
                }
                catch (Exception ex)
                {
                    Log(cache, ex);
                    try { Recover(root, cache); }
                    catch (Exception recoveryError) { Log(cache, recoveryError); return 1; }
                    if (ex is InvalidDataException) { File.Delete(Path.Combine(cache, PendingUpdate.StateName)); }
                    try { if (!IsRunning(app)) { using (Process.Start(launch)) { } } }
                    catch (Exception launchError) { Log(cache, launchError); }
                    return 1;
                }
            }
        }

        private static void Apply(string root, string cache, PendingUpdate update)
        {
            string archive = Path.Combine(cache, update.ArchiveName);
            string incoming = Path.Combine(cache, "incoming");
            string previous = Path.Combine(cache, "previous");
            RemoveWorkDirectory(incoming);
            RemoveWorkDirectory(previous);
            Directory.CreateDirectory(incoming);
            Directory.CreateDirectory(previous);
            var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var input = File.Open(archive, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var sha = SHA256.Create())
            {
                if (!string.Equals(BitConverter.ToString(sha.ComputeHash(input)).Replace("-", ""), update.Sha256, StringComparison.OrdinalIgnoreCase))
                    { throw new InvalidDataException("Downloaded update checksum mismatch."); }
                input.Position = 0;
                using (var zip = new ZipArchive(input, ZipArchiveMode.Read))
                {
                    foreach (var entry in zip.Entries)
                    {
                        if (entry.Name.Length == 0) { continue; }
                        string name = entry.FullName.Replace('/', '\\');
                        if (name.StartsWith(".\\", StringComparison.Ordinal)) { name = name.Substring(2); }
                        Destination(root, name);
                        if (!files.Add(name)) { throw new InvalidDataException("Duplicate package entry."); }
                        string path = Destination(incoming, name);
                        Directory.CreateDirectory(Path.GetDirectoryName(path));
                        entry.ExtractToFile(path);
                    }
                }
            }
            foreach (string required in new[] { "MixItUp.exe", "MixItUp.dll", "MixItUp.Base.dll", PendingUpdate.HelperName })
                { if (!files.Contains(required)) { throw new InvalidDataException("Incomplete update package: " + required); } }
            Version release = Version.Parse(update.Version);
            var image = FileVersionInfo.GetVersionInfo(Path.Combine(incoming, "MixItUp.exe"));
            if (image.FileMajorPart != release.Major || image.FileMinorPart != release.Minor || image.FileBuildPart != release.Build)
                { throw new InvalidDataException("Package executable version differs from the manifest."); }
            var installed = FileVersionInfo.GetVersionInfo(Path.Combine(root, "MixItUp.exe"));
            if (new Version(installed.FileMajorPart, installed.FileMinorPart, installed.FileBuildPart) >= release)
            {
                File.Delete(Path.Combine(cache, PendingUpdate.StateName));
                RemoveWorkDirectory(incoming);
                RemoveWorkDirectory(previous);
                File.Delete(archive);
                return;
            }

            var ordered = files.OrderBy(name => string.Equals(name, "MixItUp.exe", StringComparison.OrdinalIgnoreCase) ? 1 : 0).ToList();
            string journal = Path.Combine(cache, Journal);
            using (var stream = File.Create(journal + ".tmp"))
            using (var writer = new StreamWriter(stream))
            {
                foreach (string name in ordered) { writer.WriteLine((File.Exists(Destination(root, name)) ? "O" : "N") + name); }
                writer.Flush();
                stream.Flush(true);
            }
            File.Move(journal + ".tmp", journal);
            foreach (string name in ordered)
            {
                string dest = Destination(root, name);
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                if (File.Exists(dest))
                {
                    string backup = Destination(previous, name);
                    Directory.CreateDirectory(Path.GetDirectoryName(backup));
                    File.Copy(dest, backup + ".part");
                    File.Move(backup + ".part", backup);
                }
                ReplaceFile(Destination(incoming, name), dest);
            }
            // Keep rollback data until Windows successfully creates the new root process.
        }

        private static void Recover(string root, string cache)
        {
            string journal = Path.Combine(cache, Journal);
            if (!File.Exists(journal)) { return; }
            string previous = Path.Combine(cache, "previous");
            foreach (string line in File.ReadAllLines(journal).Reverse())
            {
                if (line.Length < 2 || (line[0] != 'O' && line[0] != 'N')) { throw new InvalidDataException("Invalid update journal."); }
                string name = line.Substring(1);
                string dest = Destination(root, name);
                string backup = Destination(previous, name);
                if (line[0] == 'N') { File.Delete(dest); }
                else if (File.Exists(backup))
                {
                    ReplaceFile(backup, dest);
                }
            }
            File.Delete(journal);
            RemoveWorkDirectory(previous);
            RemoveWorkDirectory(Path.Combine(cache, "incoming"));
        }

        private static string Destination(string root, string name)
        {
            string[] components = name.Split('\\');
            if (Path.IsPathRooted(name) || Protected.Contains(components[0], StringComparer.OrdinalIgnoreCase)
                || components.Any(p => p.Length == 0 || p == "." || p == ".." || p.EndsWith(".") || p.EndsWith(" ") || p.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
                { throw new InvalidDataException("Invalid update path: " + name); }
            string current = root;
            foreach (string component in components)
            {
                current = Path.Combine(current, component);
                if ((Directory.Exists(current) || File.Exists(current)) && IsLink(current)) { throw new IOException("Linked update path: " + current); }
            }
            return current;
        }

        private static bool IsRunning(string executable)
        {
            foreach (var process in Process.GetProcessesByName("MixItUp"))
            {
                using (process)
                {
                    try { if (string.Equals(process.MainModule.FileName, executable, StringComparison.OrdinalIgnoreCase)) { return true; } }
                    catch (InvalidOperationException) { } // Process exited during enumeration.
                    catch (System.ComponentModel.Win32Exception) { } // Another user's process.
                }
            }
            return false;
        }

        private static bool IsLink(string path) => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool MoveFileEx(string source, string target, int flags);

        private static void ReplaceFile(string source, string target)
        {
            // Both files are on the installation volume. Keep the target name valid through the swap.
            if (!MoveFileEx(source, target, 0x1 | 0x8)) { throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()); }
        }

        private static void RemoveWorkDirectory(string path)
        {
            if (!Directory.Exists(path)) { return; }
            if (IsLink(path)) { throw new IOException("Linked working directory."); }
            foreach (string dir in Directory.GetDirectories(path)) { RemoveWorkDirectory(dir); }
            foreach (string file in Directory.GetFiles(path)) { File.Delete(file); }
            Directory.Delete(path);
        }

        private static string Quote(string value)
        {
            var result = new StringBuilder("\"");
            int slashes = 0;
            foreach (char ch in value)
            {
                if (ch == '\\') { slashes++; continue; }
                result.Append('\\', ch == '"' ? slashes * 2 + 1 : slashes);
                result.Append(ch);
                slashes = 0;
            }
            return result.Append('\\', slashes * 2).Append('"').ToString();
        }

        private static void Log(string cache, Exception ex)
        {
            try { File.AppendAllText(Path.Combine(cache, "update.log"), DateTime.UtcNow.ToString("o") + " " + ex + Environment.NewLine); }
            catch (IOException) { }
        }
    }
}

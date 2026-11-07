using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace MixItUp.Uninstaller
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        public const string MixItUpProcessName = "MixItUp";

        public static readonly string DefaultInstallDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MixItUp");

        public static readonly string StartMenuDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Mix It Up");

        public const string ShortcutFileName = "Mix It Up.lnk";

        public static string DesktopShortcutFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop), ShortcutFileName);

        public const string SoftwareClassesRegistryPath = @"SOFTWARE\Classes\";
        public const string MixItUpCommandFileExtension = ".miucommand";
        public const string FileAssociationProgramID = "MixItUp.MIUCommand.1";
        public const string URIProtocolActivationHeader = "mixitup";

        public const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
        public static readonly Guid UninstallGuid = new Guid("9BED7BA2-4237-4826-B4C3-F3BB97F01151");

        public event PropertyChangedEventHandler PropertyChanged;

        private string installDirectory;
        private string logFilePath;

        #region Bindable Properties

        public bool KeepSettings
        {
            get => keepSettings;
            set { keepSettings = value; NotifyPropertyChanged(); }
        }
        private bool keepSettings = true;

        public string SettingsPath => Path.Combine(DefaultInstallDirectory, "Settings");

        public bool ShowConfirmation
        {
            get => showConfirmation;
            private set { showConfirmation = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(ShowCloseButton)); }
        }
        private bool showConfirmation = true;

        public bool ShowProgress
        {
            get => showProgress;
            private set { showProgress = value; NotifyPropertyChanged(); }
        }
        private bool showProgress;

        public bool ShowCompletion
        {
            get => showCompletion;
            private set { showCompletion = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(ShowCloseButton)); }
        }
        private bool showCompletion;

        public bool ShowError
        {
            get => showError;
            private set { showError = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(ShowCloseButton)); }
        }
        private bool showError;

        public bool ShowCloseButton => ShowCompletion || ShowError;

        public string StatusText
        {
            get => statusText;
            private set { statusText = value; NotifyPropertyChanged(); }
        }
        private string statusText = "Preparing...";

        public string DetailText
        {
            get => detailText;
            private set { detailText = value; NotifyPropertyChanged(); }
        }
        private string detailText = "";

        public int Progress
        {
            get => progress;
            private set { progress = value; NotifyPropertyChanged(); }
        }
        private int progress;

        public bool IsIndeterminate
        {
            get => isIndeterminate;
            private set { isIndeterminate = value; NotifyPropertyChanged(); }
        }
        private bool isIndeterminate = true;

        public string CompletionTitle
        {
            get => completionTitle;
            private set { completionTitle = value; NotifyPropertyChanged(); }
        }
        private string completionTitle = "Uninstallation Complete";

        public string CompletionMessage
        {
            get => completionMessage;
            private set { completionMessage = value; NotifyPropertyChanged(); }
        }
        private string completionMessage;

        public string ErrorMessage
        {
            get => errorMessage;
            private set { errorMessage = value; NotifyPropertyChanged(); }
        }
        private string errorMessage;

        #endregion

        public MainWindowViewModel()
        {
            this.installDirectory = DefaultInstallDirectory;

            string[] args = Environment.GetCommandLineArgs();
            if (args.Length >= 2)
            {
                string candidatePath = args[1].Trim('"', ' ');
                if (Directory.Exists(candidatePath) && IsValidInstallDirectory(candidatePath))
                {
                    this.installDirectory = candidatePath;
                }
            }
        }

        public async Task<bool> RunUninstallAsync()
        {
            bool success = false;

            ShowConfirmation = false;
            ShowProgress = true;
            IsIndeterminate = true;

            try
            {
                this.logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MixItUp-Uninstaller-Log.txt");
                WriteLog("Uninstallation started: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                WriteLog("Install directory: " + this.installDirectory);
                WriteLog("Keep settings: " + this.KeepSettings);
            }
            catch { }

            await Task.Run(async () =>
            {
                try
                {
                    UpdateStatus("Waiting for Mix It Up to close...", 0);
                    if (!await WaitForMixItUpToCloseAsync())
                    {
                        SetError("Mix It Up is still running. Please close it and try again.");
                        return;
                    }

                    UpdateStatus("Removing shortcuts...", 20);
                    await Task.Delay(100);
                    DeleteShortcuts();

                    UpdateStatus("Cleaning up registry...", 40);
                    await Task.Delay(100);
                    DeleteRegistryEntries();

                    UpdateStatus("Removing application files...", 60);
                    await Task.Delay(100);
                    DeleteApplicationFiles();

                    UpdateStatus("Finalizing...", 90);
                    await Task.Delay(200);

                    success = true;
                    WriteLog("Uninstallation completed successfully.");
                }
                catch (Exception ex)
                {
                    WriteLog("Uninstallation error: " + ex);
                    SetError("An error occurred during uninstallation: " + ex.Message);
                }
            });

            return success;
        }

        public void ShowCompletionState()
        {
            ShowProgress = false;
            ShowCompletion = true;
            CompletionTitle = "Done";

            if (KeepSettings)
            {
                CompletionMessage = "Mix It Up has been removed.\n\nYour settings were kept.";
            }
            else
            {
                CompletionMessage = "Mix It Up has been removed, including all settings.";
            }
        }

        #region Uninstall Operations

        private async Task<bool> WaitForMixItUpToCloseAsync()
        {
            for (int attempt = 0; attempt < 15; attempt++)
            {
                bool isRunning = false;

                foreach (Process process in Process.GetProcesses())
                {
                    try
                    {
                        if (process.ProcessName.Equals(MixItUpProcessName, StringComparison.OrdinalIgnoreCase))
                        {
                            isRunning = true;

                            if (attempt >= 5)
                            {
                                try
                                {
                                    process.CloseMainWindow();
                                    DetailText = "Requesting Mix It Up to close...";
                                }
                                catch { }
                            }
                        }
                    }
                    catch { }
                }

                if (!isRunning)
                {
                    return true;
                }

                await Task.Delay(1000);
            }

            return false;
        }

        private void DeleteShortcuts()
        {
            try
            {
                if (Directory.Exists(StartMenuDirectory))
                {
                    Directory.Delete(StartMenuDirectory, recursive: true);
                    WriteLog("Deleted Start Menu directory: " + StartMenuDirectory);
                }
            }
            catch (Exception ex)
            {
                WriteLog("Failed to delete Start Menu directory: " + ex.Message);
            }

            try
            {
                if (File.Exists(DesktopShortcutFilePath))
                {
                    File.Delete(DesktopShortcutFilePath);
                    WriteLog("Deleted desktop shortcut: " + DesktopShortcutFilePath);
                }
            }
            catch (Exception ex)
            {
                WriteLog("Failed to delete desktop shortcut: " + ex.Message);
            }
        }

        private void DeleteRegistryEntries()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true))
                {
                    string startupCommand = "\"" + Path.Combine(this.installDirectory, "MixItUp.exe") + "\"";
                    if (string.Equals(key?.GetValue("MixItUp") as string, startupCommand, StringComparison.OrdinalIgnoreCase))
                    {
                        key.DeleteValue("MixItUp", false);
                    }
                }
            }
            catch (Exception ex) { WriteLog("Failed to remove startup registration: " + ex.Message); }

            DeleteRegistryKey(SoftwareClassesRegistryPath, URIProtocolActivationHeader);
            DeleteRegistryKey(SoftwareClassesRegistryPath, MixItUpCommandFileExtension);
            DeleteRegistryKey(SoftwareClassesRegistryPath, FileAssociationProgramID);

            string uninstallGuidString = UninstallGuid.ToString("B");
            DeleteRegistryKey(UninstallKey, uninstallGuidString);
        }

        private void DeleteRegistryKey(string parentPath, string keyName)
        {
            if (string.IsNullOrWhiteSpace(keyName))
            {
                return;
            }

            try
            {
                using (RegistryKey parentKey = Registry.CurrentUser.OpenSubKey(parentPath, writable: true))
                {
                    if (parentKey != null)
                    {
                        using (RegistryKey testKey = parentKey.OpenSubKey(keyName))
                        {
                            if (testKey != null)
                            {
                                parentKey.DeleteSubKeyTree(keyName, throwOnMissingSubKey: false);
                                WriteLog($"Deleted registry key: HKCU\\{parentPath}{keyName}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLog($"Failed to delete registry key {parentPath}{keyName}: " + ex.Message);
            }
        }

        private void DeleteApplicationFiles()
        {
            if (!Directory.Exists(this.installDirectory))
            {
                WriteLog("Install directory does not exist: " + this.installDirectory);
                return;
            }

            int deletedFiles = 0;
            int failedFiles = 0;

            try
            {
                foreach (string filePath in Directory.GetFiles(this.installDirectory))
                {
                    try
                    {
                        File.Delete(filePath);
                        deletedFiles++;
                    }
                    catch (Exception ex)
                    {
                        WriteLog($"Failed to delete file {filePath}: " + ex.Message);
                        failedFiles++;
                    }
                }

                WriteLog($"Deleted {deletedFiles} files ({failedFiles} failed).");
            }
            catch (Exception ex)
            {
                WriteLog("Error enumerating files: " + ex.Message);
            }

            int deletedDirs = 0;
            int skippedDirs = 0;

            try
            {
                foreach (string dirPath in Directory.GetDirectories(this.installDirectory))
                {
                    string dirName = new DirectoryInfo(dirPath).Name;

                    if (dirName.Equals("Uninstaller", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (dirName.StartsWith("Settings", StringComparison.OrdinalIgnoreCase))
                    {
                        if (KeepSettings)
                        {
                            WriteLog($"Preserved settings directory: {dirPath}");
                            skippedDirs++;
                            continue;
                        }
                        else
                        {
                            WriteLog($"Deleting settings directory: {dirPath}");
                        }
                    }

                    if (dirName.Equals(".tmp", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            Directory.Delete(dirPath, recursive: true);
                            deletedDirs++;
                        }
                        catch { }
                        continue;
                    }

                    try
                    {
                        Directory.Delete(dirPath, recursive: true);
                        deletedDirs++;
                    }
                    catch (Exception ex)
                    {
                        WriteLog($"Failed to delete directory {dirPath}: " + ex.Message);
                    }
                }

                WriteLog($"Deleted {deletedDirs} directories ({skippedDirs} preserved).");
            }
            catch (Exception ex)
            {
                WriteLog("Error enumerating directories: " + ex.Message);
            }

            if (!KeepSettings)
            {
                try
                {
                    if (Directory.GetFiles(this.installDirectory).Length == 0 &&
                        Directory.GetDirectories(this.installDirectory).Length == 0)
                    {
                        Directory.Delete(this.installDirectory);
                        WriteLog("Deleted install directory: " + this.installDirectory);
                    }
                }
                catch (Exception ex)
                {
                    WriteLog("Could not delete install directory: " + ex.Message);
                }
            }
        }

        #endregion

        #region Helpers

        private bool IsValidInstallDirectory(string path)
        {
            string exePath = Path.Combine(path, "MixItUp.exe");
            return File.Exists(exePath) || Directory.Exists(Path.Combine(path, "Settings"));
        }

        private void UpdateStatus(string status, int progressValue)
        {
            StatusText = status;
            Progress = progressValue;
            IsIndeterminate = false;
            DetailText = "";
        }

        private void SetError(string message)
        {
            ErrorMessage = message;
            ShowProgress = false;
            ShowError = true;
            WriteLog("ERROR: " + message);
        }

        private void WriteLog(string message)
        {
            if (string.IsNullOrEmpty(this.logFilePath))
            {
                return;
            }

            try
            {
                File.AppendAllText(this.logFilePath,
                    $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch { }
        }

        protected void NotifyPropertyChanged([CallerMemberName] string name = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        #endregion
    }
}

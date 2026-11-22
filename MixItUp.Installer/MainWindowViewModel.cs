using MixItUp.Base.Model.API;
using MixItUp.Base.Model.API.Files.V2;
using MixItUp.Base.Util;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;
using System.Text;

namespace MixItUp.Installer
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        public const string InstallerLogFileName = "MixItUp-Installer-Log.txt";
        public const string ShortcutFileName = "Mix It Up.lnk";

        public const string OldApplicationSettingsFileName = "ApplicationSettings.xml";
        public const string NewApplicationSettingsFileName = "ApplicationSettings.json";

        public const string MixItUpProcessName = "MixItUp";
        public const string AutoHosterProcessName = "MixItUp.AutoHoster";

        private static readonly Version minimumOSVersion = new Version(10, 0, 0, 0);

        public static readonly string DefaultInstallDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MixItUp");
        public static readonly string StartMenuDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Mix It Up");

        public static string InstallSettingsDirectory { get { return Path.Combine(MainWindowViewModel.DefaultInstallDirectory, "Settings"); } }

        private const string FileServiceBaseUrl = BuildChannelHelper.API_FILES_UPDATE_ROOT; //"https://files.mixitup.bot/apps/mixitup-desktop/windows-x64";
        private const string TempDirectoryName = ".tmp";
        private const string EulaAcceptedFileName = "eula-accepted";
        private static readonly TimeSpan[] ManifestRetryDelays = new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8) };
        private static readonly TimeSpan[] DownloadRetryDelays = new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8) };
        private static readonly HttpClient HttpClient = new HttpClient();

        private UpdateVersionCheckModel versionCheck;
        private UpdateVersionManifestModel versionManifest;
        private string manifestChannel;
        private string manifestUrl;
        private string targetVersionOverride;
        private string targetChannelOverride;
        private string downloadedPackagePath;
        private string tempDirectoryPath;
        private readonly string installDirectoryArgument;
        private string installDirectoryResolutionNote;

        public Func<string, string, Task<bool>> ShowEulaDialogAsync { private get; set; }

        public static string StartMenuShortCutFilePath { get { return Path.Combine(StartMenuDirectory, ShortcutFileName); } }
        public static string DesktopShortCutFilePath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), ShortcutFileName); } }

        public event PropertyChangedEventHandler PropertyChanged;

        public bool IsUpdate
        {
            get { return this.isUpdate; }
            private set
            {
                this.isUpdate = value;
                this.NotifyPropertyChanged();
                this.NotifyPropertyChanged("IsInstall");
            }
        }
        private bool isUpdate;

        public bool IsInstall { get { return !this.IsUpdate; } }

        public bool IsPreview
        {
            get { return this.isPreview; }
            private set
            {
                this.isPreview = value;
                this.NotifyPropertyChanged();
            }
        }
        private bool isPreview;

        public bool IsTest
        {
            get { return this.isTest; }
            private set
            {
                this.isTest = value;
                this.NotifyPropertyChanged();
            }
        }
        private bool isTest;

        public bool IsOperationBeingPerformed
        {
            get { return this.isOperationBeingPerformed; }
            private set
            {
                this.isOperationBeingPerformed = value;
                this.NotifyPropertyChanged();
            }
        }
        private bool isOperationBeingPerformed;

        public bool IsOperationIndeterminate
        {
            get { return this.isOperationIndeterminate; }
            private set
            {
                this.isOperationIndeterminate = value;
                this.NotifyPropertyChanged();
            }
        }
        private bool isOperationIndeterminate;

        public int OperationProgress
        {
            get { return this.operationProgress; }
            private set
            {
                this.operationProgress = value;
                this.NotifyPropertyChanged();
            }
        }
        private int operationProgress;

        public string DisplayText1
        {
            get { return this.displayText1; }
            private set
            {
                this.displayText1 = value;
                this.NotifyPropertyChanged();
            }
        }
        private string displayText1;

        public string DisplayText2
        {
            get { return this.displayText2; }
            private set
            {
                this.displayText2 = value;
                this.NotifyPropertyChanged();
            }
        }
        private string displayText2;

        public bool ErrorOccurred
        {
            get { return this.errorOccurred; }
            private set
            {
                this.errorOccurred = value;
                this.NotifyPropertyChanged();
            }
        }
        private bool errorOccurred;

        public string SpecificErrorMessage
        {
            get { return this.specificErrorMessage; }
            private set
            {
                this.specificErrorMessage = value;
                this.NotifyPropertyChanged();
            }
        }
        private string specificErrorMessage;

        public string HyperlinkAddress
        {
            get { return this.hyperlinkAddress; }
            private set
            {
                this.hyperlinkAddress = value;
                this.NotifyPropertyChanged();
                this.NotifyPropertyChanged("ShowHyperlinkAddress");
            }
        }
        private string hyperlinkAddress;

        public bool ShowHyperlinkAddress { get { return !string.IsNullOrEmpty(this.HyperlinkAddress); } }

        private string installDirectory;

        public MainWindowViewModel()
        {
            this.installDirectory = DefaultInstallDirectory;
            this.installDirectoryArgument = null;
            this.installDirectoryResolutionNote = null;

            string[] args = Environment.GetCommandLineArgs();

            for (int i = 2; i < args.Length; i++)
            {
                string arg = args[i].Trim('"');
                if (arg.StartsWith("--target-version=", StringComparison.OrdinalIgnoreCase))
                {
                    this.targetVersionOverride = arg.Substring("--target-version=".Length).Trim();
                }
                else if (arg.StartsWith("--target-channel=", StringComparison.OrdinalIgnoreCase))
                {
                    this.targetChannelOverride = arg.Substring("--target-channel=".Length).Trim();
                }
            }

            if (args.Length >= 2)
            {
                this.installDirectoryArgument = args[1];
                this.installDirectory = SanitizeInstallDirectoryArgument(args[1]);
                bool updating = this.targetChannelOverride != null || this.targetVersionOverride != null;
                if (!Directory.Exists(this.installDirectory) || (updating && !DoesDirectoryContainExistingInstall(this.installDirectory)))
                {
                    this.installDirectoryResolutionNote = new DirectoryNotFoundException().Message;
                }
            }

            if (DoesDirectoryContainExistingInstall(this.installDirectory))
            {
                this.IsUpdate = true;
                string applicationSettingsFilePath = Path.Combine(this.installDirectory, NewApplicationSettingsFileName);
                if (!File.Exists(applicationSettingsFilePath))
                {
                    applicationSettingsFilePath = Path.Combine(this.installDirectory, OldApplicationSettingsFileName);
                }

                if (File.Exists(applicationSettingsFilePath))
                {
                    using (StreamReader reader = new StreamReader(File.OpenRead(applicationSettingsFilePath)))
                    {
                        JObject jobj = JObject.Parse(reader.ReadToEnd());
                        if (jobj != null)
                        {
                            if (jobj.ContainsKey("PreviewProgram"))
                            {
                                this.IsPreview = jobj["PreviewProgram"].ToObject<bool>();
                            }

                            if (jobj.ContainsKey("TestBuild"))
                            {
                                this.IsTest = jobj["TestBuild"].ToObject<bool>();
                            }
                        }
                    }
                }

            }

            if (this.IsTest)
            {
                this.IsPreview = true;
            }

            this.DisplayText1 = "Preparing installation...";
            this.isOperationBeingPerformed = true;
            this.IsOperationIndeterminate = true;
        }

        public bool CheckCompatability()
        {
            if (this.installDirectoryResolutionNote != null)
            {
                this.ShowError(this.installDirectoryResolutionNote, this.installDirectoryArgument);
                return false;
            }
            if (Environment.OSVersion.Version < minimumOSVersion)
            {
                this.ShowError(
                    $"Mix It Up only runs on Windows 10 & higher.\nDetected Version: {Environment.OSVersion.Version}",
                    $"If incorrect, please contact support@mixitupapp.com\nDiscord: https://mixitup.bot/discord");
                return false;
            }
            return true;
        }

        public async Task<bool> Run()
        {
            bool result = false;

            await Task.Run(async () =>
            {
                try
                {
                    File.Delete(InstallerLogFileName);
                    this.WriteToLogFile("Installation started: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    this.WriteToLogFile("OS Version: " + Environment.OSVersion.Version.ToString());

                    if (!string.IsNullOrEmpty(this.installDirectoryArgument))
                    {
                        this.WriteToLogFile("Install directory argument received: " + this.installDirectoryArgument);
                    }
                    if (!string.IsNullOrEmpty(this.installDirectoryResolutionNote))
                    {
                        this.WriteToLogFile(this.installDirectoryResolutionNote);
                    }
                    this.WriteToLogFile("Resolved install directory: " + this.installDirectory);

                    if (!this.IsUpdate || await this.WaitForMixItUpToClose())
                    {
                        UpdateVersionManifestModel manifest = await this.FetchManifestAsync();
                        if (manifest == null)
                        {
                            if (!this.ErrorOccurred)
                            {
                                this.ShowNetworkRetryError("We were unable to retrieve update information from the Mix It Up file service. Please check your network connection and try again.");
                            }
                            return;
                        }

                        UpdateStepModel downloadStep = this.versionManifest.GetDownloadStep();
                        if (downloadStep == null || string.IsNullOrEmpty(downloadStep.target))
                        {
                            this.SpecificErrorMessage = "The update manifest did not include a package download step.";
                            this.ShowError("Invalid update manifest.", this.SpecificErrorMessage);
                            return;
                        }

                        string constructedPackageUrl;
                        if (downloadStep.target.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                            downloadStep.target.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                        {
                            constructedPackageUrl = downloadStep.target;
                        }
                        else
                        {
                            constructedPackageUrl = string.Format("{0}/{1}/{2}/{3}", FileServiceBaseUrl, this.manifestChannel, this.versionManifest.version, downloadStep.target);
                        }

                        UpdateStepModel verifyStep = this.versionManifest.GetVerifyStep();

                        this.WriteToLogFile(string.Format("Manifest summary:{0}- Channel: {1}{0}- Version check URL: {2}{0}- Manifest URL: {3}{0}- Latest version: {4}{0}- Minimum version: {5}{0}- Update paused: {6}{0}- Package URL: {7}{0}- Expected SHA-256: {8}{0}- Installer URL: {9}",
                            Environment.NewLine,
                            this.manifestChannel ?? "<unknown>",
                            string.Format("{0}/{1}/latest", FileServiceBaseUrl, this.manifestChannel),
                            this.manifestUrl ?? "<unknown>",
                            this.versionCheck?.latestVersion ?? "<unknown>",
                            this.versionCheck?.minimumVersion ?? "<unknown>",
                            this.versionCheck?.updatePaused.ToString() ?? "<unknown>",
                            constructedPackageUrl,
                            verifyStep?.value ?? "<missing>",
                            this.versionManifest.installer ?? "<missing>"));

                        if (!await this.EnsureEulaAcceptedAsync())
                        {
                            return;
                        }

                        if (await this.DownloadPackageAsync())
                        {
                            if (this.InstallMixItUp())
                            {
                                if (string.Equals(this.installDirectory.TrimEnd(Path.DirectorySeparatorChar), DefaultInstallDirectory, StringComparison.OrdinalIgnoreCase)
                                    && !this.CreateMixItUpShortcut())
                                {
                                    this.WriteToLogFile("Shortcut creation did not complete successfully.");
                                }

                                result = true;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.WriteToLogFile(ex.ToString());
                }
            });

            if (!result && !this.ErrorOccurred)
            {
                if (!string.IsNullOrEmpty(this.SpecificErrorMessage))
                {
                    this.HyperlinkAddress = InstallerLogFileName;
                    this.ShowError(string.Format("{0} file created:", InstallerLogFileName), this.SpecificErrorMessage);
                }
                else
                {
                    this.HyperlinkAddress = InstallerLogFileName;
                    this.ShowError(string.Format("{0} file created:", InstallerLogFileName), "An installation error occured. Please visit our support Discord or send an email to support@mixitupapp.com with the contents of this file.");
                }
            }
            return result;
        }

        public void Launch()
        {
            Process.Start(new ProcessStartInfo(Path.Combine(this.installDirectory, "MixItUp.exe"))
            {
                WorkingDirectory = this.installDirectory,
                UseShellExecute = false
            });
        }

        protected void NotifyPropertyChanged([CallerMemberName] string name = "")
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private async Task<bool> WaitForMixItUpToClose()
        {
            this.DisplayText1 = "Waiting for Mix It Up to close...";
            this.IsOperationIndeterminate = true;
            this.OperationProgress = 0;

            for (int i = 0; i < 10; i++)
            {
                bool isRunning = false;
                foreach (Process clsProcess in Process.GetProcesses())
                {
                    if ((clsProcess.ProcessName.Equals(MixItUpProcessName) || clsProcess.ProcessName.Equals(AutoHosterProcessName)) && BelongsToInstall(clsProcess))
                    {
                        isRunning = true;
                        if (i == 5)
                        {
                            clsProcess.CloseMainWindow();
                        }
                    }
                }

                if (!isRunning)
                {
                    return true;
                }
                await Task.Delay(1000);
            }
            return false;
        }

        private bool BelongsToInstall(Process process)
        {
            try { return string.Equals(Path.GetDirectoryName(process.MainModule.FileName), this.installDirectory.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase); }
            catch (InvalidOperationException) { return false; }
            catch (System.ComponentModel.Win32Exception) { return false; }
        }

        private async Task<UpdateVersionManifestModel> FetchManifestAsync()
        {
            string channel = !string.IsNullOrEmpty(this.targetChannelOverride)
                ? this.targetChannelOverride
                : (this.IsPreview || this.IsTest) ? "preview" : "public";
            string versionCheckUrl = string.Format("{0}/{1}/latest", FileServiceBaseUrl, channel);

            this.manifestChannel = channel;

            this.WriteToLogFile("Requesting version check: " + versionCheckUrl);

            int maxAttempts = ManifestRetryDelays.Length + 1;

            UpdateVersionCheckModel versionCheck = null;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, versionCheckUrl))
                    {
                        request.Headers.Accept.Clear();
                        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                        using (HttpResponseMessage response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                        {
                            if (response.IsSuccessStatusCode)
                            {
                                string json = await response.Content.ReadAsStringAsync();
                                if (string.IsNullOrEmpty(json))
                                {
                                    this.WriteToLogFile("Version check response empty");
                                }
                                else
                                {
                                    try
                                    {
                                        versionCheck = JsonConvert.DeserializeObject<UpdateVersionCheckModel>(json);
                                        if (versionCheck != null)
                                        {
                                            break;
                                        }
                                        this.WriteToLogFile("Version check deserialized to null");
                                    }
                                    catch (JsonException jex)
                                    {
                                        this.WriteToLogFile("Version check parse error: " + jex);
                                        return null;
                                    }
                                }
                            }
                            else
                            {
                                string body = await response.Content.ReadAsStringAsync();
                                this.WriteToLogFile(string.Format("Version check request failed (attempt {0}): {1} {2}{3}{4}", attempt + 1, (int)response.StatusCode, response.ReasonPhrase, Environment.NewLine, body));
                            }
                        }
                    }
                }
                catch (HttpRequestException hre)
                {
                    this.WriteToLogFile(string.Format("Version check request error (attempt {0}): {1}", attempt + 1, hre));
                }
                catch (TaskCanceledException tce)
                {
                    this.WriteToLogFile(string.Format("Version check request timeout (attempt {0}): {1}", attempt + 1, tce));
                }

                if (attempt < ManifestRetryDelays.Length)
                {
                    await Task.Delay(ManifestRetryDelays[attempt]);
                }
            }

            if (versionCheck == null)
            {
                return null;
            }

            if (versionCheck.updatePaused)
            {
                this.WriteToLogFile("Updates paused for channel: " + channel);
                this.SpecificErrorMessage = "There are currently no active updates for this channel. Please try again later.";
                this.ShowError("No active updates available.", this.SpecificErrorMessage);
                return null;
            }

            this.versionCheck = versionCheck;

            string targetVersion = !string.IsNullOrEmpty(this.targetVersionOverride)
                ? this.targetVersionOverride
                : versionCheck.latestVersion;

            this.WriteToLogFile(string.Format("Version check: channel={0}, url={1}, latestVersion={2}, minimumVersion={3}, updatePaused={4}, targetVersion={5}",
                channel, versionCheckUrl, versionCheck.latestVersion, versionCheck.minimumVersion, versionCheck.updatePaused,
                targetVersion));

            string manifestUrl = string.Format("{0}/{1}/{2}", FileServiceBaseUrl, channel, targetVersion);
            this.manifestUrl = manifestUrl;

            this.WriteToLogFile("Requesting version manifest: " + manifestUrl);

            UpdateVersionManifestModel versionManifest = null;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, manifestUrl))
                    {
                        request.Headers.Accept.Clear();
                        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                        using (HttpResponseMessage response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                        {
                            if (response.IsSuccessStatusCode)
                            {
                                string json = await response.Content.ReadAsStringAsync();
                                if (string.IsNullOrEmpty(json))
                                {
                                    this.WriteToLogFile("Manifest response empty");
                                }
                                else
                                {
                                    try
                                    {
                                        versionManifest = JsonConvert.DeserializeObject<UpdateVersionManifestModel>(json);
                                        if (versionManifest != null)
                                        {
                                            break;
                                        }
                                        this.WriteToLogFile("Manifest deserialized to null");
                                    }
                                    catch (JsonException jex)
                                    {
                                        this.WriteToLogFile("Manifest parse error: " + jex);
                                        return null;
                                    }
                                }
                            }
                            else
                            {
                                string body = await response.Content.ReadAsStringAsync();
                                this.WriteToLogFile(string.Format("Manifest request failed (attempt {0}): {1} {2}{3}{4}", attempt + 1, (int)response.StatusCode, response.ReasonPhrase, Environment.NewLine, body));
                            }
                        }
                    }
                }
                catch (HttpRequestException hre)
                {
                    this.WriteToLogFile(string.Format("Manifest request error (attempt {0}): {1}", attempt + 1, hre));
                }
                catch (TaskCanceledException tce)
                {
                    this.WriteToLogFile(string.Format("Manifest request timeout (attempt {0}): {1}", attempt + 1, tce));
                }

                if (attempt < ManifestRetryDelays.Length)
                {
                    await Task.Delay(ManifestRetryDelays[attempt]);
                }
            }

            if (versionManifest == null)
            {
                return null;
            }

            this.versionManifest = versionManifest;
            return versionManifest;
        }

        private async Task<bool> DownloadPackageAsync()
        {
            UpdateStepModel downloadStep = versionManifest.GetDownloadStep();
            UpdateStepModel verifyStep = versionManifest.GetVerifyStep();
            UpdateStepModel extractStep = versionManifest.GetExtractStep();

            if (downloadStep == null || string.IsNullOrEmpty(downloadStep.target))
            {
                WriteToLogFile("Package download step missing from manifest; cannot download.");
                ShowError("Invalid update manifest.", "The update manifest did not include a package download step.");
                return false;
            }

            // target interpretation rule: treat as absolute URL if it begins with a URI scheme; otherwise construct from base
            string packageUrl;
            string packageFileName;
            if (downloadStep.target.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                downloadStep.target.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                packageUrl = downloadStep.target;
                packageFileName = Uri.TryCreate(downloadStep.target, UriKind.Absolute, out Uri parsed)
                                      ? Path.GetFileName(parsed.LocalPath)
                                      : "package.zip";
            }
            else
            {
                packageUrl = $"{FileServiceBaseUrl}/{this.manifestChannel}/{versionManifest.version}/{downloadStep.target}";
                packageFileName = downloadStep.target;
            }

            string expectedSha256 = verifyStep?.value;

            bool encounteredChecksumMismatch = false;
            string lastExpectedHash = null;
            string lastActualHash = null;

            this.tempDirectoryPath = Path.Combine(this.installDirectory, TempDirectoryName);
            Directory.CreateDirectory(this.tempDirectoryPath);

            string filePath = Path.Combine(this.tempDirectoryPath, packageFileName);
            int maxAttempts = DownloadRetryDelays.Length + 1;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    this.DisplayText1 = "Downloading update package...";
                    this.DisplayText2 = string.Empty;
                    this.IsOperationIndeterminate = true;
                    this.OperationProgress = 0;

                    this.WriteToLogFile(string.Format("Downloading package (attempt {0}): {1}", attempt + 1, packageUrl));

                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }

                    using (HttpResponseMessage response = await HttpClient.GetAsync(packageUrl, HttpCompletionOption.ResponseHeadersRead))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            string body = await response.Content.ReadAsStringAsync();
                            this.WriteToLogFile(string.Format("Package download failed (attempt {0}): {1} {2}{3}{4}", attempt + 1, (int)response.StatusCode, response.ReasonPhrase, Environment.NewLine, body));
                        }
                        else
                        {
                            long? contentLength = response.Content.Headers.ContentLength;

                            using (Stream httpStream = await response.Content.ReadAsStreamAsync())
                            using (FileStream fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                            {
                                byte[] buffer = new byte[81920];
                                long totalRead = 0;
                                int bytesRead;
                                while ((bytesRead = await httpStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                                {
                                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                                    totalRead += bytesRead;

                                    if (contentLength.HasValue && contentLength.Value > 0)
                                    {
                                        int progress = (int)Math.Min(100, Math.Floor((totalRead / (double)contentLength.Value) * 100));
                                        this.OperationProgress = progress;
                                        this.IsOperationIndeterminate = false;
                                    }
                                }
                            }

                            bool checksumMismatchThisAttempt = false;

                            this.DisplayText1 = "Verifying download package...";

                            string expectedHash = string.IsNullOrWhiteSpace(expectedSha256) ? null : expectedSha256.Trim();
                            if (!string.IsNullOrEmpty(expectedHash))
                            {
                                string verifyFilePath = (verifyStep != null && !string.IsNullOrEmpty(verifyStep.target))
                                    ? Path.Combine(this.tempDirectoryPath, verifyStep.target)
                                    : filePath;
                                string actualHash = ComputeSha256(verifyFilePath);

                                if (!string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
                                {
                                    encounteredChecksumMismatch = true;
                                    checksumMismatchThisAttempt = true;
                                    lastExpectedHash = expectedHash;
                                    lastActualHash = actualHash;

                                    this.WriteToLogFile(string.Format("Package checksum mismatch (attempt {0}): expected {1}, actual {2}", attempt + 1, expectedHash, actualHash));

                                    if (File.Exists(filePath))
                                    {
                                        File.Delete(filePath);
                                    }

                                    this.downloadedPackagePath = null;
                                    this.OperationProgress = 0;
                                    this.DisplayText1 = "Checksum mismatch detected.";
                                    this.DisplayText2 = "Retrying download...";
                                }
                            }
                            else
                            {
                                this.WriteToLogFile("No expected SHA-256 provided; skipping verification.");
                            }

                            if (!checksumMismatchThisAttempt)
                            {
                                string extractFilePath = (extractStep != null && !string.IsNullOrEmpty(extractStep.target))
                                    ? Path.Combine(this.tempDirectoryPath, extractStep.target)
                                    : filePath;
                                this.downloadedPackagePath = extractFilePath;
                                this.OperationProgress = 100;
                                this.IsOperationIndeterminate = false;
                                this.DisplayText1 = "Download complete.";
                                this.DisplayText2 = string.Empty;
                                this.WriteToLogFile("Package downloaded successfully: " + filePath);
                                return true;
                            }
                        }
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException || ex is IOException || ex is TaskCanceledException)
                {
                    this.WriteToLogFile(string.Format("Package download error (attempt {0}): {1}", attempt + 1, ex));
                }
                catch (Exception ex)
                {
                    this.WriteToLogFile(string.Format("Unexpected package download error (attempt {0}): {1}", attempt + 1, ex));
                    break;
                }

                if (File.Exists(filePath))
                {
                    try
                    {
                        File.Delete(filePath);
                    }
                    catch (Exception cleanupEx)
                    {
                        this.WriteToLogFile("Failed to delete incomplete download: " + cleanupEx);
                    }
                }

                if (attempt < DownloadRetryDelays.Length)
                {
                    await Task.Delay(DownloadRetryDelays[attempt]);
                }
            }

            this.downloadedPackagePath = null;

            if (encounteredChecksumMismatch)
            {
                this.WriteToLogFile(string.Format("Package download failed after {0} attempts due to checksum mismatches.", maxAttempts));
                this.ShowChecksumMismatchError(lastExpectedHash ?? expectedSha256 ?? "<unknown>", lastActualHash ?? "<missing>");
            }
            else
            {
                this.WriteToLogFile(string.Format("Package download failed after {0} attempts due to network errors.", maxAttempts));
                this.ShowNetworkRetryError("We were unable to download the Mix It Up update after multiple attempts. Please check your connection and try again.");
            }

            return false;
        }

        private bool InstallMixItUp()
        {
            this.DisplayText1 = "Installing files...";
            this.IsOperationIndeterminate = false;
            this.OperationProgress = 0;
            this.DisplayText2 = string.Empty;

            try
            {
                if (string.IsNullOrEmpty(this.downloadedPackagePath) || !File.Exists(this.downloadedPackagePath))
                {
                    this.SpecificErrorMessage = "We were unable to locate the downloaded update package. Please try again.";
                    this.WriteToLogFile("Package not found: " + (this.downloadedPackagePath ?? "<null>"));
                    return false;
                }

                Directory.CreateDirectory(this.installDirectory);
                if (!Directory.Exists(this.installDirectory))
                {
                    this.SpecificErrorMessage = "We were unable to prepare the Mix It Up installation directory.";
                    return false;
                }

                using (FileStream packageStream = File.OpenRead(this.downloadedPackagePath))
                using (ZipArchive archive = new ZipArchive(packageStream, ZipArchiveMode.Read, leaveOpen: false))
                {
                    double current = 0;
                    double total = archive.Entries.Count;
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string fullName = entry.FullName;
                        if (entry.FullName.StartsWith("Mix It Up/", StringComparison.Ordinal))
                        {
                            fullName = entry.FullName.Substring("Mix It Up/".Length);
                        }

                        string filePath = Path.Combine(this.installDirectory, fullName);
                        string directoryPath = Path.GetDirectoryName(filePath);
                        if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
                        {
                            Directory.CreateDirectory(directoryPath);
                        }

                        if (Path.HasExtension(filePath))
                        {
                            entry.ExtractToFile(filePath, overwrite: true);
                        }

                        current++;
                        if (total > 0)
                        {
                            this.OperationProgress = (int)((current / total) * 100);
                        }
                    }
                }

                this.OperationProgress = 100;
                this.CleanupTemporaryDownload();
                this.WriteToLogFile("Installation files updated successfully.");
                return true;
            }
            catch (UnauthorizedAccessException uaex)
            {
                this.SpecificErrorMessage = "We were unable to update due to a file lock issue. Please try rebooting your PC and then running the update. You can also download and re-run our installer to update your installation.";
                this.WriteToLogFile(uaex.ToString());
            }
            catch (IOException ioex)
            {
                this.SpecificErrorMessage = "We were unable to update due to a file lock issue. Please try rebooting your PC and then running the update. You can also download and re-run our installer to update your installation.";
                this.WriteToLogFile(ioex.ToString());
            }
            catch (Exception ex)
            {
                this.WriteToLogFile(ex.ToString());
            }
            return false;
        }

        private async Task<bool> EnsureEulaAcceptedAsync()
        {
            if (string.IsNullOrEmpty(versionManifest.eulaVersion))
            {
                this.WriteToLogFile("EULA not required for this update.");
                return true;
            }

            string eulaUrl = versionManifest.GetEulaUrl(FileServiceBaseUrl, this.manifestChannel);

            string eulaAcceptedPath = Path.Combine(this.installDirectory, EulaAcceptedFileName);
            try
            {
                if (File.Exists(eulaAcceptedPath))
                {
                    string[] lines = File.ReadAllLines(eulaAcceptedPath);
                    if (lines.Length > 0)
                    {
                        string recordedVersion = (lines[0] ?? string.Empty).Trim();
                        if (!string.IsNullOrEmpty(recordedVersion) && string.Equals(recordedVersion, versionManifest.eulaVersion, StringComparison.OrdinalIgnoreCase))
                        {
                            this.WriteToLogFile("EULA already accepted for version " + recordedVersion);
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                this.WriteToLogFile("Failed to read EULA acceptance file: " + ex);
            }

            if (this.ShowEulaDialogAsync == null)
            {
                this.WriteToLogFile("EULA dialog delegate not provided; cannot display EULA.");
                this.ShowEulaDeclinedError();
                return false;
            }

            string eulaMarkdown;
            try
            {
                this.WriteToLogFile("Downloading EULA markdown: " + eulaUrl);
                using (HttpResponseMessage response = await HttpClient.GetAsync(eulaUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        string body = await response.Content.ReadAsStringAsync();
                        this.WriteToLogFile(string.Format("Failed to download EULA: {0} {1}{2}{3}", (int)response.StatusCode, response.ReasonPhrase, Environment.NewLine, body));
                        this.ShowNetworkRetryError("We were unable to download the Mix It Up EULA. Please check your connection and try again.");
                        return false;
                    }

                    eulaMarkdown = await response.Content.ReadAsStringAsync();
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is IOException || ex is TaskCanceledException)
            {
                this.WriteToLogFile("Network error downloading EULA: " + ex);
                this.ShowNetworkRetryError("We were unable to download the Mix It Up EULA. Please check your connection and try again.");
                return false;
            }

            this.DisplayText1 = "Awaiting EULA acceptance...";
            this.DisplayText2 = string.Empty;
            this.IsOperationIndeterminate = true;
            this.OperationProgress = 0;

            this.WriteToLogFile("Displaying EULA version " + versionManifest.eulaVersion);

            bool accepted;
            try
            {
                accepted = await this.ShowEulaDialogAsync(eulaMarkdown, eulaUrl);
            }
            catch (Exception ex)
            {
                this.WriteToLogFile("EULA dialog threw an exception: " + ex);
                this.ShowEulaDeclinedError();
                return false;
            }

            if (!accepted)
            {
                this.WriteToLogFile("User declined EULA version " + versionManifest.eulaVersion);
                this.ShowEulaDeclinedError();
                return false;
            }

            this.WriteToLogFile("User accepted EULA version " + versionManifest.eulaVersion);
            this.PersistEulaAcceptance(versionManifest.eulaVersion);
            return true;
        }

        private static string ComputeSha256(string filePath)
        {
            using (FileStream stream = File.OpenRead(filePath))
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(stream);
                StringBuilder builder = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash)
                {
                    builder.AppendFormat("{0:x2}", b);
                }
                return builder.ToString();
            }
        }

        private static string SanitizeInstallDirectoryArgument(string argument)
        {
            if (string.IsNullOrWhiteSpace(argument))
            {
                return null;
            }

            string candidate = argument.Trim().Trim('"');

            if (string.IsNullOrEmpty(candidate) || candidate.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                return null;
            }

            try
            {
                candidate = Path.GetFullPath(candidate);
                return candidate;
            }
            catch
            {
                return null;
            }
        }

        private static bool DoesDirectoryContainExistingInstall(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                if (!Directory.Exists(path))
                {
                    return false;
                }

                if (File.Exists(Path.Combine(path, "MixItUp.exe")))
                {
                    return true;
                }

                if (File.Exists(Path.Combine(path, "MixItUp.Base.dll")))
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private void CleanupTemporaryDownload()
        {
            if (!string.IsNullOrEmpty(this.downloadedPackagePath) && File.Exists(this.downloadedPackagePath))
            {
                try
                {
                    File.Delete(this.downloadedPackagePath);
                }
                catch (Exception ex)
                {
                    this.WriteToLogFile("Failed to delete temporary package: " + ex);
                }
                finally
                {
                    this.downloadedPackagePath = null;
                }
            }

            if (!string.IsNullOrEmpty(this.tempDirectoryPath) && Directory.Exists(this.tempDirectoryPath))
            {
                try
                {
                    if (Directory.GetFileSystemEntries(this.tempDirectoryPath).Length == 0)
                    {
                        Directory.Delete(this.tempDirectoryPath);
                        this.tempDirectoryPath = null;
                    }
                }
                catch (Exception ex)
                {
                    this.WriteToLogFile("Failed to remove temporary directory: " + ex);
                }
            }
        }

        private void PersistEulaAcceptance(string eulaVersion)
        {
            string eulaAcceptedPath = Path.Combine(this.installDirectory, EulaAcceptedFileName);
            string tempPath = Path.Combine(this.installDirectory, EulaAcceptedFileName + ".tmp");
            string timestamp = DateTimeOffset.UtcNow.ToString("o");

            try
            {
                Directory.CreateDirectory(this.installDirectory);
                File.WriteAllLines(tempPath, new[] { eulaVersion ?? string.Empty, timestamp });

                if (File.Exists(eulaAcceptedPath))
                {
                    File.Replace(tempPath, eulaAcceptedPath, null);
                }
                else
                {
                    File.Move(tempPath, eulaAcceptedPath);
                }

                this.WriteToLogFile("Recorded EULA acceptance version " + (eulaVersion ?? "<unknown>") + " at " + timestamp);
            }
            catch (Exception ex)
            {
                this.WriteToLogFile("Failed to persist EULA acceptance: " + ex);
            }
            finally
            {
                try
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                catch (Exception cleanupEx)
                {
                    this.WriteToLogFile("Failed to remove temporary EULA acceptance file: " + cleanupEx);
                }
            }
        }

        private bool CreateMixItUpShortcut()
        {
            try
            {
                this.DisplayText1 = "Creating Start Menu & Desktop shortcuts...";
                this.IsOperationIndeterminate = true;
                this.OperationProgress = 0;

                bool startMenuCreated = this.CreateShortcutFile(StartMenuShortCutFilePath);
                bool desktopCreated = this.CreateShortcutFile(DesktopShortCutFilePath);

                if (startMenuCreated && desktopCreated)
                {
                    this.WriteToLogFile("Start Menu and Desktop shortcuts created successfully.");
                    return true;
                }

                if (startMenuCreated)
                {
                    this.WriteToLogFile("Desktop shortcut could not be created.");
                    return true;
                }

                if (desktopCreated)
                {
                    this.WriteToLogFile("Start Menu shortcut could not be created.");
                    return true;
                }

                this.WriteToLogFile("Failed to create Start Menu and Desktop shortcuts.");
            }
            catch (Exception ex)
            {
                this.WriteToLogFile("Shortcut creation threw an exception: " + ex);
            }
            return false;
        }

        private bool CreateShortcutFile(string shortcutPath)
        {
            string executablePath = Path.Combine(this.installDirectory, "MixItUp.exe");
            if (!File.Exists(executablePath))
            {
                return false;
            }

            string shortcutDirectory = Path.GetDirectoryName(shortcutPath);
            if (!string.IsNullOrEmpty(shortcutDirectory) && !Directory.Exists(shortcutDirectory))
            {
                Directory.CreateDirectory(shortcutDirectory);
            }

            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
            {
                return false;
            }

            dynamic shell = Activator.CreateInstance(shellType);
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = executablePath;
            shortcut.WorkingDirectory = this.installDirectory;
            shortcut.IconLocation = executablePath + ",0";
            shortcut.Save();

            return File.Exists(shortcutPath);
        }

        private void ShowNetworkRetryError(string detailMessage = null)
        {
            string message = detailMessage ?? "Please check your network connection and try again. Details have been written to MixItUp-Installer-Log.txt.";
            this.SpecificErrorMessage = message;
            this.HyperlinkAddress = InstallerLogFileName;
            this.ShowError("Unable to reach the Mix It Up update service.", message);
        }

        private void ShowChecksumMismatchError(string expectedHash, string actualHash)
        {
            this.SpecificErrorMessage = "The downloaded update file failed verification and was removed. Please try again.";
            this.HyperlinkAddress = InstallerLogFileName;
            this.ShowError("Update verification failed.", this.SpecificErrorMessage);
        }

        private void ShowEulaDeclinedError()
        {
            this.SpecificErrorMessage = "You must accept the Mix It Up End User License Agreement to continue with the installation.";
            this.ShowError("EULA acceptance required.", this.SpecificErrorMessage);
        }

        private void ShowError(string message1, string message2)
        {
            this.IsOperationBeingPerformed = false;
            this.ErrorOccurred = true;
            this.DisplayText1 = message1;
            this.DisplayText2 = message2;
        }

        private void WriteToLogFile(string text)
        {
            File.AppendAllText(InstallerLogFileName, text + Environment.NewLine + Environment.NewLine);
        }
    }
}

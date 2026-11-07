using Microsoft.Win32;
using MixItUp.Base.Services;
using MixItUp.Base.Util;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace MixItUp.WPF.Services
{
    public class WindowsFileService : IFileService
    {
        private static readonly List<string> webPathPrefixes = new List<string>() { "http://", "https://", "www." };

        private static SemaphoreSlim fileLock = new SemaphoreSlim(1);
        private static readonly HttpClient httpClient = new HttpClient();

        public static async Task WithFileSnapshot(Func<Task> snapshot)
        {
            await fileLock.WaitAsync();
            try { await snapshot(); }
            finally { fileLock.Release(); }
        }

        public string TextFileFilter() { return MixItUp.Base.Resources.TextFileFormatFilter; }
        public string ImageFileFilter() { return MixItUp.Base.Resources.ImageFileFormatFilter; }
        public string SoundFileFilter() { return MixItUp.Base.Resources.SoundFileFormatFilter; }
        public string VideoFileFilter() { return MixItUp.Base.Resources.VideoFileFormatFilter; }
        public string HTMLFileFilter() { return MixItUp.Base.Resources.HTMLFileFormatFilter; }

        public async Task CopyFile(string sourcePath, string destinationPath)
        {
            sourcePath = this.ExpandEnvironmentVariablesInFilePath(sourcePath);
            destinationPath = this.ExpandEnvironmentVariablesInFilePath(destinationPath);

            if (!string.IsNullOrEmpty(sourcePath) && !string.IsNullOrEmpty(destinationPath) && File.Exists(sourcePath))
            {
                string destinationDirectory = Path.GetDirectoryName(destinationPath);
                await this.CreateDirectory(destinationDirectory);
                File.Copy(sourcePath, destinationPath, overwrite: true);
            }
        }

        public Task DeleteFile(string filePath)
        {
            filePath = this.ExpandEnvironmentVariablesInFilePath(filePath);
            if (!string.IsNullOrEmpty(filePath))
            {
                File.Delete(filePath);
            }
            return Task.CompletedTask;
        }

        public Task CreateDirectory(string path)
        {
            path = this.ExpandEnvironmentVariablesInFilePath(path);
            if (!string.IsNullOrEmpty(path) && !Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            return Task.CompletedTask;
        }

        public async Task CopyDirectory(string sourceDirectoryPath, string destinationDirectoryPath)
        {
            sourceDirectoryPath = this.ExpandEnvironmentVariablesInFilePath(sourceDirectoryPath);
            destinationDirectoryPath = this.ExpandEnvironmentVariablesInFilePath(destinationDirectoryPath);

            if (!string.IsNullOrEmpty(sourceDirectoryPath) && !string.IsNullOrEmpty(destinationDirectoryPath) && Directory.Exists(sourceDirectoryPath))
            {
                await this.CreateDirectory(destinationDirectoryPath);
                foreach (string filepath in Directory.GetFiles(sourceDirectoryPath))
                {
                    File.Copy(filepath, filepath.Replace(sourceDirectoryPath, destinationDirectoryPath));
                }
            }
        }

        public Task<IEnumerable<string>> GetFilesInDirectory(string directoryPath)
        {
            directoryPath = this.ExpandEnvironmentVariablesInFilePath(directoryPath);
            if (!string.IsNullOrEmpty(directoryPath) && Directory.Exists(directoryPath))
            {
                return Task.FromResult<IEnumerable<string>>(Directory.GetFiles(directoryPath));
            }
            return Task.FromResult<IEnumerable<string>>(new List<string>());
        }

        public Task<IEnumerable<string>> GetFoldersInDirectory(string directoryPath)
        {
            directoryPath = this.ExpandEnvironmentVariablesInFilePath(directoryPath);
            if (!string.IsNullOrEmpty(directoryPath) && Directory.Exists(directoryPath))
            {
                return Task.FromResult<IEnumerable<string>>(Directory.GetDirectories(directoryPath));
            }
            return Task.FromResult<IEnumerable<string>>(new List<string>());
        }

        public bool FileExists(string filePath)
        {
            filePath = this.ExpandEnvironmentVariablesInFilePath(filePath);
            return !string.IsNullOrEmpty(filePath) && File.Exists(filePath);
        }

        public bool IsURLPath(string filePath)
        {
            return !string.IsNullOrEmpty(filePath) && Uri.IsWellFormedUriString(filePath, UriKind.RelativeOrAbsolute);
        }

        public long GetFileSize(string filePath)
        {
            if (this.FileExists(filePath))
            {
                try
                {
                    return new FileInfo(filePath).Length;
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
            }
            return 0;
        }

        public async Task<string> ReadFile(string filePath)
        {
            filePath = this.ExpandEnvironmentVariablesInFilePath(filePath);
            if (!string.IsNullOrEmpty(filePath))
            {
                try
                {
                    string safeFilePath = filePath.ToFilePathString();
                    if (File.Exists(filePath))
                    {
                        return await File.ReadAllTextAsync(filePath);
                    }
                    else if (File.Exists(safeFilePath))
                    {
                        return await File.ReadAllTextAsync(safeFilePath);
                    }
                    else if (webPathPrefixes.Any(p => filePath.StartsWith(p, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        return await httpClient.GetStringAsync(filePath);
                    }
                }
                catch (Exception ex) { Logger.Log(ex); }
            }
            return null;
        }

        public async Task<byte[]> ReadFileAsBytes(string filePath)
        {
            filePath = this.ExpandEnvironmentVariablesInFilePath(filePath);
            if (!string.IsNullOrEmpty(filePath))
            {
                try
                {
                    string safeFilePath = filePath.ToFilePathString();
                    if (File.Exists(filePath))
                    {
                        return await File.ReadAllBytesAsync(filePath);
                    }
                    else if (File.Exists(safeFilePath))
                    {
                        return await File.ReadAllBytesAsync(safeFilePath);
                    }
                    else if (webPathPrefixes.Any(p => filePath.StartsWith(p, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        return await httpClient.GetByteArrayAsync(filePath);
                    }
                }
                catch (Exception ex) { Logger.Log(ex); }
            }
            return null;
        }

        public async Task SaveFile(string filePath, string data)
        {
            filePath = this.ExpandEnvironmentVariablesInFilePath(filePath);
            if (!string.IsNullOrEmpty(filePath))
            {
                try
                {
                    await WindowsFileService.fileLock.WaitAsync();

                    await File.WriteAllTextAsync(filePath, data ?? string.Empty);
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
                finally
                {
                    WindowsFileService.fileLock.Release();
                }
            }
        }

        public async Task SaveFile(string filePath, byte[] data)
        {
            filePath = this.ExpandEnvironmentVariablesInFilePath(filePath);
            if (!string.IsNullOrEmpty(filePath))
            {
                try
                {
                    await File.WriteAllBytesAsync(filePath, data);
                }
                catch (Exception ex) { Logger.Log(ex); }
            }
        }

        public async Task SaveFile(string filePath, Stream data)
        {
            filePath = this.ExpandEnvironmentVariablesInFilePath(filePath);
            if (!string.IsNullOrEmpty(filePath))
            {
                try
                {
                    await WindowsFileService.fileLock.WaitAsync();

                    data.Seek(0, SeekOrigin.Begin);
                    using (FileStream fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await data.CopyToAsync(fileStream);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
                finally
                {
                    WindowsFileService.fileLock.Release();
                }
            }
        }

        public async Task AppendFile(string filePath, string data)
        {
            filePath = this.ExpandEnvironmentVariablesInFilePath(filePath);
            if (!string.IsNullOrEmpty(filePath))
            {
                try
                {
                    await WindowsFileService.fileLock.WaitAsync();

                    using (StreamWriter writer = new StreamWriter(File.Open(filePath, FileMode.Append)))
                    {
                        if (!string.IsNullOrEmpty(data))
                        {
                            await writer.WriteAsync(data);
                        }
                        await writer.FlushAsync();
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
                finally
                {
                    WindowsFileService.fileLock.Release();
                }
            }
        }

        public async Task SaveSettingsFile(string filePath, string data)
        {
            filePath = this.ExpandEnvironmentVariablesInFilePath(filePath);
            if (!string.IsNullOrEmpty(filePath))
            {
                string tempPath = filePath + ".tmp";

                try
                {
                    await WindowsFileService.fileLock.WaitAsync();

                    byte[] bytes = System.Text.Encoding.UTF8.GetBytes(data ?? string.Empty);
                    using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                    {
                        await stream.WriteAsync(bytes, 0, bytes.Length);
                        await stream.FlushAsync();
                        stream.Flush(true);
                    }

                    if (new FileInfo(tempPath).Length == 0)
                    {
                        File.Delete(tempPath);
                        throw new IOException("Empty file");
                    }

                    string tempContents = await File.ReadAllTextAsync(tempPath, System.Text.Encoding.UTF8);
                    if (string.IsNullOrWhiteSpace(tempContents) || tempContents.Contains('\0') || !tempContents.TrimStart().StartsWith("{"))
                    {
                        File.Delete(tempPath);
                        throw new IOException("Invalid settings file");
                    }
                    JObject.Parse(tempContents);

                    const int maxAttempts = 5;
                    for (int attempt = 1; ; attempt++)
                    {
                        try
                        {
                            if (File.Exists(filePath))
                            {
                                File.Replace(tempPath, filePath, null, ignoreMetadataErrors: true);
                            }
                            else
                            {
                                File.Move(tempPath, filePath, overwrite: true);
                            }
                            tempPath = null;
                            break;
                        }
                        catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) && attempt < maxAttempts)
                        {
                            Logger.Log(LogLevel.Warning, $"Settings file replace failed for {filePath}. Retrying attempt {attempt + 1} of {maxAttempts}.");
                            Logger.Log(LogLevel.Warning, ex);
                            await Task.Delay(100 * attempt);
                        }
                    }

                    string backupPath = filePath + ".backup";
                    try
                    {
                        File.Copy(filePath, backupPath, overwrite: true);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(LogLevel.Error, "Failed to save settings backup file.");
                        Logger.Log(ex);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                    throw;
                }
                finally
                {
                    if (!string.IsNullOrEmpty(tempPath) && File.Exists(tempPath))
                    {
                        try { File.Delete(tempPath); }
                        catch (Exception ex) { Logger.Log(ex); }
                    }
                    WindowsFileService.fileLock.Release();
                }
            }
        }

        public string ShowOpenFolderDialog()
        { 
            using (var folderDialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    return folderDialog.SelectedPath;
                }
            }
            return null;
        }

        public string ShowOpenFileDialog() { return this.ShowOpenFileDialog("All files (*.*)|*.*"); }

        public string ShowOpenFileDialog(string filter)
        {
            OpenFileDialog fileDialog = new OpenFileDialog();
            fileDialog.Filter = filter;
            fileDialog.CheckFileExists = true;
            fileDialog.CheckPathExists = true;

            if (fileDialog.ShowDialog() == true)
            {
                return fileDialog.FileName;
            }
            return null;
        }

        public IEnumerable<string> ShowMultiselectOpenFileDialog(string filter)
        {
            OpenFileDialog fileDialog = new OpenFileDialog();
            fileDialog.Filter = filter;
            fileDialog.CheckFileExists = true;
            fileDialog.CheckPathExists = true;
            fileDialog.Multiselect = true;

            if (fileDialog.ShowDialog() == true && fileDialog.FileNames != null && fileDialog.FileNames.Length > 0 && !string.IsNullOrWhiteSpace(fileDialog.FileNames[0]))
            {
                return fileDialog.FileNames;
            }
            return null;
        }

        public string ShowSaveFileDialog(string fileName) { return this.ShowSaveFileDialog(fileName, "All files (*.*)|*.*"); }

        public string ShowSaveFileDialog(string fileName, string filter)
        {
            SaveFileDialog fileDialog = new SaveFileDialog();
            fileDialog.Filter = filter;
            fileDialog.CheckPathExists = true;
            fileDialog.FileName = fileName;
            if (fileDialog.ShowDialog() == true)
            {
                return fileDialog.FileName;
            }
            return null;
        }

        public async Task ZipFiles(string destinationFilePath, IEnumerable<string> filePathsToBeAdded)
        {
            if (!string.IsNullOrEmpty(destinationFilePath) && filePathsToBeAdded != null)
            {
                string tempDirectory = Path.Combine(this.GetTempFolder(), Path.GetRandomFileName());
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, recursive: true);
                }
                Directory.CreateDirectory(tempDirectory);

                foreach (string filePathToBeAdded in filePathsToBeAdded)
                {
                    await this.CopyFile(filePathToBeAdded, Path.Combine(tempDirectory, Path.GetFileName(filePathToBeAdded)));
                }

                ZipFile.CreateFromDirectory(tempDirectory, destinationFilePath);
            }
        }

        public async Task UnzipFiles(string zipFilePath, string destinationFolderPath)
        {
            await Task.Run(() =>
            {
                try
                {
                    if (!string.IsNullOrEmpty(zipFilePath) && !string.IsNullOrEmpty(destinationFolderPath) && File.Exists(zipFilePath))
                    {
                        using (FileStream stream = File.OpenRead(zipFilePath))
                        {
                            ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read);
                            foreach (ZipArchiveEntry file in archive.Entries)
                            {
                                string fullPath = Path.Combine(destinationFolderPath, file.FullName);
                                if (string.IsNullOrEmpty(file.Name))
                                {
                                    string directoryPath = Path.GetDirectoryName(fullPath);
                                    if (!Directory.Exists(directoryPath))
                                    {
                                        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                                    }
                                }
                                else
                                {
                                    file.ExtractToFile(fullPath, true);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
            });
        }

        public string GetTempFolder() { return Path.GetTempPath(); }

        public string GetApplicationDirectory() { return Path.GetDirectoryName(typeof(IFileService).Assembly.Location); }

        public string GetApplicationVersion() { return Assembly.GetEntryAssembly().GetName().Version.ToString().Trim(); }

        public IEnumerable<string> GetInstalledFonts()
        {
            using (InstalledFontCollection fontsCollection = new InstalledFontCollection())
            {
                FontFamily[] fontFamilies = fontsCollection.Families;
                List<string> fonts = new List<string>();
                foreach (FontFamily font in fontFamilies)
                {
                    if (!string.IsNullOrEmpty(font.Name))
                    {
                        fonts.Add(font.Name);
                    }
                }
                return fonts;
            }
        }

        public string ExpandEnvironmentVariablesInFilePath(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                return Environment.ExpandEnvironmentVariables(path);
            }
            return path;
        }
    }
}

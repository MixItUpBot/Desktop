using MixItUp.Base.Services;
using MixItUp.Base.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace MixItUp.WPF.Services
{
    public class WindowsProcessService : IProcessService
    {
        private const string StartupRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string StartupRegistryName = "MixItUp";
        internal static string StartupCommand => "\"" + Path.Combine(AppContext.BaseDirectory, "MixItUp.exe") + "\"";

        public bool LaunchAtLoginEnabled
        {
            get
            {
                try
                {
                    using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(StartupRegistryPath))
                    {
                        return string.Equals(key?.GetValue(StartupRegistryName) as string, StartupCommand, StringComparison.OrdinalIgnoreCase);
                    }
                }
                catch (Exception ex) { Logger.Log(ex); return false; }
            }
        }

        public Result SetLaunchAtLogin(bool enabled)
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(StartupRegistryPath))
                {
                    if (enabled)
                    {
                        key.SetValue(StartupRegistryName, StartupCommand, Microsoft.Win32.RegistryValueKind.String);
                    }
                    else if (string.Equals(key.GetValue(StartupRegistryName) as string, StartupCommand, StringComparison.OrdinalIgnoreCase))
                    {
                        key.DeleteValue(StartupRegistryName, false);
                    }
                }
                return new Result();
            }
            catch (Exception ex) { Logger.Log(ex); return new Result(ex); }
        }

        public void LaunchLink(string url)
        {
            this.TryLaunchLink(url);
        }

        public bool TryLaunchLink(string url)
        {
            try
            {
                if (!string.IsNullOrEmpty(url))
                {
                    ProcessStartInfo processInfo = new ProcessStartInfo(url)
                    {
                        UseShellExecute = true
                    };
                    Process.Start(processInfo);
                    return true;
                }
            }
            catch (Exception ex) { Logger.Log(ex); }
            return false;
        }

        public void LaunchFolder(string folderPath)
        {
            try
            {
                ProcessStartInfo processInfo = new ProcessStartInfo(this.GetRootedPath(folderPath))
                {
                    UseShellExecute = true
                };
                Process.Start(processInfo);
            }
            catch (Exception ex) { Logger.Log(ex); }
        }

        public void LaunchProgram(string filePath, string arguments = "")
        {
            try
            {
                ProcessStartInfo processInfo = new ProcessStartInfo(this.GetRootedPath(filePath), arguments)
                {
                    UseShellExecute = true,
                };
                Process.Start(processInfo);
            }
            catch (Exception ex) { Logger.Log(ex); }
        }

        public IEnumerable<Process> GetProcessesByName(string name)
        {
            try
            {
                return Process.GetProcessesByName(name);
            }
            catch (Exception ex) { Logger.Log(ex); }
            return new List<Process>();
        }

        private string GetRootedPath(string path)
        {
            if (!Path.IsPathRooted(path))
            {
                path = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), path);
            }
            return path;
        }
    }
}

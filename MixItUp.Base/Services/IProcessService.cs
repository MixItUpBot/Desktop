using System.Collections.Generic;
using System.Diagnostics;

namespace MixItUp.Base.Services
{
    public interface IProcessService
    {
        bool LaunchAtLoginEnabled { get; }

        MixItUp.Base.Util.Result SetLaunchAtLogin(bool enabled);

        void LaunchLink(string url);

        /// <summary>Opens the URL in the user's default browser, reporting whether the launch actually
        /// succeeded. Needed by flows such as OAuth that are stuck waiting on the browser and must be able
        /// to tell the user when it never opened.</summary>
        bool TryLaunchLink(string url);

        void LaunchFolder(string folderPath);

        void LaunchProgram(string filePath, string arguments = "");

        IEnumerable<Process> GetProcessesByName(string name);
    }
}

using MixItUp.Base.Util;
using System;
using System.Diagnostics;
using System.Linq;

namespace MixItUp.WPF
{
    public class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            if (args.Any(arg => arg.StartsWith("mixitup:", StringComparison.OrdinalIgnoreCase) || (!arg.StartsWith("--") && System.IO.File.Exists(arg))))
            {
                Process[] processes = Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName);
                if (processes.Length > 1)
                {
                    ActivationProtocolHandler.SendRequest(args);
                    return;
                }
            }

            // Settings and profile databases in existing 1.x installations are relative to this folder.
            Environment.CurrentDirectory = AppContext.BaseDirectory;
            App application = new App();
            application.InitializeComponent();
            application.Run();
        }
    }
}

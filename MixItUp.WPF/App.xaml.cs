using MixItUp.Base;
using MixItUp.Base.Model.Settings;
using MixItUp.Base.Services;
using MixItUp.Base.Services.External;
using MixItUp.Base.Util;
using MixItUp.WPF.Services;
using MixItUp.WPF.Services.DeveloperAPI;
using MixItUp.WPF.Services.MCP;
using MixItUp.WPF.Util;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace MixItUp.WPF
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private bool crashObtained = false;

        public App()
        {
            // Disabled for now. Not needed anymore?
            //ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;

            try
            {
                ServiceManager.Add<IFileService>(new WindowsFileService());
                FileLoggerHandler.Initialize();
                Logger.AlwaysLogFullStackTraceWithExceptions = true;
                Logger.ForceLog(LogLevel.Information, "Application startup: registering services");

                ToolTipService.ShowDurationProperty.OverrideMetadata(typeof(DependencyObject), new FrameworkPropertyMetadata(Int32.MaxValue));

                // We need to load the language setting VERY early, so this is the minimal code necessary to get this value
                ServiceManager.Add<IDatabaseService>(new WindowsDatabaseService());
                ServiceManager.Add<IInputService>(new WindowsInputService());
                ServiceManager.Add<IImageService>(new WindowsImageService());
                ServiceManager.Add<IAudioService>(new WindowsAudioService());
                ServiceManager.Add<IDeveloperAPIService>(new WindowsDeveloperAPIService());
                ServiceManager.Add<IMCPService>(new WindowsMCPService());
                ServiceManager.Add<ITelemetryService>(new WindowsTelemetryService());
                ServiceManager.Add<IMusicPlayerService>(new WindowsMusicPlayerService());
                ServiceManager.Add<IProcessService>(new WindowsProcessService());
                ServiceManager.Add<IScriptRunnerService>(new WindowsScriptRunnerService());
                ServiceManager.Add(new WindowsMicrosoftAzureSpeechService());
                ServiceManager.Add(new StreamlabsService(new WindowsSocketIOConnection()));
                ServiceManager.Add(new RainmakerService(new WindowsSocketIOConnection()));
                ServiceManager.Add(new StreamElementsService());
                ServiceManager.Add(new TipeeeStreamService(new WindowsSocketIOConnection()));
                ServiceManager.Add(new TreatStreamService(new WindowsSocketIOConnection()));
                ServiceManager.Add<IOBSStudioService>(new WindowsOBSService());
                ServiceManager.Add(new WindowsSpeechService());
                ServiceManager.Add(new WindowsAmazonPollyService());
                ServiceManager.Add<IThemeService>(new WindowsThemeService());

                Logger.ForceLog(LogLevel.Information, "Application startup: initializing ChannelSession");
                ChannelSession.Initialize().Wait();
                Logger.ForceLog(LogLevel.Information, "Application startup: ChannelSession initialized");

                System.Threading.Thread.CurrentThread.CurrentUICulture = Languages.GetLanguageLocaleCultureInfo();

                // Apply software rendering mode if hardware acceleration setting is disabled
                if (ChannelSession.AppSettings != null && ChannelSession.AppSettings.DisableHardwareAcceleration)
                {
                    System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
                }
            }
            catch (Exception ex)
            {
                Logger.ForceLog(LogLevel.Fatal, "Application startup failed");
                Logger.Log(LogLevel.Fatal, ex, includeStackTrace: true);
                FileLoggerHandler.Flush();
                // Do not pump the dispatcher or allow the queued login startup to run.
                throw;
            }
        }


        protected override void OnStartup(StartupEventArgs e)
        {
            ActivationProtocolHandler.Initialize();

            RegistryHelpers.RegisterFileAssociation();
            RegistryHelpers.RegisterURIActivationProtocol();
            //RegistryHelpers.RegisterUninstaller(); // Disabled for now

            DispatcherHelper.RegisterDispatcher(new WindowsDispatcher(this.Dispatcher));

            DialogHelper.Initialize(new WPFDialogShower());
            AvalonEditCustomHighlighting.Initialize();

            Application.Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            try
            {
                WindowsIdentity id = WindowsIdentity.GetCurrent();
                ChannelSession.IsElevated = id.Owner != id.User;
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            Logger.ForceLog(LogLevel.Information, "Application Version: " + ServiceManager.Get<IFileService>().GetApplicationVersion() + " [" + VersionHelper.GetFullVersionString() + BuildChannelHelper.BUILD_CHANNEL_SUFFIX + "]");
            if (ChannelSession.IsDebug() || (ChannelSession.AppSettings != null && ChannelSession.AppSettings.DiagnosticLogging))
            {
                Logger.SetLogLevel(LogLevel.Debug);
            }
            else
            {
                Logger.SetLogLevel(LogLevel.Error);
            }

            try
            {
                ServiceManager.Get<IThemeService>().ApplyTheme(
                    ChannelSession.AppSettings.ColorScheme ?? "Indigo",
                    ChannelSession.AppSettings.BackgroundColor ?? "Light",
                    ChannelSession.AppSettings.ForegroundColor ?? "Default",
                    ChannelSession.AppSettings.FullThemeName
                );

                // The login window is pinned to the packaged font at 100%. The user's own font and
                // scale are applied once they are past login, in LoadingWindowBase.ShowMainWindow.
                ServiceManager.Get<IThemeService>().ApplyFont(ApplicationSettingsV2Model.DefaultUIFontFamily);
                ServiceManager.Get<IThemeService>().ApplyFontScale(ApplicationSettingsV2Model.DefaultUIScale / 100.0);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                ServiceManager.Get<IThemeService>().ApplyTheme("Indigo", "Light", "Default", null);
                ServiceManager.Get<IThemeService>().ApplyFont(ApplicationSettingsV2Model.DefaultUIFontFamily);
                ServiceManager.Get<IThemeService>().ApplyFontScale(ApplicationSettingsV2Model.DefaultUIScale / 100.0);
            }

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            ActivationProtocolHandler.Close();

            base.OnExit(e);
        }

        private void Current_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e) { this.HandleCrash(e.Exception); }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e) { this.HandleCrash((Exception)e.ExceptionObject); }

        private void HandleCrash(Exception ex)
        {
            if (!this.crashObtained)
            {
#if DEBUG
                Debugger.Break();
#endif

                this.crashObtained = true;

                if (ServiceManager.Has<ITelemetryService>())
                {
                    ServiceManager.Get<ITelemetryService>().TrackException(ex);
                }

                try
                {
                    StringBuilder sb = new StringBuilder();
                    foreach (Exception e in ex.UnwrapException())
                    {
                        sb.AppendLine(e.ToString());
                        sb.AppendLine();
                    }

                    using (StreamWriter writer = File.AppendText(FileLoggerHandler.CurrentLogFilePath))
                    {
                        writer.WriteLine("CRASHING EXCEPTION: " + Environment.NewLine + sb.ToString() + Environment.NewLine + Environment.StackTrace);
                    }
                }
                catch (Exception) { }

                ServiceManager.Get<IProcessService>().LaunchProgram("MixItUp.Reporter.exe", $"{FileLoggerHandler.CurrentLogFilePath} {ChannelSession.Settings?.Name ?? "NONE"}");

                Task.Delay(3000).Wait();
            }
        }
    }
}

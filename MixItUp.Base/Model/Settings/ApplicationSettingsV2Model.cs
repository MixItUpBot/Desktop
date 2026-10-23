using MixItUp.Base.Services;
using MixItUp.Base.Util;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace MixItUp.Base.Model.Settings
{
    [DataContract]
    public class MainMenuControlSettings
    {
        [DataMember]
        public string Id { get; set; }

        [DataMember]
        public bool Hidden { get; set; }
    }

    [DataContract]
    public class ApplicationSettingsV2Model
    {
        private const string ApplicationSettingsFileName = "ApplicationSettings.json";

        /// <summary>
        /// The packaged font that the application UI uses unless the user selects a different one.
        /// </summary>
        public const string DefaultUIFontFamily = "Noto Sans";

        /// <summary>
        /// The UI font scale, as a percentage of the MD3 type scale's designed sizes. 100 is unscaled.
        /// Applies to the text scale only - icon sizes are deliberately not scaled.
        /// </summary>
        public const int DefaultUIScale = 100;
        public const int MinimumUIScale = 50;
        public const int MaximumUIScale = 150;

        /// <summary>
        /// Theme defaults, named so that the reset action and support mode resolve to exactly the
        /// same values the properties below are initialised with.
        /// </summary>
        public const string DefaultColorScheme = "Indigo";
        public const string DefaultBackgroundColor = "Light";
        public const string DefaultForegroundColor = "Default";
        public const string DefaultFullThemeName = "";

        public static async Task<ApplicationSettingsV2Model> Load()
        {
            ApplicationSettingsV2Model settings = null;
            bool settingsFileExists = ServiceManager.Get<IFileService>().FileExists(ApplicationSettingsFileName);
            if (settingsFileExists)
            {
                try
                {
                    settings = await FileSerializerHelper.DeserializeFromFile<ApplicationSettingsV2Model>(ApplicationSettingsFileName);
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
            }
            // Do not overwrite an existing file that failed to deserialize.
            bool canSaveSettings = settings != null || !settingsFileExists;
            if (settings == null)
            {
                settings = new ApplicationSettingsV2Model();
            }
            bool settingsChanged = false;
            if (settings.ForceResetPreviewProgram)
            {
                settings.ForceResetPreviewProgram = false;
                settingsChanged = true;
            }

            string updateChannel = BuildChannelHelper.GetUpdateChannel(settings.UpdateChannel, settings.PreviewProgram, false);
            if (updateChannel != "internal")
            {
                string savedUpdateChannel = updateChannel == "preview" ? "Preview" : "Public";
                bool previewProgram = updateChannel == "preview";
                if (settings.UpdateChannel != savedUpdateChannel || settings.PreviewProgram != previewProgram)
                {
                    settings.UpdateChannel = savedUpdateChannel;
                    settings.PreviewProgram = previewProgram;
                    settingsChanged = true;
                }
            }
            if (settingsChanged && canSaveSettings)
            {
                await settings.Save();
            }
            return settings;
        }

        [JsonIgnore]
        public bool SettingsChangeRestartRequired { get; set; }

        [DataMember]
        public bool DiagnosticLogging { get; set; } = false;

        [DataMember]
        public bool PreviewProgram { get; set; } = false;

        [DataMember]
        public string UpdateChannel { get; set; } = string.Empty;

        [DataMember]
        public bool ForceResetPreviewProgram { get; set; } = true;

        [DataMember]
        public bool TestBuild { get; set; } = false;

        [DataMember]
        public Guid AutoLogInID { get; set; } = Guid.Empty;

        [DataMember]
        public string ColorScheme { get; set; } = "Indigo";

        [DataMember]
        public string BackgroundColor { get; set; } = "Light";

        [DataMember]
        public string FullThemeName { get; set; } = string.Empty;

        [DataMember]
        public string ForegroundColor { get; set; } = "Default";

        [DataMember]
        public string UIFontFamily { get; set; } = DefaultUIFontFamily;

        [DataMember]
        public int UIScale { get; set; } = DefaultUIScale;

        [DataMember]
        public bool DontSaveLastWindowPosition { get; set; }

        [DataMember]
        public double Top { get; set; }

        [DataMember]
        public double Left { get; set; }

        [DataMember]
        public double Width { get; set; }

        [DataMember]
        public double Height { get; set; }

        [DataMember]
        public bool IsMaximized { get; set; }

        [DataMember]
        public double DashboardTop { get; set; }

        [DataMember]
        public double DashboardLeft { get; set; }

        [DataMember]
        public double DashboardWidth { get; set; }

        [DataMember]
        public double DashboardHeight { get; set; }

        [DataMember]
        public bool IsDashboardMaximized { get; set; }

        [DataMember]
        public bool IsDashboardPinned { get; set; }

        [DataMember]
        public LanguageOptions LanguageOption { get; set; }

        [DataMember]
        public string SettingsRestoreFilePath { get; set; }

        [DataMember]
        public Guid SettingsToReplaceDuringRestore { get; set; }

        [DataMember]
        public Guid SettingsToDelete { get; set; }

        [JsonIgnore]
        public bool IsDarkBackground { get { return this.BackgroundColor.Equals("Dark"); } }

        [DataMember]
        public List<MainMenuControlSettings> MainMenuControls { get; set; } = new List<MainMenuControlSettings>();

        [DataMember]
        public int LastReadNotificationId { get; set; } = 0;

        [DataMember]
        public bool DisableHardwareAcceleration { get; set; } = false;

        public ApplicationSettingsV2Model() { }

        public async Task Save()
        {
            try
            {
                await FileSerializerHelper.SerializeToFile(ApplicationSettingsFileName, this);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }
    }
}

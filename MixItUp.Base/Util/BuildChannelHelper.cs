namespace MixItUp.Base.Util
{
    public static class BuildChannelHelper
    {
        // Dev CDN root for update manifest/packages; production uses the public CDN
#if DEBUG_BUILD
        public const string API_FILES_UPDATE_ROOT = "https://dev.files.mixitup.bot/api/v2/apps/mixitup-desktop/windows-x64";
#else
        public const string API_FILES_UPDATE_ROOT = "https://files.mixitup.bot/api/v2/apps/mixitup-desktop/windows-x64";
#endif

        // Skip the version check in debug so a local build can launch without being current
#if DEBUG_BUILD
        public const bool BYPASS_UPDATE_CHECK = true;
#else
        public const bool BYPASS_UPDATE_CHECK = false;
#endif

        // Appended to window titles and version strings to identify non-public builds
#if PREVIEW_BUILD
        public const string BUILD_CHANNEL_SUFFIX = " - Preview";
#elif INTERNAL_BUILD
        public const string BUILD_CHANNEL_SUFFIX = " - Internal";
#elif DEV_BUILD
        public const string BUILD_CHANNEL_SUFFIX = " - Dev";
#elif DEBUG_BUILD
        public const string BUILD_CHANNEL_SUFFIX = " - Debug";
#else
        public const string BUILD_CHANNEL_SUFFIX = "";
#endif

        public static string GetReleaseChannel()
        {
#if DEBUG_BUILD
            return "debug";
#elif INTERNAL_BUILD
            return "internal";
#elif DEV_BUILD
            return "dev";
#elif PREVIEW_BUILD
            return "preview";
#else
            return "public";
#endif
        }

        public static string GetUpdateChannel(string updateChannel, bool previewProgram, bool testBuild)
        {
            if (string.Equals(updateChannel, "Internal", System.StringComparison.OrdinalIgnoreCase))
            {
                return "internal";
            }
            if (string.Equals(updateChannel, "Public", System.StringComparison.OrdinalIgnoreCase))
            {
                return "public";
            }
            if (string.Equals(updateChannel, "Preview", System.StringComparison.OrdinalIgnoreCase))
            {
                return "preview";
            }
            return previewProgram ? "preview" : "public";
        }
    }
}

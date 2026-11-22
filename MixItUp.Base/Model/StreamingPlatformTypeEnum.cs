using MixItUp.Base.Services;
using MixItUp.Base.Services.Kick.New;
using MixItUp.Base.Services.Mock.New;
using MixItUp.Base.Services.Twitch.New;
using MixItUp.Base.Services.Velora.New;
using MixItUp.Base.Services.VPZone.New;
using MixItUp.Base.Services.Joystick;
using MixItUp.Base.Services.AltStream;
using MixItUp.Base.Services.Razo;
using MixItUp.Base.Services.YouTube.New;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MixItUp.Base.Model
{
    public enum StreamingPlatformTypeEnum
    {
        None = 0,

        [Obsolete]
        Mixer = 1,
        Twitch = 2,
        YouTube = 3,
        [Obsolete]
        Trovo = 4,
        [Obsolete]
        Glimesh = 5,
        [Obsolete]
        Facebook = 6,
        Kick = 7,
        Velora = 8,
        VPZone = 9,
        Joystick = 10,
        AltStream = 11,
        Razo = 12,

        All = 99999,

        Mock = 100000,
    }

    public static class StreamingPlatforms
    {
        public const string TwitchLogoImageAssetFilePath = "/Assets/Images/twitch-color_lg.png";
        public const string YouTubeLogoImageAssetFilePath = "/Assets/Images/youtube-color_lg.png";
        public const string KickLogoImageAssetFilePath = "/Assets/Images/kick-color_lg.png";
        public const string VeloraLogoImageAssetFilePath = "/Assets/Images/velora-color_lg.png";
        public const string VPZoneLogoImageAssetFilePath = "/Assets/Images/vpzone-color_lg.png";
        public const string JoystickLogoImageAssetFilePath = "/Assets/Images/joystick-color_lg.png";
        public const string AltStreamLogoImageAssetFilePath = "/Assets/Images/altstream-color_lg.png";
        public const string RazoLogoImageAssetFilePath = "/Assets/Images/razo-color_lg.png";

        public const string TwitchSmallLogoImageAssetFilePath = "/Assets/Images/twitch-color_sm.png";
        public const string YouTubeSmallLogoImageAssetFilePath = "/Assets/Images/youtube-color_sm.png";
        public const string KickSmallLogoImageAssetFilePath = "/Assets/Images/kick-color_sm.png";
        public const string VeloraSmallLogoImageAssetFilePath = "/Assets/Images/velora-color_sm.png";
        public const string VPZoneSmallLogoImageAssetFilePath = "/Assets/Images/vpzone-color_sm.png";
        public const string JoystickSmallLogoImageAssetFilePath = "/Assets/Images/joystick-color_sm.png";
        public const string AltStreamSmallLogoImageAssetFilePath = "/Assets/Images/altstream-color_sm.png";
        public const string RazoSmallLogoImageAssetFilePath = "/Assets/Images/razo-color_sm.png";

        public static ISet<StreamingPlatformTypeEnum> SupportedPlatforms { get; private set; } = new HashSet<StreamingPlatformTypeEnum>()
        {
            StreamingPlatformTypeEnum.Twitch,
            StreamingPlatformTypeEnum.YouTube,
            StreamingPlatformTypeEnum.Kick,
            StreamingPlatformTypeEnum.Velora,
            StreamingPlatformTypeEnum.VPZone,
            StreamingPlatformTypeEnum.Joystick,
            StreamingPlatformTypeEnum.AltStream,
            StreamingPlatformTypeEnum.Razo,
        };

        public static ISet<StreamingPlatformTypeEnum> SelectablePlatforms { get; private set; } = new HashSet<StreamingPlatformTypeEnum>()
        {
            StreamingPlatformTypeEnum.All,
            StreamingPlatformTypeEnum.Twitch,
            StreamingPlatformTypeEnum.YouTube,
            StreamingPlatformTypeEnum.Kick,
            StreamingPlatformTypeEnum.Velora,
            StreamingPlatformTypeEnum.VPZone,
            StreamingPlatformTypeEnum.Joystick,
            StreamingPlatformTypeEnum.AltStream,
            StreamingPlatformTypeEnum.Razo,
        };

        public static bool IsValidPlatform(StreamingPlatformTypeEnum platform) { return StreamingPlatforms.SupportedPlatforms.Contains(platform); }

        public static bool ContainsPlatform(StreamingPlatformTypeEnum platform, StreamingPlatformTypeEnum check) { return platform == StreamingPlatformTypeEnum.All || platform == check; }

        public static IEnumerable<StreamingPlatformSessionBase> GetPlatformSessions()
        {
            return new List<StreamingPlatformSessionBase>()
            {
                ServiceManager.Get<TwitchSession>(),
                ServiceManager.Get<YouTubeSession>(),
                ServiceManager.Get<KickSession>(),
                ServiceManager.Get<VeloraSession>(),
                ServiceManager.Get<VPZoneSession>(),
                ServiceManager.Get<JoystickSession>(),
                ServiceManager.Get<AltStreamSession>(),
                ServiceManager.Get<RazoSession>(),
                //ServiceManager.Get<MockSession>()
            };
        }

        public static bool IsPlatformEnabled(StreamingPlatformTypeEnum platform) { return StreamingPlatforms.GetPlatformSession(platform).IsEnabled; }

        public static IEnumerable<StreamingPlatformSessionBase> GetEnabledPlatformSessions()
        {
            return StreamingPlatforms.GetPlatformSessions().Where(p => p.IsEnabled);
        }

        public static bool IsPlatformConnected(StreamingPlatformTypeEnum platform) { return StreamingPlatforms.GetPlatformSession(platform).IsConnected; }

        public static IEnumerable<StreamingPlatformSessionBase> GetConnectedPlatformSessions()
        {
            return StreamingPlatforms.GetPlatformSessions().Where(p => p.IsConnected);
        }

        public static StreamingPlatformSessionBase GetPlatformSession(StreamingPlatformTypeEnum platform)
        {
            if (platform == StreamingPlatformTypeEnum.Twitch) { return ServiceManager.Get<TwitchSession>(); }
            else if (platform == StreamingPlatformTypeEnum.YouTube) { return ServiceManager.Get<YouTubeSession>(); }
            else if (platform == StreamingPlatformTypeEnum.Kick) { return ServiceManager.Get<KickSession>(); }
            else if (platform == StreamingPlatformTypeEnum.Velora) { return ServiceManager.Get<VeloraSession>(); }
            else if (platform == StreamingPlatformTypeEnum.VPZone) { return ServiceManager.Get<VPZoneSession>(); }
            else if (platform == StreamingPlatformTypeEnum.Joystick) { return ServiceManager.Get<JoystickSession>(); }
            else if (platform == StreamingPlatformTypeEnum.AltStream) { return ServiceManager.Get<AltStreamSession>(); }
            else if (platform == StreamingPlatformTypeEnum.Razo) { return ServiceManager.Get<RazoSession>(); }
            else if (platform == StreamingPlatformTypeEnum.Mock) { return ServiceManager.Get<MockSession>(); }
            else if (platform == StreamingPlatformTypeEnum.All && ChannelSession.Settings != null)
            {
                return StreamingPlatforms.GetPlatformSession(ChannelSession.Settings.DefaultStreamingPlatform);
            }
            return null;
        }

        public static string GetPlatformImage(StreamingPlatformTypeEnum platform)
        {
            if (platform == StreamingPlatformTypeEnum.Twitch) { return TwitchLogoImageAssetFilePath; }
            else if (platform == StreamingPlatformTypeEnum.YouTube) { return YouTubeLogoImageAssetFilePath; }
            else if (platform == StreamingPlatformTypeEnum.Kick) { return KickLogoImageAssetFilePath; }
            else if (platform == StreamingPlatformTypeEnum.Velora) { return VeloraLogoImageAssetFilePath; }
            else if (platform == StreamingPlatformTypeEnum.VPZone) { return VPZoneLogoImageAssetFilePath; }
            else if (platform == StreamingPlatformTypeEnum.Joystick) { return JoystickLogoImageAssetFilePath; }
            else if (platform == StreamingPlatformTypeEnum.AltStream) { return AltStreamLogoImageAssetFilePath; }
            else if (platform == StreamingPlatformTypeEnum.Razo) { return RazoLogoImageAssetFilePath; }
            return string.Empty;
        }

        public static string GetPlatformSmallImage(StreamingPlatformTypeEnum platform)
        {
            if (platform == StreamingPlatformTypeEnum.Twitch) { return TwitchSmallLogoImageAssetFilePath; }
            else if (platform == StreamingPlatformTypeEnum.YouTube) { return YouTubeSmallLogoImageAssetFilePath; }
            else if (platform == StreamingPlatformTypeEnum.Kick) { return KickSmallLogoImageAssetFilePath; }
            else if (platform == StreamingPlatformTypeEnum.Velora) { return VeloraSmallLogoImageAssetFilePath; }
            else if (platform == StreamingPlatformTypeEnum.VPZone) { return VPZoneSmallLogoImageAssetFilePath; }
            else if (platform == StreamingPlatformTypeEnum.Joystick) { return JoystickSmallLogoImageAssetFilePath; }
            else if (platform == StreamingPlatformTypeEnum.AltStream) { return AltStreamSmallLogoImageAssetFilePath; }
            else if (platform == StreamingPlatformTypeEnum.Razo) { return RazoSmallLogoImageAssetFilePath; }
            return string.Empty;
        }

        public static IEnumerable<StreamingPlatformTypeEnum> GetConnectedPlatforms()
        {
            List<StreamingPlatformTypeEnum> platforms = new List<StreamingPlatformTypeEnum>();
            foreach (StreamingPlatformTypeEnum platform in StreamingPlatforms.SupportedPlatforms)
            {
                if (StreamingPlatforms.IsPlatformConnected(platform))
                {
                    platforms.Add(platform);
                }
            }
            return platforms;
        }

        public static void ForEachPlatform(Action<StreamingPlatformTypeEnum> action)
        {
            foreach (StreamingPlatformTypeEnum platform in StreamingPlatforms.SupportedPlatforms)
            {
                action(platform);
            }
        }

        public static async Task ForEachPlatform(Func<StreamingPlatformTypeEnum, Task> function)
        {
            foreach (StreamingPlatformTypeEnum platform in StreamingPlatforms.SupportedPlatforms)
            {
                await function(platform);
            }
        }
    }
}

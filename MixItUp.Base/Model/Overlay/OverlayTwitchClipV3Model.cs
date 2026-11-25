using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.Twitch.Clips;
using MixItUp.Base.Model.Twitch.User;
using MixItUp.Base.Services;
using MixItUp.Base.Services.Twitch;
using MixItUp.Base.Services.Twitch.New;
using MixItUp.Base.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MixItUp.Base.Model.Overlay
{
    public enum OverlayTwitchClipV3ClipType
    {
        LatestClip,
        RandomClip,
        SpecificClip,
        LatestFeaturedClip,
        RandomFeaturedClip,
    }

    [DataContract]
    public class OverlayTwitchClipV3Model : OverlayItemV3ModelBase
    {

        public static readonly string DefaultHTML = OverlayResources.OverlayTwitchClipVideoDefaultHTML;
        public static readonly string DefaultCSS = OverlayResources.OverlayTwitchClipVideoDefaultCSS;
        public static readonly string DefaultJavascript = OverlayResources.OverlayActionDefaultJavascript + Environment.NewLine + Environment.NewLine + OverlayResources.OverlayVideoActionDefaultJavascript;

        public static readonly List<string> TwitchClipURLPrefixes = new List<string>()
        {
            "https://clips.twitch.tv/",
            "clips.twitch.tv/"
        };

        public static readonly List<string> TwitchChannelClipURLRegexPatterns = new List<string>()
        {
            "https:\\/\\/www\\.twitch\\.tv\\/\\w+\\/clip\\/",
            "www\\.twitch\\.tv\\/\\w+\\/clip\\/"
        };

        [DataMember]
        public OverlayTwitchClipV3ClipType ClipType { get; set; }

        [DataMember]
        public string ClipReferenceID { get; set; }

        [DataMember]
        public double Volume { get; set; }

        public const string ClipIDProperty = "ClipID";
        public const string ClipDirectLinkProperty = "ClipDirectLink";

        public bool SelectsRandomClip => this.ClipType == OverlayTwitchClipV3ClipType.RandomClip || this.ClipType == OverlayTwitchClipV3ClipType.RandomFeaturedClip;

        [JsonIgnore]
        public string ClipHeight { get { return (this.Height > 0) ? $"{this.Height}px" : "100%"; } }
        [JsonIgnore]
        public string ClipWidth { get { return (this.Width > 0) ? $"{this.Width}px" : "100%"; } }

        public OverlayTwitchClipV3Model() : base(OverlayItemV3Type.TwitchClip) { }

        public override Dictionary<string, object> GetGenerationProperties()
        {
            Dictionary<string, object> properties = base.GetGenerationProperties();
            properties[nameof(this.Volume)] = this.Volume.ToInvariantNumberString();
            properties[nameof(this.ClipHeight)] = this.ClipHeight;
            properties[nameof(this.ClipWidth)] = this.ClipWidth;
            return properties;
        }

        internal async Task<ClipModel> SelectClip(CommandParametersModel parameters)
        {
            if (ServiceManager.Get<TwitchSession>().IsConnected)
            {
                string clipReferenceID = await SpecialIdentifierStringBuilder.ProcessSpecialIdentifiers(this.ClipReferenceID, parameters);

                if (this.SelectsRandomClip && !string.IsNullOrWhiteSpace(this.ClipReferenceID) && string.IsNullOrWhiteSpace(clipReferenceID))
                {
                    return null;
                }

                UserModel twitchUser = ServiceManager.Get<TwitchSession>().StreamerModel;
                if (!string.IsNullOrEmpty(clipReferenceID))
                {
                    if (this.ClipType == OverlayTwitchClipV3ClipType.RandomClip || this.ClipType == OverlayTwitchClipV3ClipType.LatestClip ||
                        this.ClipType == OverlayTwitchClipV3ClipType.RandomFeaturedClip || this.ClipType == OverlayTwitchClipV3ClipType.LatestFeaturedClip)
                    {
                        twitchUser = await ServiceManager.Get<TwitchSession>().StreamerService.GetNewAPIUserByLogin(clipReferenceID);
                        if (twitchUser == null)
                        {
                            // No valid user found, fail out and send an error message
                            await ServiceManager.Get<ChatService>().SendMessage(Resources.OverlayTwitchClipErrorUnableToFindValidClip, StreamingPlatformTypeEnum.Twitch);
                            return null;
                        }
                    }
                }

                ClipModel clip = null;
                if (this.SelectsRandomClip)
                {
                    bool featured = this.ClipType == OverlayTwitchClipV3ClipType.RandomFeaturedClip;
                    IEnumerable<ClipModel> clips = await ServiceManager.Get<TwitchSession>().StreamerService.GetClips(twitchUser, featured: featured, maxResults: 500);
                    if (clips != null && clips.Count() > 0)
                    {
                        clip = clips.Random();
                    }

                    if (clip == null && this.ClipType == OverlayTwitchClipV3ClipType.RandomFeaturedClip)
                    {
                        clips = await ServiceManager.Get<TwitchSession>().StreamerService.GetClips(twitchUser, featured: false, maxResults: 500);
                        if (clips != null && clips.Count() > 0)
                        {
                            clip = clips.Random();
                        }
                    }
                }
                else if (this.ClipType == OverlayTwitchClipV3ClipType.LatestClip || this.ClipType == OverlayTwitchClipV3ClipType.LatestFeaturedClip)
                {
                    DateTimeOffset startDate = DateTimeOffset.Now.Subtract(TimeSpan.FromDays(365));
                    bool featured = this.ClipType == OverlayTwitchClipV3ClipType.LatestFeaturedClip;
                    IEnumerable<ClipModel> clips = await ServiceManager.Get<TwitchSession>().StreamerService.GetClips(twitchUser, startDate: startDate, endDate: DateTimeOffset.Now, featured: featured, maxResults: 500);
                    if (clips != null && clips.Count() > 0)
                    {
                        clip = clips.OrderByDescending(c => c.created_at).FirstOrDefault();
                    }

                    if (clip == null && this.ClipType == OverlayTwitchClipV3ClipType.LatestFeaturedClip)
                    {
                        clips = await ServiceManager.Get<TwitchSession>().StreamerService.GetClips(twitchUser, startDate: startDate, endDate: DateTimeOffset.Now, featured: false, maxResults: 500);
                        if (clips != null && clips.Count() > 0)
                        {
                            clip = clips.OrderByDescending(c => c.created_at).FirstOrDefault();
                        }
                    }
                }
                else if (this.ClipType == OverlayTwitchClipV3ClipType.SpecificClip && !string.IsNullOrEmpty(clipReferenceID))
                {
                    foreach (string prefix in TwitchClipURLPrefixes)
                    {
                        clipReferenceID = clipReferenceID.Replace(prefix, "");
                    }

                    foreach (string pattern in TwitchChannelClipURLRegexPatterns)
                    {
                        clipReferenceID = Regex.Replace(clipReferenceID, pattern, "");
                    }

                    if (clipReferenceID.Contains("?"))
                    {
                        clipReferenceID = clipReferenceID.Substring(0, clipReferenceID.IndexOf("?"));
                    }

                    clip = await ServiceManager.Get<TwitchSession>().StreamerService.GetClip(clipReferenceID);
                }

                if (clip == null)
                {
                    // No valid clip found, fail out and send an error message
                    await ServiceManager.Get<ChatService>().SendMessage(Resources.OverlayTwitchClipErrorUnableToFindValidClip, StreamingPlatformTypeEnum.Twitch);
                    return null;
                }

                return clip;
            }

            return null;
        }
    }
}
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace MixItUp.Base.Model.User.Platform
{
    [DataContract]
    public class RazoUserPlatformV2Model : UserPlatformV2ModelBase
    {
        private const string Badges = "https://files.mixitup.bot/static/platforms/razo/badges/";
        public RazoUserPlatformV2Model() { this.Platform = StreamingPlatformTypeEnum.Razo; }

        internal static string GetUserID(string username) => "user:" + username.ToLowerInvariant();

        public static RazoUserPlatformV2Model FromChat(JObject data, string channel)
        {
            string name = data.Value<string>("username");
            if (string.IsNullOrWhiteSpace(name)) return null;
            var user = new RazoUserPlatformV2Model { ID = GetUserID(name), Username = name,
                DisplayName = data.Value<string>("display_name") ?? name, AvatarLink = data.Value<string>("avatar_url") };
            var badges = new HashSet<string>(data["badges"] is JArray values ? values.Values<string>() : Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            if (badges.Contains("broadcaster") && string.Equals(name, channel, StringComparison.OrdinalIgnoreCase))
            {
                user.Roles.Add(UserRoleEnum.Streamer);
                user.RoleBadgeLink = Badges + "broadcaster.png";
            }
            else if (badges.Contains("moderator"))
            {
                user.Roles.Add(UserRoleEnum.Moderator);
                user.RoleBadgeLink = Badges + "moderator.png";
            }
            if (badges.Contains("subscriber"))
            {
                user.Roles.Add(UserRoleEnum.Subscriber);
                user.SubscriberBadgeLink = Badges + "subscriber.png";
            }
            if (badges.Contains("founder")) user.SpecialtyBadgeLink = Badges + "founder.png";
            return user;
        }
        public override Task Refresh() => Task.CompletedTask;
    }
}

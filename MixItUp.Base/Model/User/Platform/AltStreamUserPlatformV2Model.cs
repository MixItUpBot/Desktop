using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace MixItUp.Base.Model.User.Platform
{
    [DataContract]
    public class AltStreamUserPlatformV2Model : UserPlatformV2ModelBase
    {
        private const string BadgeRoot = "https://files.mixitup.bot/static/platforms/altstream/badges/";
        [DataMember]
        public string Color { get; set; }
        public AltStreamUserPlatformV2Model() { this.Platform = StreamingPlatformTypeEnum.AltStream; }

        public static AltStreamUserPlatformV2Model FromAuthor(JObject author, bool fromBot = false)
        {
            string name = author?.Value<string>("channelName") ?? author?.Value<string>("username");
            if (string.IsNullOrWhiteSpace(name)) return null;
            var user = new AltStreamUserPlatformV2Model
            {
                ID = author.Value<string>("id"), Username = name, DisplayName = author.Value<string>("displayName") ?? name,
                Color = author.Value<string>("color"), AvatarLink = author.Value<string>("image")
            };
            if (DateTimeOffset.TryParse(author.Value<string>("createdAt"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var created))
                user.AccountDate = created;
            if (!fromBot && !string.IsNullOrWhiteSpace(user.ID) && author.Value<bool?>("isBot") != true && author["roles"] is JObject roles && roles.Value<bool?>("isBot") != true)
            {
                if (roles.Value<bool?>("broadcaster") == true) { user.Roles.Add(UserRoleEnum.Streamer); user.RoleBadgeLink = BadgeRoot + "broadcaster.png"; }
                else if (roles.Value<bool?>("moderator") == true) { user.Roles.Add(UserRoleEnum.Moderator); user.RoleBadgeLink = BadgeRoot + "moderator.png"; }
                if (roles.Value<bool?>("channelSubscriber") == true) user.Roles.Add(UserRoleEnum.Subscriber);
                if (roles.Value<bool?>("follower") == true) user.Roles.Add(UserRoleEnum.Follower);
                if (roles.Value<bool?>("altstreamSubscriber") == true) user.SpecialtyBadgeLink = BadgeRoot + "supporter.png";
            }
            return user;
        }

        public void UpdateFrom(AltStreamUserPlatformV2Model author)
        {
            this.Username = author.Username;
            this.DisplayName = author.DisplayName;
            this.Color = author.Color;
            this.AccountDate = author.AccountDate ?? this.AccountDate;
            this.AvatarLink = author.AvatarLink ?? this.AvatarLink;
            this.RoleBadgeLink = author.RoleBadgeLink;
            this.SpecialtyBadgeLink = author.SpecialtyBadgeLink;
            this.Roles = author.Roles;
        }
        public override Task Refresh() => Task.CompletedTask;
    }
}

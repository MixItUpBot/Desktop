using Newtonsoft.Json.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace MixItUp.Base.Model.User.Platform
{
    [DataContract]
    public class JoystickUserPlatformV2Model : UserPlatformV2ModelBase
    {
        private const string Badges = "https://files.mixitup.bot/static/platforms/joystick/badges/";

        [DataMember]
        public string Color { get; set; }

        public JoystickUserPlatformV2Model() { this.Platform = StreamingPlatformTypeEnum.Joystick; }

        public static JoystickUserPlatformV2Model FromIdentity(JObject identity)
        {
            string channel = identity?.Value<string>("channel_id");
            string username = identity?.Value<string>("username");
            if (string.IsNullOrWhiteSpace(channel) || string.IsNullOrWhiteSpace(username)) { return null; }
            var user = new JoystickUserPlatformV2Model
            {
                ID = channel, Username = username, DisplayName = identity.Value<string>("nickname") ?? username,
                AvatarLink = identity.Value<string>("photo_url"), RoleBadgeLink = Badges + "broadcaster.png"
            };
            user.Roles.Add(UserRoleEnum.Streamer);
            return user;
        }

        public static JoystickUserPlatformV2Model FromAuthor(JObject author)
        {
            string username = author?.Value<string>("username");
            if (string.IsNullOrWhiteSpace(username)) { return null; }
            var user = new JoystickUserPlatformV2Model { ID = "user:" + username.ToLowerInvariant() };
            user.SetAuthor(author);
            return user;
        }

        public void SetAuthor(JObject author)
        {
            this.Username = author.Value<string>("username");
            this.DisplayName = author.Value<string>("nickname") ?? this.Username;
            this.Color = author.Value<string>("color");
            if (!(author["badges"] is JObject badges)) { return; }

            this.Roles.Remove(UserRoleEnum.Moderator);
            this.Roles.Remove(UserRoleEnum.Subscriber);
            if (badges.Value<bool?>("mod") == true) { this.Roles.Add(UserRoleEnum.Moderator); }
            if (badges.Value<bool?>("subscriber") == true) { this.Roles.Add(UserRoleEnum.Subscriber); }
            this.SubscriberBadgeLink = this.Roles.Contains(UserRoleEnum.Subscriber) ? Badges + "subscriber.png" : null;
            this.RoleBadgeLink = this.Roles.Contains(UserRoleEnum.Streamer) ? Badges + "broadcaster.png" :
                this.Roles.Contains(UserRoleEnum.Moderator) ? Badges + "moderator.png" : null;
            this.SpecialtyBadgeLink = null;
            foreach (var badge in new[] { ("bot", "bot"), ("staff", "staff"), ("streamer", "streamer"), ("new", "new-user") })
            {
                if (badges.Value<bool?>(badge.Item1) == true)
                {
                    this.SpecialtyBadgeLink = Badges + badge.Item2 + ".png";
                    break;
                }
            }
        }

        public override Task Refresh() { return Task.CompletedTask; }
    }
}

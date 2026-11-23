using MixItUp.Base.Services;
using MixItUp.Base.Services.AltStream;
using MixItUp.Base.Util;
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
        private static readonly Uri site = new Uri("https://altstream.online/");
        private const string BadgeRoot = "https://files.mixitup.bot/static/platforms/altstream/badges/";
        private readonly object avatarRefreshLock = new object();
        private Task avatarRefresh = Task.CompletedTask;
        internal DateTimeOffset LastAvatarLookup { get; set; }
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
                Color = author.Value<string>("color"), AvatarLink = GetAvatarURL(author.Value<string>("image"))
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

        private static string GetAvatarURL(string image)
        {
            return !string.IsNullOrWhiteSpace(image) && Uri.TryCreate(site, image.Trim(), out Uri url) &&
                (url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeHttp) ? url.AbsoluteUri : null;
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context) => this.AvatarLink = GetAvatarURL(this.AvatarLink);

        public void UpdateFrom(AltStreamUserPlatformV2Model author)
        {
            this.Username = author.Username;
            this.DisplayName = author.DisplayName;
            this.Color = author.Color;
            this.AccountDate = author.AccountDate ?? this.AccountDate;
            lock (this.avatarRefreshLock)
            {
                if (author.LastAvatarLookup > this.LastAvatarLookup)
                {
                    this.LastAvatarLookup = author.LastAvatarLookup;
                    this.AvatarLink = author.AvatarLink;
                }
                else if (author.AvatarLink != null)
                {
                    this.AvatarLink = author.AvatarLink;
                }
            }
            this.RoleBadgeLink = author.RoleBadgeLink;
            this.SpecialtyBadgeLink = author.SpecialtyBadgeLink;
            this.Roles = author.Roles;
        }

        public override Task Refresh() => this.RefreshAvatar(ServiceManager.Get<AltStreamSession>());

        internal Task RefreshAvatar(AltStreamSession session)
        {
            if (session?.IsBotConnected != true || string.IsNullOrWhiteSpace(this.Username) || string.IsNullOrWhiteSpace(this.ID) ||
                this.ID.StartsWith("bot:", StringComparison.Ordinal) || this.ID.StartsWith("user:", StringComparison.Ordinal))
            {
                return Task.CompletedTask;
            }

            lock (this.avatarRefreshLock)
            {
                var now = DateTimeOffset.UtcNow;
                if (this.avatarRefresh.IsCompleted && now - this.LastAvatarLookup >= this.RefreshTimeSpan)
                {
                    this.LastAvatarLookup = now;
                    this.avatarRefresh = this.ReadProfileAvatar(session, this.AvatarLink);
                }
                return this.avatarRefresh;
            }
        }

        private async Task ReadProfileAvatar(AltStreamSession session, string previousAvatar)
        {
            var profile = await AsyncRunner.RunAsync(session.FindUser(this.Username));
            lock (this.avatarRefreshLock)
            {
                // A newer live image can arrive while the profile request is in flight.
                if (profile?.ID == this.ID && string.Equals(profile.Username, this.Username, StringComparison.OrdinalIgnoreCase) && this.AvatarLink == previousAvatar)
                {
                    this.AvatarLink = profile.AvatarLink;
                }
            }
        }
    }
}

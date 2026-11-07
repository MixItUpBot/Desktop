using Newtonsoft.Json;
using System.Linq;

namespace MixItUp.Base.Model.Velora.Users
{
    public class UserModel
    {
        [JsonProperty("id")]
        public string ID { get; set; }

        [JsonProperty("userId")]
        public string UserId { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("avatarUrl")]
        public string AvatarUrl { get; set; }

        [JsonProperty("avatar")]
        public string Avatar { get; set; }

        [JsonProperty("profilePicture")]
        public string ProfilePicture { get; set; }

        [JsonProperty("followerCount")]
        public long? FollowerCount { get; set; }

        [JsonProperty("bio")]
        public string Bio { get; set; }

        [JsonProperty("color")]
        public string Color { get; set; }

        [JsonProperty("isLive")]
        public bool? IsLive { get; set; }

        [JsonIgnore]
        public string UserID { get { return FirstNonEmpty(this.ID, this.UserId); } }

        [JsonIgnore]
        public string BestAvatarUrl { get { return FirstNonEmpty(this.AvatarUrl, this.Avatar, this.ProfilePicture); } }

        [JsonIgnore]
        public string BestDisplayName { get { return FirstNonEmpty(this.DisplayName, this.Username); } }

        internal static string FirstNonEmpty(params string[] values)
        {
            return values?.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
        }
    }
}

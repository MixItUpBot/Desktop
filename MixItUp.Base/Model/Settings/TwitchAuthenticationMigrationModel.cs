using MixItUp.Base.Model.Twitch.ChannelPoints;
using MixItUp.Base.Model.Web;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace MixItUp.Base.Model.Settings
{
    [DataContract]
    public class TwitchAuthenticationMigrationModel
    {
        [DataMember]
        public string UserID { get; set; }
        [DataMember]
        public OAuthTokenModel NewUserToken { get; set; }
        [DataMember]
        public OAuthTokenModel NewBotToken { get; set; }
        [DataMember]
        public bool AuthenticationConfirmed { get; set; }
        [DataMember]
        public bool InventorySaved { get; set; }
        [DataMember]
        public List<Guid> ExistingNewRewardIDs { get; set; } = new List<Guid>();
        [DataMember]
        public List<TwitchRewardMigrationModel> Rewards { get; set; } = new List<TwitchRewardMigrationModel>();
        [DataMember]
        public bool Completed { get; set; }
        [DataMember]
        public bool CredentialsCleared { get; set; }
        [DataMember]
        public int CredentialCleanupVersion { get; set; }
    }

    [DataContract]
    public class TwitchRewardMigrationModel
    {
        [DataMember]
        public CustomChannelPointRewardModel Original { get; set; }
        [DataMember]
        public bool DeleteStarted { get; set; }
        [DataMember]
        public bool CreateStarted { get; set; }
        [DataMember]
        public Guid NewRewardID { get; set; }
        // Null preserves the original prompt; an empty string is an approved removal.
        [DataMember]
        public string ReplacementPrompt { get; set; }
    }
}

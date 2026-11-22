using MixItUp.Base.Model.Commands;
using MixItUp.Base.Services;
using MixItUp.Base.ViewModel.User;
using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace MixItUp.Base.Model.Actions
{
    [DataContract]
    public class UserLookupActionModel : ActionModelBase
    {
        public const string UsernameSpecialIdentifier = "lookupusername";
        public const string DisplayNameSpecialIdentifier = "lookupdisplayname";
        public const string IDSpecialIdentifier = "lookupid";
        public const string AvatarURLSpecialIdentifier = "lookupavatarurl";
        public const string SuccessSpecialIdentifier = "lookupsuccess";

        // A lookup that misses everything we already know falls through to the platform's API. That
        // path is throttled so this action can't be pointed at a list and turned into a scraper.
        private static readonly TimeSpan PlatformSearchCooldown = TimeSpan.FromSeconds(60);
        private static readonly object platformSearchLock = new object();
        private static DateTimeOffset lastPlatformSearch = DateTimeOffset.MinValue;

        [DataMember]
        public string UsernameOrID { get; set; }

        [DataMember]
        public StreamingPlatformTypeEnum Platform { get; set; }

        public UserLookupActionModel(string usernameOrID, StreamingPlatformTypeEnum platform)
            : base(ActionTypeEnum.UserLookup)
        {
            this.UsernameOrID = usernameOrID;
            this.Platform = platform;
        }

        [Obsolete]
        public UserLookupActionModel() { }

        protected override Task PerformInternal(CommandParametersModel parameters)
        {
            return LookupUser(this.UsernameOrID, this.Platform, parameters);
        }

        internal static async Task LookupUser(string usernameOrID, StreamingPlatformTypeEnum platform, CommandParametersModel parameters)
        {
            usernameOrID = await ReplaceStringWithSpecialModifiers(usernameOrID, parameters);

            UserV2ViewModel user = null;
            if (!string.IsNullOrEmpty(usernameOrID))
            {
                // The value can be either form, so it goes in as both and whichever matches wins.
                user = await ServiceManager.Get<UserService>().GetUserByPlatform(platform, platformID: usernameOrID, platformUsername: usernameOrID);
                if (user == null && TryStartPlatformSearch())
                {
                    user = await ServiceManager.Get<UserService>().GetUserByPlatform(platform, platformID: usernameOrID, platformUsername: usernameOrID, performPlatformSearch: true);
                }
            }

            // Always assigned, even on a miss, so a failed lookup leaves blanks instead of the raw
            // special identifier text sitting in whatever the next action sends.
            parameters.SpecialIdentifiers[UsernameSpecialIdentifier] = user?.Username ?? string.Empty;
            parameters.SpecialIdentifiers[DisplayNameSpecialIdentifier] = user?.DisplayName ?? string.Empty;
            parameters.SpecialIdentifiers[IDSpecialIdentifier] = user?.PlatformID ?? string.Empty;
            parameters.SpecialIdentifiers[AvatarURLSpecialIdentifier] = user?.AvatarLink ?? string.Empty;
            parameters.SpecialIdentifiers[SuccessSpecialIdentifier] = (user != null).ToString();
        }

        private static bool TryStartPlatformSearch()
        {
            lock (platformSearchLock)
            {
                DateTimeOffset now = DateTimeOffset.Now;
                if ((now - lastPlatformSearch) < PlatformSearchCooldown)
                {
                    return false;
                }
                lastPlatformSearch = now;
                return true;
            }
        }
    }
}

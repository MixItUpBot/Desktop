using MixItUp.Base.Services;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Chat;
using MixItUp.Base.ViewModel.Chat.Twitch;
using MixItUp.Base.ViewModel.User;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace MixItUp.Base.Model.Commands
{
    public class TwitchRedemptionContext
    {
        public string RedemptionID { get; set; }
        public string RewardID { get; set; }
    }

    [DataContract]
    public class CommandParametersModel
    {
        public static CommandParametersModel GetTestParameters(Dictionary<string, string> specialIdentifiers)
        {
            var platform = StreamingPlatformTypeEnum.All;
            if (specialIdentifiers != null && specialIdentifiers.TryGetValue(SpecialIdentifierStringBuilder.StreamingPlatformSpecialIdentifier, out string name)
                && Enum.TryParse(name, out StreamingPlatformTypeEnum eventPlatform) && StreamingPlatforms.IsValidPlatform(eventPlatform))
            {
                platform = eventPlatform;
            }

            var user = GetDefaultTestUser(platform);
            return new CommandParametersModel(user, platform, new[] { "@" + user.Username }, specialIdentifiers);
        }

        public static UserV2ViewModel GetDefaultTestUser(StreamingPlatformTypeEnum platform)
        {
            if (platform == StreamingPlatformTypeEnum.All)
            {
                return ChannelSession.User ?? UserV2ViewModel.CreateUnassociated();
            }

            var session = StreamingPlatforms.GetPlatformSession(platform);
            if (session?.IsConnected == true)
            {
                if (session.Streamer != null) { return session.Streamer; }
                // Bot-token connections identify the channel by name and have no streamer account.
                if (session is ChannelChatSessionBase) { return UserV2ViewModel.CreateUnassociated(session.ChannelID); }
            }
            return UserV2ViewModel.CreateUnassociated();
        }

        public async Task SetTestUser(string username)
        {
            username = username?.Trim().TrimStart('@');
            if (string.IsNullOrWhiteSpace(username)) { return; }

            bool resolveChannelOwner = this.Platform != StreamingPlatformTypeEnum.All && this.User.IsUnassociated
                && StreamingPlatforms.GetPlatformSession(this.Platform) is ChannelChatSessionBase { IsConnected: true };
            if (resolveChannelOwner || !string.Equals(username, this.User.Username, StringComparison.OrdinalIgnoreCase))
            {
                var user = await ServiceManager.Get<UserService>().GetUserByPlatform(this.Platform, platformUsername: username, performPlatformSearch: true);
                this.User = user ?? UserV2ViewModel.CreateUnassociated(username);
            }
        }

        public static List<string> GenerateArguments(string arguments)
        {
            return (!string.IsNullOrEmpty(arguments)) ? arguments.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries).ToList() : new List<string>();
        }

        [DataMember]
        public UserV2ViewModel User { get; set; } = ChannelSession.User;
        [DataMember]
        public StreamingPlatformTypeEnum Platform { get; set; } = StreamingPlatformTypeEnum.All;
        [DataMember]
        public List<string> Arguments { get; private set; } = new List<string>();
        [DataMember]
        public Dictionary<string, string> SpecialIdentifiers { get; set; } = new Dictionary<string, string>();

        [DataMember]
        public bool IgnoreRequirements { get; set; }

        [DataMember]
        public bool UseCommandLocks { get; set; }

        [DataMember]
        public UserV2ViewModel TargetUser { get; set; }

        [DataMember]
        public string TriggeringChatMessageID { get; set; }

        [DataMember]
        public bool ExitCommand { get; set; }

        [DataMember]
        public Guid InitialCommandID { get; set; } = Guid.Empty;

        public TwitchRedemptionContext TwitchRedemption { get; set; }

        public CommandParametersModel() : this(ChannelSession.User) { }

        public CommandParametersModel(UserV2ViewModel user) : this(user, (user != null) ? user.Platform : StreamingPlatformTypeEnum.None) { }

        public CommandParametersModel(StreamingPlatformTypeEnum platform) : this(null, platform, null, null) { }

        public CommandParametersModel(StreamingPlatformTypeEnum platform, IEnumerable<string> arguments) : this(null, platform, arguments, null) { }

        public CommandParametersModel(StreamingPlatformTypeEnum platform, Dictionary<string, string> specialIdentifiers) : this(null, platform, null, specialIdentifiers) { }

        public CommandParametersModel(UserV2ViewModel user, StreamingPlatformTypeEnum platform) : this(user, platform, null, null) { }

        public CommandParametersModel(UserV2ViewModel user, IEnumerable<string> arguments) : this(user, (user != null) ? user.Platform : StreamingPlatformTypeEnum.None, arguments, null) { }

        public CommandParametersModel(UserV2ViewModel user, Dictionary<string, string> specialIdentifiers) : this(user, null, specialIdentifiers) { }

        public CommandParametersModel(UserV2ViewModel user, StreamingPlatformTypeEnum platform, IEnumerable<string> arguments) : this(user, platform, arguments, null) { }

        public CommandParametersModel(UserV2ViewModel user, StreamingPlatformTypeEnum platform, Dictionary<string, string> specialIdentifiers) : this(user, platform, null, specialIdentifiers) { }

        public CommandParametersModel(UserV2ViewModel user, IEnumerable<string> arguments, Dictionary<string, string> specialIdentifiers) : this(user, (user != null) ? user.Platform : StreamingPlatformTypeEnum.None, arguments, specialIdentifiers) { }

        public CommandParametersModel(ChatMessageViewModel message, IEnumerable<string> arguments = null)
            : this(message.User, message.Platform, (arguments != null) ? arguments : message.ToArguments())
        {
            this.SpecialIdentifiers["messageemotecount"] = message.EmotesOnlyContents.Count().ToString();
            this.SpecialIdentifiers["messageemotes"] = string.Join(",", message.EmotesOnlyContents.Select(e => e.Name));
            this.SpecialIdentifiers["messageemoteurls"] = string.Join(",", message.EmotesOnlyContents.Select(e => e.OverlayAnimatedOrStaticImageURL));
            this.SpecialIdentifiers["messagenoemotes"] = message.TextOnlyMessageContents;
            this.SpecialIdentifiers["message"] = message.PlainTextMessage;

            this.SpecialIdentifiers["messageinmychannel"] = message.IsInUsersChannel.ToString().ToLowerInvariant();
            this.SpecialIdentifiers["messageissharedchat"] = (!message.IsInUsersChannel).ToString().ToLowerInvariant();
            if (message is TwitchChatMessageViewModel twitchMessage)
            {
                this.SpecialIdentifiers["messagesourcechannel"] = twitchMessage.SourceChannelName ?? string.Empty;
                this.SpecialIdentifiers["messagesourcechannelid"] = twitchMessage.SourceChannelID ?? string.Empty;
            }
            this.TriggeringChatMessageID = message.ID;
        }

        public CommandParametersModel(UserV2ViewModel user = null, StreamingPlatformTypeEnum platform = StreamingPlatformTypeEnum.None, IEnumerable<string> arguments = null, Dictionary<string, string> specialIdentifiers = null)
        {
            if (user != null)
            {
                this.User = user;
            }

            if (arguments != null)
            {
                this.Arguments = new List<string>(arguments);
            }

            if (specialIdentifiers != null)
            {
                this.SpecialIdentifiers = new Dictionary<string, string>(specialIdentifiers);
            }

            if (platform != StreamingPlatformTypeEnum.None)
            {
                this.Platform = platform;
            }
            else
            {
                this.Platform = this.User.Platform;
            }

            if (this.Platform == StreamingPlatformTypeEnum.None)
            {
                this.Platform = StreamingPlatformTypeEnum.All;
            }    

            this.SpecialIdentifiers[SpecialIdentifierStringBuilder.StreamingPlatformSpecialIdentifier] = this.Platform.ToString();

            this.ParseArguments();
        }

        public bool IsTargetUserSelf { get { return this.TargetUser == this.User; } }
        public bool HasNoTarget { get; set; }
        public bool DisplayOnlyActor { get; set; }

        public void SetArguments(string arguments)
        {
            if (!string.IsNullOrEmpty(arguments))
            {
                this.SetArguments(arguments.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
            }
        }

        public void SetArguments(IEnumerable<string> arguments)
        {
            this.Arguments = new List<string>(arguments);
            this.ParseArguments();
        }

        public CommandParametersModel Duplicate(IEnumerable<string> arguments = null)
        {
            CommandParametersModel result = new CommandParametersModel(this.User, this.Platform, (arguments != null) ? arguments : this.Arguments, this.SpecialIdentifiers);
            result.TargetUser = this.TargetUser;
            result.HasNoTarget = this.HasNoTarget;
            result.DisplayOnlyActor = this.DisplayOnlyActor;
            result.TwitchRedemption = this.TwitchRedemption;
            return result;
        }

        public void ParseArguments()
        {
            if (this.Arguments != null && this.Arguments.Count > 0)
            {
                if (this.Platform == StreamingPlatformTypeEnum.YouTube)
                {
                    List<string> newArguments = new List<string>();
                    for (int i = 0; i < this.Arguments.Count; i++)
                    {
                        if (this.Arguments[i].StartsWith("@"))
                        {
                            string usernameTag = this.Arguments[i];
                            for (int j = i + 1; j < this.Arguments.Count; j++)
                            {
                                usernameTag += " " + this.Arguments[j];
                                UserV2ViewModel userTag = ServiceManager.Get<UserService>().GetActiveUserByPlatform(StreamingPlatformTypeEnum.YouTube, platformUsername: usernameTag);
                                if (userTag != null)
                                {
                                    newArguments.Add(usernameTag);
                                    usernameTag = null;
                                    i = j;
                                    break;
                                }
                            }

                            if (!string.IsNullOrWhiteSpace(usernameTag))
                            {
                                for (; i < this.Arguments.Count; i++)
                                {
                                    newArguments.Add(this.Arguments[i]);
                                }
                            }
                        }
                        else
                        {
                            newArguments.Add(this.Arguments[i]);
                        }
                    }

                    if (newArguments.Count < this.Arguments.Count)
                    {
                        this.Arguments.Clear();
                        this.Arguments.AddRange(newArguments);
                    }
                }
            }
        }

        public async Task SetTargetUser()
        {
            if (this.HasNoTarget) return;
            if (this.TargetUser == null)
            {
                if (this.Arguments != null && this.Arguments.Count > 0)
                {
                    string username = UserService.SanitizeUsername(this.Arguments.ElementAt(0));
                    if (this.Arguments.Count > 0)
                    {
                        this.TargetUser = await ServiceManager.Get<UserService>().GetUserByPlatform(this.Platform, platformUsername: username, performPlatformSearch: true);
                        if (this.TargetUser != null && !string.Equals(username, this.TargetUser.Username, StringComparison.OrdinalIgnoreCase))
                        {
                            this.TargetUser = null;
                        }
                    }
                }

                if (this.TargetUser == null)
                {
                    this.TargetUser = this.User;
                }
                await this.TargetUser.Refresh();
            }
        }
    }
}

using MixItUp.Base.Model.Commands;
using MixItUp.Base.Services;
using MixItUp.Base.Services.Velora.New;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.User;
using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace MixItUp.Base.Model.Actions
{
    public enum VeloraActionType
    {
        SetTitle,
        SetGame,
        Announce,
        ClearChat,
        ModUser,
        UnmodUser,
        VIPUser,
        UnVIPUser,
        BanUser,
        UnbanUser,
        TimeoutUser,
        UntimeoutUser,
        Raid,
        Shoutout,
        GrantChannelPoints,
        DeductChannelPoints,
        CreatePoll,
        EndPoll,
        GetActivePoll,
    }

    public enum VeloraAnnounceColor
    {
        Default,
        Blue,
        Gold,
        Green,
        Red,
        Orange,
        Coral,
    }

    [DataContract]
    public class VeloraActionModel : ActionModelBase
    {
        public static VeloraActionModel CreateTextAction(VeloraActionType type, string text)
        {
            return new VeloraActionModel(type) { Text = text };
        }

        public static VeloraActionModel CreateAction(VeloraActionType type)
        {
            return new VeloraActionModel(type);
        }

        [DataMember]
        public VeloraActionType ActionType { get; set; }

        // Multi-purpose text field: stream title / game / announcement message / raid target channel.
        [DataMember]
        public string Text { get; set; }

        // Target user (mod/vip/ban/timeout/unban/untimeout/shoutout) or channel-points target (@user/@all/...).
        [DataMember]
        public string TargetUsername { get; set; }

        // Timeout duration (seconds) or channel-points amount.
        [DataMember]
        public string Amount { get; set; }

        // Optional reason for ban / timeout (REST moderate supports it).
        [DataMember]
        public string Reason { get; set; }

        // Announcement accent color (maps to /announce vs /announceblue|gold|green|red|orange|coral).
        [DataMember]
        public VeloraAnnounceColor AnnounceColor { get; set; }

        // Announce: post as the streamer instead of the connected bot (the bot is the default when connected).
        [DataMember]
        public bool SendAsStreamer { get; set; }

        [DataMember]
        public string PollOptions { get; set; }
        [DataMember]
        public bool PollMultiple { get; set; }

        private VeloraActionModel(VeloraActionType type)
            : base(ActionTypeEnum.Velora)
        {
            this.ActionType = type;
        }

        [Obsolete]
        public VeloraActionModel() { }

        protected override async Task PerformInternal(CommandParametersModel parameters)
        {
            VeloraSession session = ServiceManager.Get<VeloraSession>();
            if (!session.IsConnected)
            {
                return;
            }

            switch (this.ActionType)
            {
                case VeloraActionType.CreatePoll:
                    {
                        string question = await ReplaceStringWithSpecialModifiers(this.Text, parameters);
                        string optionsText = await ReplaceStringWithSpecialModifiers(this.PollOptions, parameters);
                        string[] options = (optionsText ?? string.Empty).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(o => o.Trim()).Where(o => o.Length > 0).ToArray();
                        string durationText = await ReplaceStringWithSpecialModifiers(this.Amount, parameters);
                        if (string.IsNullOrWhiteSpace(question) || options.Length < 2 || options.Length > 6 ||
                            !int.TryParse(durationText, out int duration) || duration < 15 || duration > 3600)
                        {
                            throw new InvalidOperationException(Resources.VeloraPollSettingsHint);
                        }
                        parameters.SpecialIdentifiers["velorapollid"] = string.Empty;
                        JToken poll = await session.StreamerService.CreatePoll(session.StreamerModel.Username, new JObject()
                        {
                            ["question"] = question, ["options"] = JArray.FromObject(options),
                            ["durationSeconds"] = duration, ["allowMultiple"] = this.PollMultiple
                        });
                        if (poll == null) { throw new InvalidOperationException(Resources.FailedToUpdateChannelInformation); }
                        SetPollIdentifiers(parameters, poll);
                    }
                    break;
                case VeloraActionType.GetActivePoll:
                    SetPollIdentifiers(parameters, await session.StreamerService.GetActivePolls(session.StreamerModel.Username));
                    break;
                case VeloraActionType.EndPoll:
                    {
                        string pollID = await ReplaceStringWithSpecialModifiers(this.Text, parameters);
                        if (string.IsNullOrWhiteSpace(pollID)) { throw new InvalidOperationException(Resources.ValidValueMustBeSpecified); }
                        Result result = await session.StreamerService.EndPoll(session.StreamerModel.Username, pollID);
                        if (!result.Success) { throw new InvalidOperationException(result.Message); }
                    }
                    break;
                case VeloraActionType.SetTitle:
                    {
                        string text = await ReplaceStringWithSpecialModifiers(this.Text, parameters);
                        Result result = await session.SetStreamTitle(text);
                        if (!result.Success)
                        {
                            await ServiceManager.Get<ChatService>().SendMessage(MixItUp.Base.Resources.FailedToUpdateChannelInformation, parameters);
                        }
                    }
                    break;
                case VeloraActionType.SetGame:
                    {
                        string text = await ReplaceStringWithSpecialModifiers(this.Text, parameters);
                        Result result = await session.SetStreamCategory(text);
                        if (!result.Success)
                        {
                            await ServiceManager.Get<ChatService>().SendMessage(MixItUp.Base.Resources.ErrorFailedToUpdateCategory, parameters);
                        }
                    }
                    break;
                case VeloraActionType.Announce:
                    {
                        string message = await ReplaceStringWithSpecialModifiers(this.Text, parameters);
                        if (!string.IsNullOrWhiteSpace(message))
                        {
                            string color = this.AnnounceColor == VeloraAnnounceColor.Default ? null : this.AnnounceColor.ToString().ToLowerInvariant();
                            await session.SendAnnouncement(message, color, this.SendAsStreamer);
                        }
                    }
                    break;
                case VeloraActionType.ClearChat:
                    await session.ClearMessages();
                    break;
                case VeloraActionType.Raid:
                    {
                        string channel = await ReplaceStringWithSpecialModifiers(this.Text, parameters);
                        await session.Raid(channel);
                    }
                    break;
                case VeloraActionType.GrantChannelPoints:
                case VeloraActionType.DeductChannelPoints:
                    {
                        string amount = await ReplaceStringWithSpecialModifiers(this.Amount, parameters);
                        string target = await ReplaceStringWithSpecialModifiers(this.TargetUsername, parameters);
                        if (string.IsNullOrWhiteSpace(target))
                        {
                            target = parameters.User?.Username;
                        }
                        string direction = this.ActionType == VeloraActionType.GrantChannelPoints ? "add" : "remove";
                        await session.AdjustChannelPoints(direction, amount, target);
                    }
                    break;
                default:
                    await this.PerformUserAction(session, parameters);
                    break;
            }
        }

        private static void SetPollIdentifiers(CommandParametersModel parameters, JToken result)
        {
            parameters.SpecialIdentifiers["velorapoll"] = result?.ToString(Newtonsoft.Json.Formatting.None) ?? string.Empty;
            JToken poll = result is JObject obj ? obj["poll"] ?? obj["data"] ?? result : result;
            if (poll is JArray array) { poll = array.FirstOrDefault(); }
            parameters.SpecialIdentifiers["velorapollid"] = poll is JObject value ? (value["id"] ?? value["pollId"])?.ToString() ?? string.Empty : string.Empty;
        }

        private async Task PerformUserAction(VeloraSession session, CommandParametersModel parameters)
        {
            // Resolve the target on the Velora platform: an explicit username (special identifiers allowed),
            // else the command's user. No-op if it cannot be resolved (matches the Moderation action).
            UserV2ViewModel targetUser;
            if (!string.IsNullOrEmpty(this.TargetUsername))
            {
                string username = await ReplaceStringWithSpecialModifiers(this.TargetUsername, parameters);
                targetUser = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Velora, platformUsername: username, performPlatformSearch: true);
            }
            else
            {
                targetUser = parameters.User;
            }

            if (targetUser == null)
            {
                return;
            }

            string reason = string.IsNullOrEmpty(this.Reason) ? null : await ReplaceStringWithSpecialModifiers(this.Reason, parameters);

            switch (this.ActionType)
            {
                case VeloraActionType.ModUser: await session.ModUser(targetUser); break;
                case VeloraActionType.UnmodUser: await session.UnmodUser(targetUser); break;
                case VeloraActionType.VIPUser: await session.VIPUser(targetUser); break;
                case VeloraActionType.UnVIPUser: await session.UnVIPUser(targetUser); break;
                case VeloraActionType.UnbanUser: await session.UnbanUser(targetUser); break;
                case VeloraActionType.UntimeoutUser: await session.UntimeoutUser(targetUser); break;
                case VeloraActionType.Shoutout: await session.Shoutout(targetUser); break;
                case VeloraActionType.BanUser: await session.BanUser(targetUser, reason); break;
                case VeloraActionType.TimeoutUser:
                    {
                        int duration = 300;
                        string amountString = await ReplaceStringWithSpecialModifiers(this.Amount, parameters);
                        if (!string.IsNullOrWhiteSpace(amountString) && int.TryParse(amountString, out int parsed) && parsed > 0)
                        {
                            duration = parsed;
                        }
                        await session.TimeoutUser(targetUser, duration, reason);
                    }
                    break;
            }
        }
    }
}

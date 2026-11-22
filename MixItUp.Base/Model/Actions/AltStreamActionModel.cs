using MixItUp.Base.Model.Commands;
using MixItUp.Base.Services;
using MixItUp.Base.Services.AltStream;
using MixItUp.Base.Util;
using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace MixItUp.Base.Model.Actions
{
    public enum AltStreamActionType
    {
        SendShoutout,
        FulfillRedemption,
        RefundRedemption,
        // Keep the Internal build's serialized values readable in saved commands.
        [Obsolete]
        GetChannelInfo,
        [Obsolete]
        GetChannelPanels,
        [Obsolete]
        GetChannelRewards,
        [Obsolete]
        GetChannelEmotes,
        [Obsolete]
        GetChannelTags,
        [Obsolete]
        GetUserProfile,
        [Obsolete]
        GetUserSettings,
        [Obsolete]
        GetUserAchievements,
        [Obsolete]
        GetStickers,
        [Obsolete]
        GetModerators,
        [Obsolete]
        GetRedemptions
    }

    [DataContract]
    public class AltStreamActionModel : ActionModelBase
    {
        [DataMember]
        public AltStreamActionType ActionType { get; set; }

        [DataMember]
        public string Target { get; set; }

        public AltStreamActionModel(AltStreamActionType actionType, string target) : base(ActionTypeEnum.AltStream)
        {
            this.ActionType = actionType;
            this.Target = target;
        }

        [Obsolete]
        public AltStreamActionModel() { }

        protected override async Task PerformInternal(CommandParametersModel parameters)
        {
            var session = ServiceManager.Get<AltStreamSession>();
            if (this.ActionType == AltStreamActionType.GetUserProfile)
            {
                string username = string.IsNullOrWhiteSpace(this.Target) ? session.ChannelID : this.Target;
                await UserLookupActionModel.LookupUser(username ?? string.Empty, StreamingPlatformTypeEnum.AltStream, parameters);
                return;
            }

            if (!session.IsBotConnected) return;
            switch (this.ActionType)
            {
                case AltStreamActionType.SendShoutout:
                    await session.Shoutout(await ReplaceStringWithSpecialModifiers(this.Target, parameters));
                    break;
                case AltStreamActionType.FulfillRedemption:
                case AltStreamActionType.RefundRedemption:
                    string target = await ReplaceStringWithSpecialModifiers(this.Target, parameters);
                    if (!long.TryParse(target, out long id) || id <= 0) throw new InvalidOperationException(Resources.ValidValueMustBeSpecified);
                    await session.SetRedemptionStatus(id, this.ActionType == AltStreamActionType.RefundRedemption);
                    break;
                default:
                    Logger.Log(LogLevel.Debug, $"Skipping discontinued AltStream action: {this.ActionType}");
                    break;
            }
        }
    }
}

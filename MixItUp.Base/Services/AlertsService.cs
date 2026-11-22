using MixItUp.Base.Util;
using MixItUp.Base.Model.Commands;
using System.Collections.Generic;
using MixItUp.Base.ViewModel.Chat;
using System.Linq;
using System.Threading.Tasks;

namespace MixItUp.Base.Services
{
    public class AlertsService
    {
        public ThreadSafeObservableCollection<AlertChatMessageViewModel> Alerts { get; private set; } = new ThreadSafeObservableCollection<AlertChatMessageViewModel>();
        private LockedDictionary<string, AlertChatMessageViewModel> alertsLookup = new LockedDictionary<string, AlertChatMessageViewModel>();

        internal Task AddPlatformEvent(EventTypeEnum type, CommandParametersModel parameters)
        {
            var settings = ChannelSession.Settings;
            var user = parameters.User;
            if (user == null) return Task.CompletedTask;
            var values = parameters.SpecialIdentifiers;
            string name = user.FullDisplayName;
            (string color, string message) = type switch
            {
                EventTypeEnum.AltStreamChannelFollowed or EventTypeEnum.RazoChannelFollowed or EventTypeEnum.JoystickChannelFollowed =>
                    (settings.AlertFollowColor, string.Format(Resources.AlertFollow, name)),
                EventTypeEnum.RazoChannelSubscribed or EventTypeEnum.JoystickChannelSubscribed =>
                    (settings.AlertSubColor, string.Format(Resources.AlertSubscribed, name)),
                EventTypeEnum.RazoChannelResubscribed or EventTypeEnum.JoystickChannelResubscribed =>
                    (settings.AlertSubColor, string.Format(Resources.AlertResubscribed, name, values.GetValueOrDefault("usersubmonths"))),
                EventTypeEnum.JoystickChannelSubscriptionGifted =>
                    (settings.AlertGiftedSubColor, string.Format(Resources.AlertSubscriptionGifted, name, parameters.TargetUser?.FullDisplayName)),
                EventTypeEnum.JoystickChannelMassSubscriptionsGifted or EventTypeEnum.RazoChannelMassSubscriptionsGifted =>
                    (settings.AlertMassGiftedSubColor, string.Format(Resources.AlertMassSubscriptionsGifted, name, values.GetValueOrDefault("subsgiftedamount"))),
                EventTypeEnum.AltStreamChannelRaided or EventTypeEnum.RazoChannelRaided =>
                    (settings.AlertRaidColor, string.Format(Resources.AlertRaid, name, values.GetValueOrDefault("raidviewercount"))),
                EventTypeEnum.AltStreamChannelPointsRedeemed =>
                    (settings.AlertAltStreamChannelPointsColor, string.Format(Resources.AlertAltStreamChannelPointRedeemed, name, values.GetValueOrDefault("rewardname"))),
                EventTypeEnum.JoystickChannelTipped =>
                    (settings.AlertJoystickTipsColor, $"{name} · {Resources.JoystickChannelTipped} · {values.GetValueOrDefault("joysticktokens")}"),
                EventTypeEnum.RazoChannelGift =>
                    (settings.AlertRazoGiftsColor, $"{name} · {Resources.RazoChannelGift} · {values.GetValueOrDefault("razosamount")}"),
                _ => (null, null)
            };
            return string.IsNullOrEmpty(color) ? Task.CompletedTask : this.AddAlert(new AlertChatMessageViewModel(user, message, color));
        }

        public async Task AddAlert(AlertChatMessageViewModel alert)
        {
            if (!string.IsNullOrEmpty(alert.Color))
            {
                this.alertsLookup[alert.ID] = alert;

                if (ChannelSession.Settings.LatestChatAtTop)
                {
                    this.Alerts.Insert(0, alert);
                }
                else
                {
                    this.Alerts.Add(alert);
                }

                if (this.Alerts.Count > ChannelSession.Settings.MaxMessagesInChat)
                {
                    AlertChatMessageViewModel removedAlert = (ChannelSession.Settings.LatestChatAtTop) ? this.Alerts.Last() : this.Alerts.First();
                    this.alertsLookup.Remove(removedAlert.ID);
                    this.Alerts.Remove(removedAlert);
                }

                await ServiceManager.Get<ChatService>().WriteToChatEventLog(alert);

                if (!ChannelSession.Settings.OnlyShowAlertsInDashboard)
                {
                    await ServiceManager.Get<ChatService>().AddMessage(alert);
                }
            }
        }
    }
}

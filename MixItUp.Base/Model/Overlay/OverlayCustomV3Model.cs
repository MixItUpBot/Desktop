using MixItUp.Base.Model.Joystick;
using MixItUp.Base.Model.Razo;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.Kick.Kicks;
using MixItUp.Base.Model.Velora;
using MixItUp.Base.Model.VPZone;
using MixItUp.Base.Model.Twitch.Bits;
using MixItUp.Base.Model.User;
using MixItUp.Base.Services;
using MixItUp.Base.Services.Twitch;
using MixItUp.Base.Services.Twitch.New;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Chat;
using MixItUp.Base.ViewModel.Chat.YouTube;
using MixItUp.Base.ViewModel.User;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace MixItUp.Base.Model.Overlay
{
    [DataContract]
    public class OverlayCustomV3Model : OverlayEventTrackingV3ModelBase
    {
        public static readonly string DefaultHTML = OverlayResources.OverlayCustomDefaultHTML;
        public static readonly string DefaultCSS = OverlayResources.OverlayCustomDefaultCSS + Environment.NewLine + Environment.NewLine + OverlayResources.OverlayTextDefaultCSS;
        public static readonly string DefaultJavascript = OverlayResources.OverlayCustomDefaultJavascript;

        [DataMember]
        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();

        public OverlayCustomV3Model() : base(OverlayItemV3Type.Custom) { }

        public override async void OnChatUserBanned(object sender, UserV2ViewModel user)
        {
            await this.CallFunction("UserBanned", new Dictionary<string, object>()
            {
                { "User", user }
            });
        }

        public override async void OnChatUserTimedOut(object sender, UserV2ViewModel user)
        {
            await this.CallFunction("UserTimeout", new Dictionary<string, object>()
            {
                { "User", user }
            });
        }

        public override async void OnChatMessageReceived(object sender, ChatMessageViewModel message)
        {
            await this.CallFunction("ChatMessageReceived", OverlayChatV3Model.GetMessageProperties(message));
        }

        public override async void OnChatMessageDeleted(object sender, string messageID)
        {
            await this.CallFunction("ChatMessageDeleted", new Dictionary<string, object>()
            {
                { "MessageID", messageID }
            });
        }

        public override async void OnChatCleared(object sender, EventArgs e)
        {
            await this.CallFunction("ChatCleared", new Dictionary<string, object>());
        }

        public override async void OnFollow(object sender, UserV2ViewModel user)
        {
            await this.CallFunction("Follow", new Dictionary<string, object>()
            {
                { "User", user }
            });
        }

        public override async void OnRaid(object sender, Tuple<UserV2ViewModel, int> raid)
        {
            await this.CallFunction("Raid", new Dictionary<string, object>()
            {
                { "User", raid.Item1 },
                { "Amount", raid.Item2 }
            });
        }

        public override async void OnSubscribe(object sender, SubscriptionDetailsModel subscription)
        {
            Dictionary<string, object> data = new Dictionary<string, object>()
            {
                { "User", subscription.User },
                { "Tier", subscription.Tier.ToString() },
                { "Months", subscription.Months.ToString() },
                { "Gifter", subscription.Gifter }
            };

            if (subscription.Platform == StreamingPlatformTypeEnum.YouTube)
            {
                data["Tier"] = subscription.YouTubeMembershipTier;
            }

            if (subscription.Gifter != null)
            {
                await this.CallFunction("SubscriptionGifted", data);
            }
            else
            {
                await this.CallFunction("Subscription", data);
            }
        }

        public override async void OnSubscriptionsGiftedWithoutRecipients(UserV2ViewModel gifter, int count)
        {
            await this.CallFunction("MassSubscriptionGifted", new Dictionary<string, object>
            {
                ["Users"] = Array.Empty<UserV2ViewModel>(), ["Tier"] = string.Empty, ["Amount"] = count, ["Gifter"] = gifter
            });
        }

        public override async void OnMassSubscription(object sender, IEnumerable<SubscriptionDetailsModel> subscriptions)
        {
            List<SubscriptionDetailsModel> subscriptionList = subscriptions?.Where(s => s != null).ToList();
            if (subscriptionList == null || subscriptionList.Count == 0)
            {
                return;
            }

            SubscriptionDetailsModel firstSubscription = subscriptionList[0];
            StreamingPlatformTypeEnum platform = firstSubscription.Platform;
            UserV2ViewModel gifter = firstSubscription.Gifter;
            int tier = firstSubscription.Tier;
            string membershipName = firstSubscription.YouTubeMembershipTier;
            int amount = subscriptionList.Count;

            Dictionary<string, object> data = new Dictionary<string, object>()
            {
                { "Users", subscriptionList.Select(s => s.User) },
                { "Tier", tier.ToString() },
                { "Amount", amount },
                { "Gifter", gifter }
            };

            if (platform == StreamingPlatformTypeEnum.YouTube)
            {
                data["Tier"] = membershipName;
            }

            await this.CallFunction("MassSubscriptionGifted", data);
        }

        public override async void OnDonation(object sender, UserDonationModel donation)
        {
            await this.CallFunction("Donation", new Dictionary<string, object>()
            {
                { "User", donation.User },
                { "Donation", donation }
            });
        }

        public override async void OnTwitchBits(object sender, TwitchBitsCheeredEventModel bitsCheered)
        {
            await this.CallFunction("TwitchBits", new Dictionary<string, object>()
            {
                { "User", bitsCheered.User },
                { "Amount", bitsCheered.Amount },
                { "Message", bitsCheered.Message }
            });
        }

        public override async void OnYouTubeSuperChat(object sender, YouTubeSuperChatViewModel superChat)
        {
            await this.CallFunction("YouTubeSuperChat", new Dictionary<string, object>()
            {
                { "User", superChat.User },
                { "Amount", superChat.Amount },
                { "AmountDisplay", superChat.AmountDisplay },
                { "Message", superChat.Message }
            });
        }

        public override async void OnKickKicksGifted(object sender, KickKicksGiftedEventModel kicksGifted)
        {
            await this.CallFunction("KickKicksGifted", new Dictionary<string, object>()
            {
                { "User", kicksGifted.User },
                { "Amount", kicksGifted.Amount },
                { "Message", kicksGifted.Message }
            });
        }

        public override async void OnVeloraCheered(object sender, VeloraCheeredEventModel cheered)
        {
            await this.CallFunction("VeloraCheered", new Dictionary<string, object>()
            {
                { "User", cheered.User },
                { "Amount", cheered.Amount },
                { "Message", cheered.Message }
            });
        }

        public override async void OnVPZoneCheered(object sender, VPZoneCheeredEventModel cheered)
        {
            await this.CallFunction("VPZoneCheered", new Dictionary<string, object>()
            {
                { "User", cheered.User },
                { "Amount", cheered.Amount },
                { "Message", cheered.Message }
            });
        }
        public override async void OnJoystickTipped(object sender, JoystickTippedEventModel cheered)
        {
            await this.CallFunction("JoystickTipped", new Dictionary<string, object>()
            {
                { "User", cheered.User },
                { "Amount", cheered.Amount },
                { "Message", cheered.Message }
            });
        }
        public override async void OnRazoGift(object sender, RazoGiftEventModel cheered)
        {
            await this.CallFunction("RazoGift", new Dictionary<string, object>()
            {
                { "User", cheered.User },
                { "Amount", cheered.Amount },
                { "Message", cheered.Message }
            });
        }

        public override Dictionary<string, object> GetGenerationProperties()
        {
            Dictionary<string, object> properties = base.GetGenerationProperties();

            return properties;
        }

        public override async Task ProcessGenerationProperties(Dictionary<string, object> properties, CommandParametersModel parameters)
        {
            foreach (var property in this.Properties)
            {
                properties[property.Key] = await SpecialIdentifierStringBuilder.ProcessSpecialIdentifiers(property.Value, parameters);
            }
        }
    }
}

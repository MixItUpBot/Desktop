using MixItUp.Base.Model.Joystick;
using MixItUp.Base.Model.Razo;
using Google.Apis.YouTube.v3.Data;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.Kick.Kicks;
using MixItUp.Base.Model.Velora;
using MixItUp.Base.Model.VPZone;
using MixItUp.Base.Model.Twitch.Bits;
using MixItUp.Base.Model.Twitch.Clients.PubSub.Messages;
using MixItUp.Base.Model.User;
using MixItUp.Base.Services;
using MixItUp.Base.Services.Twitch;
using MixItUp.Base.Services.Twitch.New;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Chat.Twitch;
using MixItUp.Base.ViewModel.Chat.YouTube;
using MixItUp.Base.ViewModel.User;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Base.Model.Overlay
{
    [DataContract]
    public class OverlayEventListHeaderV3Model : OverlayHeaderV3ModelBase
    {
        public OverlayEventListHeaderV3Model() { }
    }

    [DataContract]
    public class OverlayEventListV3Model : OverlayEventTrackingV3ModelBase
    {
        public const string DetailsAmountPropertyName = "Amount";
        public const string DetailsTierPropertyName = "Tier";
        public const string DetailsMembershipNamePropertyName = "MembershipName";

        public static readonly string DefaultHTML = OverlayResources.OverlayEventListDefaultHTML;
        public static readonly string DefaultCSS = OverlayResources.OverlayEventListDefaultCSS + Environment.NewLine + Environment.NewLine + OverlayResources.OverlayTextDefaultCSS + Environment.NewLine + Environment.NewLine + OverlayResources.OverlayHeaderTextDefaultCSS;
        public static readonly string DefaultJavascript = OverlayResources.OverlayEventListDefaultJavascript;

        [DataMember]
        public OverlayEventListHeaderV3Model Header { get; set; }

        [DataMember]
        public string BackgroundColor { get; set; }
        [DataMember]
        public string BorderColor { get; set; }

        [DataMember]
        public int TotalToShow { get; set; }
        [DataMember]
        public bool AddToTop { get; set; }

        [DataMember]
        public string FollowsDetailsTemplate { get; set; }
        [DataMember]
        public string RaidsDetailsTemplate { get; set; }

        [DataMember]
        public string TwitchSubscriptionsDetailsTemplate { get; set; }
        [DataMember]
        public string TwitchResubscriptionsDetailsTemplate { get; set; }
        [DataMember]
        public string TwitchGiftedSubscriptionsDetailsTemplate { get; set; }
        [DataMember]
        public string TwitchMassGiftedSubscriptionsDetailsTemplate { get; set; }
        [DataMember]
        public string TwitchBitsDetailsTemplate { get; set; }

        [DataMember]
        public string YouTubeMembershipsDetailsTemplate { get; set; }
        [DataMember]
        public string YouTubeRenewedMembershipsDetailsTemplate { get; set; }
        [DataMember]
        public string YouTubeGiftedMembershipsDetailsTemplate { get; set; }
        [DataMember]
        public string YouTubeMassGiftedMembershipsDetailsTemplate { get; set; }
        [DataMember]
        public string YouTubeSuperChatsDetailsTemplate { get; set; }

        [DataMember]
        public string KickSubscriptionsDetailsTemplate { get; set; }
        [DataMember]
        public string KickResubscriptionsDetailsTemplate { get; set; }
        [DataMember]
        public string KickGiftedSubscriptionsDetailsTemplate { get; set; }
        [DataMember]
        public string KickMassGiftedSubscriptionsDetailsTemplate { get; set; }
        [DataMember]
        public string KickKicksDetailsTemplate { get; set; }

        [DataMember]
        public string VeloraCheeredDetailsTemplate { get; set; }
        [DataMember]
        public string VPZoneCheeredDetailsTemplate { get; set; }
        [DataMember]
        public string JoystickTippedDetailsTemplate { get; set; }
        [DataMember]
        public string RazoGiftDetailsTemplate { get; set; }

        [DataMember]
        public string VeloraSubscriptionsDetailsTemplate { get; set; }
        [DataMember]
        public string VPZoneSubscriptionsDetailsTemplate { get; set; }
        [DataMember]
        public string VeloraResubscriptionsDetailsTemplate { get; set; }
        [DataMember]
        public string VPZoneResubscriptionsDetailsTemplate { get; set; }
        [DataMember]
        public string VeloraGiftedSubscriptionsDetailsTemplate { get; set; }
        [DataMember]
        public string VPZoneGiftedSubscriptionsDetailsTemplate { get; set; }
        [DataMember]
        public string VeloraMassGiftedSubscriptionsDetailsTemplate { get; set; }
        [DataMember]
        public string VPZoneMassGiftedSubscriptionsDetailsTemplate { get; set; }

        [DataMember]
        public string DonationsDetailsTemplate { get; set; }

        [DataMember]
        public OverlayAnimationV3Model ItemAddedAnimation { get; set; } = new OverlayAnimationV3Model();
        [DataMember]
        public OverlayAnimationV3Model ItemRemovedAnimation { get; set; } = new OverlayAnimationV3Model();

        public OverlayEventListV3Model() : base(OverlayItemV3Type.EventList) { }

        public override async void OnFollow(object sender, UserV2ViewModel user)
        {
            await this.AddEvent(user, nameof(this.Follows), this.FollowsDetailsTemplate, new Dictionary<string, string>());
        }

        public override async void OnRaid(object sender, Tuple<UserV2ViewModel, int> raid)
        {
            await this.AddEvent(raid.Item1, nameof(this.Raids), this.RaidsDetailsTemplate, new Dictionary<string, string>() { { DetailsAmountPropertyName, raid.Item2.ToString() } });
        }

        public override async void OnSubscribe(object sender, SubscriptionDetailsModel subscription)
        {
            if (subscription.Gifter != null)
            {
                if (subscription.Platform == StreamingPlatformTypeEnum.Twitch)
                {
                    await this.AddEvent(subscription.Gifter, nameof(this.TwitchSubscriptions), this.TwitchGiftedSubscriptionsDetailsTemplate, new Dictionary<string, string>() { { DetailsTierPropertyName, subscription.Tier.ToString() } });
                }
                else if (subscription.Platform == StreamingPlatformTypeEnum.YouTube)
                {
                    await this.AddEvent(subscription.Gifter, nameof(this.YouTubeMemberships), this.YouTubeGiftedMembershipsDetailsTemplate, new Dictionary<string, string>() { { DetailsMembershipNamePropertyName, subscription.Tier.ToString() } });
                }
                else if (subscription.Platform == StreamingPlatformTypeEnum.Kick)
                {
                    await this.AddEvent(subscription.Gifter, nameof(this.KickSubscriptions), this.KickGiftedSubscriptionsDetailsTemplate, new Dictionary<string, string>() { { DetailsTierPropertyName, subscription.Tier.ToString() } });
                }
                else if (subscription.Platform == StreamingPlatformTypeEnum.Velora)
                {
                    await this.AddEvent(subscription.Gifter, nameof(this.VeloraSubscriptions), this.VeloraGiftedSubscriptionsDetailsTemplate, new Dictionary<string, string>() { { DetailsTierPropertyName, subscription.Tier.ToString() } });
                }
                else if (subscription.Platform == StreamingPlatformTypeEnum.VPZone)
                {
                    await this.AddEvent(subscription.Gifter, nameof(this.VPZoneSubscriptions), this.VPZoneGiftedSubscriptionsDetailsTemplate, new Dictionary<string, string>() { { DetailsTierPropertyName, subscription.Tier.ToString() } });
                }
            }
            else if (subscription.Months > 1)
            {
                if (subscription.Platform == StreamingPlatformTypeEnum.Twitch)
                {
                    await this.AddEvent(subscription.User, nameof(this.TwitchSubscriptions), this.TwitchResubscriptionsDetailsTemplate, new Dictionary<string, string>()
                    {
                        { DetailsTierPropertyName, subscription.Tier.ToString() },
                        { DetailsAmountPropertyName, subscription.Months.ToString() }
                    });
                }
                else if (subscription.Platform == StreamingPlatformTypeEnum.YouTube)
                {
                    await this.AddEvent(subscription.User, nameof(this.YouTubeMemberships), this.YouTubeRenewedMembershipsDetailsTemplate, new Dictionary<string, string>()
                    {
                        { DetailsMembershipNamePropertyName, subscription.YouTubeMembershipTier },
                        { DetailsAmountPropertyName, subscription.Months.ToString() }
                    });
                }
                else if (subscription.Platform == StreamingPlatformTypeEnum.Kick)
                {
                    await this.AddEvent(subscription.User, nameof(this.KickSubscriptions), this.KickResubscriptionsDetailsTemplate, new Dictionary<string, string>()
                    {
                        { DetailsTierPropertyName, subscription.Tier.ToString() },
                        { DetailsAmountPropertyName, subscription.Months.ToString() }
                    });
                }
                else if (subscription.Platform == StreamingPlatformTypeEnum.Velora)
                {
                    await this.AddEvent(subscription.User, nameof(this.VeloraSubscriptions), this.VeloraResubscriptionsDetailsTemplate, new Dictionary<string, string>()
                    {
                        { DetailsTierPropertyName, subscription.Tier.ToString() },
                        { DetailsAmountPropertyName, subscription.Months.ToString() }
                    });
                }
                else if (subscription.Platform == StreamingPlatformTypeEnum.VPZone)
                {
                    await this.AddEvent(subscription.User, nameof(this.VPZoneSubscriptions), this.VPZoneResubscriptionsDetailsTemplate, new Dictionary<string, string>()
                    {
                        { DetailsTierPropertyName, subscription.Tier.ToString() },
                        { DetailsAmountPropertyName, subscription.Months.ToString() }
                    });
                }
            }
            else
            {
                if (subscription.Platform == StreamingPlatformTypeEnum.Twitch)
                {
                    await this.AddEvent(subscription.User, nameof(this.TwitchSubscriptions), this.TwitchSubscriptionsDetailsTemplate, new Dictionary<string, string>() { { DetailsTierPropertyName, subscription.Tier.ToString() } });
                }
                else if (subscription.Platform == StreamingPlatformTypeEnum.YouTube)
                {
                    await this.AddEvent(subscription.User, nameof(this.YouTubeMemberships), this.YouTubeMembershipsDetailsTemplate, new Dictionary<string, string>() { { DetailsMembershipNamePropertyName, subscription.YouTubeMembershipTier } });
                }
                else if (subscription.Platform == StreamingPlatformTypeEnum.Kick)
                {
                    await this.AddEvent(subscription.User, nameof(this.KickSubscriptions), this.KickSubscriptionsDetailsTemplate, new Dictionary<string, string>() { { DetailsTierPropertyName, subscription.Tier.ToString() } });
                }
                else if (subscription.Platform == StreamingPlatformTypeEnum.Velora)
                {
                    await this.AddEvent(subscription.User, nameof(this.VeloraSubscriptions), this.VeloraSubscriptionsDetailsTemplate, new Dictionary<string, string>() { { DetailsTierPropertyName, subscription.Tier.ToString() } });
                }
                else if (subscription.Platform == StreamingPlatformTypeEnum.VPZone)
                {
                    await this.AddEvent(subscription.User, nameof(this.VPZoneSubscriptions), this.VPZoneSubscriptionsDetailsTemplate, new Dictionary<string, string>() { { DetailsTierPropertyName, subscription.Tier.ToString() } });
                }
            }
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

            if (platform == StreamingPlatformTypeEnum.Twitch)
            {
                await this.AddEvent(gifter, nameof(this.TwitchSubscriptions), this.TwitchMassGiftedSubscriptionsDetailsTemplate, new Dictionary<string, string>()
                {
                    { DetailsTierPropertyName, tier.ToString() },
                    { DetailsAmountPropertyName, amount.ToString() }
                });
            }
            else if (platform == StreamingPlatformTypeEnum.YouTube)
            {
                await this.AddEvent(gifter, nameof(this.YouTubeMemberships), this.YouTubeMassGiftedMembershipsDetailsTemplate, new Dictionary<string, string>()
                {
                    { DetailsMembershipNamePropertyName, membershipName },
                    { DetailsAmountPropertyName, amount.ToString() }
                });
            }
            else if (platform == StreamingPlatformTypeEnum.Kick)
            {
                await this.AddEvent(gifter, nameof(this.KickSubscriptions), this.KickMassGiftedSubscriptionsDetailsTemplate, new Dictionary<string, string>()
                {
                    { DetailsTierPropertyName, tier.ToString() },
                    { DetailsAmountPropertyName, amount.ToString() }
                });
            }
            else if (platform == StreamingPlatformTypeEnum.Velora)
            {
                await this.AddEvent(gifter, nameof(this.VeloraSubscriptions), this.VeloraMassGiftedSubscriptionsDetailsTemplate, new Dictionary<string, string>()
                {
                    { DetailsTierPropertyName, tier.ToString() },
                    { DetailsAmountPropertyName, amount.ToString() }
                });
            }
            else if (platform == StreamingPlatformTypeEnum.VPZone)
            {
                await this.AddEvent(gifter, nameof(this.VPZoneSubscriptions), this.VPZoneMassGiftedSubscriptionsDetailsTemplate, new Dictionary<string, string>()
                {
                    { DetailsTierPropertyName, tier.ToString() },
                    { DetailsAmountPropertyName, amount.ToString() }
                });
            }
        }

        public override async void OnDonation(object sender, UserDonationModel donation)
        {
            await this.AddEvent(donation.User, nameof(this.Donations), this.DonationsDetailsTemplate, new Dictionary<string, string>() { { DetailsAmountPropertyName, donation.AmountText } });
        }

        public override async void OnTwitchBits(object sender, TwitchBitsCheeredEventModel bitsCheered)
        {
            await this.AddEvent(bitsCheered.User, nameof(this.TwitchBits), this.TwitchBitsDetailsTemplate, new Dictionary<string, string>() { { DetailsAmountPropertyName, bitsCheered.Amount.ToString() } });
        }

        public override async void OnYouTubeSuperChat(object sender, YouTubeSuperChatViewModel superChat)
        {
            await this.AddEvent(superChat.User, nameof(this.YouTubeSuperChats), this.YouTubeSuperChatsDetailsTemplate, new Dictionary<string, string>() { { DetailsAmountPropertyName, superChat.AmountDisplay } });
        }

        public override async void OnKickKicksGifted(object sender, KickKicksGiftedEventModel kicksGifted)
        {
            await this.AddEvent(kicksGifted.User, nameof(this.KickKicks), this.KickKicksDetailsTemplate, new Dictionary<string, string>() { { DetailsAmountPropertyName, kicksGifted.Amount.ToString() } });
        }

        public override async void OnVeloraCheered(object sender, VeloraCheeredEventModel cheered)
        {
            await this.AddEvent(cheered.User, nameof(this.VeloraCheered), this.VeloraCheeredDetailsTemplate, new Dictionary<string, string>() { { DetailsAmountPropertyName, cheered.Amount.ToString() } });
        }

        public override async void OnVPZoneCheered(object sender, VPZoneCheeredEventModel cheered)
        {
            await this.AddEvent(cheered.User, nameof(this.VPZoneCheered), this.VPZoneCheeredDetailsTemplate, new Dictionary<string, string>() { { DetailsAmountPropertyName, cheered.Amount.ToString() } });
        }
        public override async void OnJoystickTipped(object sender, JoystickTippedEventModel cheered)
        {
            await this.AddEvent(cheered.User, nameof(this.JoystickTipped), this.JoystickTippedDetailsTemplate, new Dictionary<string, string>() { { DetailsAmountPropertyName, cheered.Amount.ToString() } });
        }
        public override async void OnRazoGift(object sender, RazoGiftEventModel cheered)
        {
            await this.AddEvent(cheered.User, nameof(this.RazoGift), this.RazoGiftDetailsTemplate, new Dictionary<string, string>() { { DetailsAmountPropertyName, cheered.Amount.ToString() } });
        }

        public override Dictionary<string, object> GetGenerationProperties()
        {
            Dictionary<string, object> properties = base.GetGenerationProperties();

            foreach (var kvp in this.Header.GetGenerationProperties())
            {
                properties[kvp.Key] = kvp.Value;
            }

            properties[nameof(this.BackgroundColor)] = this.BackgroundColor;
            properties[nameof(this.BorderColor)] = this.BorderColor;

            properties[nameof(this.TotalToShow)] = this.TotalToShow;
            properties[nameof(this.AddToTop)] = this.AddToTop.ToString().ToLower();

            this.ItemAddedAnimation.AddAnimationProperties(properties, nameof(this.ItemAddedAnimation));
            this.ItemRemovedAnimation.AddAnimationProperties(properties, nameof(this.ItemRemovedAnimation));

            return properties;
        }

        public async Task ClearEvents()
        {
            await this.CallFunction("clear", new Dictionary<string, object>());
        }

        public async Task AddEvent(UserV2ViewModel user, string type, string template, Dictionary<string, string> properties)
        {
            if (string.IsNullOrWhiteSpace(template))
            {
                return;
            }

            if (user == null)
            {
                user = ChannelSession.User;
            }

            Dictionary<string, object> data = new Dictionary<string, object>();
            data["User"] = JObject.FromObject(user);
            data["Type"] = type;

            string details = template;
            foreach (var kvp in properties)
            {
                details = OverlayV3Service.ReplaceProperty(details, kvp.Key, kvp.Value);
            }
            details = await SpecialIdentifierStringBuilder.ProcessSpecialIdentifiers(details, new CommandParametersModel(user));
            data["Details"] = details;

            await this.CallFunction("add", data);
        }

        public Task TestAllEvents()
        {
            AsyncRunner.RunAsyncBackground(async (token) =>
            {
                if (this.Follows)
                {
                    this.OnFollow(this, ChannelSession.User);
                    await Task.Delay(3000);
                }

                if (this.Raids)
                {
                    this.OnRaid(this, new Tuple<UserV2ViewModel, int>(ChannelSession.User, 10));
                    await Task.Delay(3000);
                }

                if (this.TwitchSubscriptions)
                {
                    this.OnSubscribe(this, new SubscriptionDetailsModel(StreamingPlatformTypeEnum.Twitch, ChannelSession.User));
                    await Task.Delay(3000);

                    this.OnSubscribe(this, new SubscriptionDetailsModel(StreamingPlatformTypeEnum.Twitch, ChannelSession.User, months: 10));
                    await Task.Delay(3000);

                    this.OnSubscribe(this, new SubscriptionDetailsModel(StreamingPlatformTypeEnum.Twitch, ChannelSession.User, ChannelSession.User));
                    await Task.Delay(3000);

                    List<SubscriptionDetailsModel> subs = new List<SubscriptionDetailsModel>();
                    for (int i = 0; i < 5; i++)
                    {
                        subs.Add(new SubscriptionDetailsModel(StreamingPlatformTypeEnum.Twitch, ChannelSession.User, ChannelSession.User));
                    }
                    this.OnMassSubscription(this, subs);
                    await Task.Delay(3000);
                }

                if (this.TwitchBits)
                {
                    this.OnTwitchBits(this, new TwitchBitsCheeredEventModel(ChannelSession.User, 100, new TwitchChatMessageViewModel(ChannelSession.User, "Hello World")));
                    await Task.Delay(3000);
                }

                if (this.YouTubeMemberships)
                {
                    this.OnSubscribe(this, new SubscriptionDetailsModel(StreamingPlatformTypeEnum.YouTube, ChannelSession.User, youTubeMembershipTier: "Foobar"));
                    await Task.Delay(3000);

                    this.OnSubscribe(this, new SubscriptionDetailsModel(StreamingPlatformTypeEnum.YouTube, ChannelSession.User, months: 10, youTubeMembershipTier: "Foobar"));
                    await Task.Delay(3000);

                    this.OnSubscribe(this, new SubscriptionDetailsModel(StreamingPlatformTypeEnum.YouTube, ChannelSession.User, ChannelSession.User, youTubeMembershipTier: "Foobar"));
                    await Task.Delay(3000);

                    List<SubscriptionDetailsModel> subs = new List<SubscriptionDetailsModel>();
                    for (int i = 0; i < 5; i++)
                    {
                        subs.Add(new SubscriptionDetailsModel(StreamingPlatformTypeEnum.YouTube, ChannelSession.User, ChannelSession.User, youTubeMembershipTier: "Foobar"));
                    }
                    this.OnMassSubscription(this, subs);
                    await Task.Delay(3000);
                }

                if (this.YouTubeSuperChats)
                {
                    this.OnYouTubeSuperChat(this, new YouTubeSuperChatViewModel(new LiveChatSuperChatDetails() { AmountDisplayString = "$12.34" }, ChannelSession.User));
                    await Task.Delay(3000);
                }

                if (this.KickKicks)
                {
                    this.OnKickKicksGifted(this, new KickKicksGiftedEventModel(ChannelSession.User, 100, "Great stream!"));
                    await Task.Delay(3000);
                }

                if (this.VeloraCheered)
                {
                    this.OnVeloraCheered(this, new VeloraCheeredEventModel(ChannelSession.User, 100, "Great stream!"));
                    await Task.Delay(3000);
                }

                if (this.VPZoneCheered)
                {
                    this.OnVPZoneCheered(this, new VPZoneCheeredEventModel(ChannelSession.User, 100, "Great stream!"));
                    await Task.Delay(3000);
                }
                if (this.JoystickTipped)
                {
                    this.OnJoystickTipped(this, new JoystickTippedEventModel(ChannelSession.User, 100, "Great stream!"));
                    await Task.Delay(3000);
                }
                if (this.RazoGift)
                {
                    this.OnRazoGift(this, new RazoGiftEventModel(ChannelSession.User, 100, "Great stream!"));
                    await Task.Delay(3000);
                }

                if (this.Donations)
                {
                    this.OnDonation(this, new UserDonationModel()
                    {
                        Source = UserDonationSourceEnum.Streamlabs,

                        User = ChannelSession.User,
                        Username = ChannelSession.User.Username,

                        Message = "Text",

                        Amount = 12.34,

                        DateTime = DateTimeOffset.Now,
                    });
                    await Task.Delay(3000);
                };
            }, CancellationToken.None);

            return Task.CompletedTask;
        }
    }
}

using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.Kick.Kicks;
using MixItUp.Base.Model.Velora;
using MixItUp.Base.Model.VPZone;
using MixItUp.Base.Model.Twitch.Bits;
using MixItUp.Base.Model.User;
using MixItUp.Base.Services;
using MixItUp.Base.Services.External;
using MixItUp.Base.Services.Twitch.New;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Chat;
using MixItUp.Base.ViewModel.Chat.YouTube;
using MixItUp.Base.ViewModel.Overlay;
using MixItUp.Base.ViewModel.User;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MixItUp.Base.Model.Overlay
{
    public enum OverlayEndCreditsSectionV3Type
    {
        Chatters = 0,
        Followers,
        Subscribers,
        Moderators,
        VIPs,
        Regulars,

        Raids = 10,

        NewFollowers = 20,
        NewSubscribers,
        Resubscribers,
        GiftedSubscriptions,
        AllSubscriptions,

        FirstTimeChatters = 30,

        ChannelPoints = 35,

        TwitchBits = 40,
        TwitchPowerUps = 41,

        YouTubeSuperChats = 50,
        YouTubeJewels = 51,

        KickKicks = 60,

        VeloraCheered = 70,
        VPZoneCheered = 71,

        Donations = 200,

        Streamloots = 210,

        Patreon = 220,
        PatreonMembers = 221,

        HTML = 900,
        CustomSection = 999,
    }

    [DataContract]
    public class OverlayEndCreditsHeaderV3Model : OverlayHeaderV3ModelBase
    {
        public OverlayEndCreditsHeaderV3Model() { }
    }

    [DataContract]
    public class OverlayEndCreditsSectionV3Model
    {
        public const string SectionIDPropertyName = "SectionID";
        public const string UsernamePropertyName = "Username";
        public const string AmountPropertyName = "Amount";
        public const string TextPropertyName = "Text";

        public static readonly string DefaultHTML = OverlayResources.OverlayEndCreditsSectionDefaultHTML;

        public static readonly string UsernameItemTemplate = $"{{{UsernamePropertyName}}}";
        public static readonly string UsernameAmountItemTemplate = $"{{{UsernamePropertyName}}} - {{{AmountPropertyName}}}";
        public static readonly string TextItemTemplate = $"{{{TextPropertyName}}}";

        [DataMember]
        public Guid ID { get; set; }

        [DataMember]
        public OverlayEndCreditsSectionV3Type Type { get; set; }

        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public string ItemTemplate { get; set; }

        [DataMember]
        public int Columns { get; set; }

        [DataMember]
        public string HTML { get; set; } = string.Empty;

        [JsonIgnore]
        public double ColumnWidthPercentage
        {
            get
            {
                return Math.Round(100.0 / ((double)this.Columns), 4);
            }
        }

        [JsonIgnore]
        public Dictionary<UserV2ViewModel, double> itemTracking = new Dictionary<UserV2ViewModel, double>();

        [JsonIgnore]
        public List<Tuple<UserV2ViewModel, string>> customTracking = new List<Tuple<UserV2ViewModel, string>>();

        public void Track(UserV2ViewModel user) { this.Track(user, 0); }

        public void Track(UserV2ViewModel user, double amount)
        {
            if (this.ShouldTrack(user))
            {
                if (!this.itemTracking.ContainsKey(user))
                {
                    this.itemTracking[user] = 0;
                }
                this.itemTracking[user] += amount;

                Logger.Log(LogLevel.Debug, $"Tracking added for {user} to {this.itemTracking[user]} for {this.Name} End Credits section");
            }
            else
            {
                Logger.Log(LogLevel.Debug, $"No tracking {user} for {this.Name} End Credits section");
            }
        }

        public void Track(UserV2ViewModel user, string text)
        {
            this.customTracking.Add(new Tuple<UserV2ViewModel, string>(user, text));
            Logger.Log(LogLevel.Debug, $"Tracking added for {user} to {text} for {this.Name} End Credits section");
        }

        public void Untrack(UserV2ViewModel user)
        {
            this.itemTracking.Remove(user);

            foreach (var item in this.customTracking.ToList())
            {
                if (item.Item1.Equals(user))
                {
                    this.customTracking.Remove(item);
                }
            }
        }

        public void ClearTracking()
        {
            this.itemTracking.Clear();
            this.customTracking.Clear();
        }

        public Dictionary<string, object> GetGenerationProperties()
        {
            Dictionary<string, object> properties = new Dictionary<string, object>();

            properties[nameof(this.Name)] = this.Name;
            properties[OverlayEndCreditsSectionV3Model.SectionIDPropertyName] = this.ID.ToString();
            properties[nameof(this.ColumnWidthPercentage)] = this.ColumnWidthPercentage.ToString();

            return properties;
        }

        public async Task<IEnumerable<string>> GetItems()
        {
            List<string> items = new List<string>();

            if (this.Type == OverlayEndCreditsSectionV3Type.HTML)
            {
                items.Add(await OverlayHTMLHelper.ProcessSpecialIdentifiers(this.ItemTemplate, new CommandParametersModel()));
                return items;
            }
            else if (this.Type == OverlayEndCreditsSectionV3Type.PatreonMembers)
            {
                return await this.GetPatreonMemberItems();
            }
            else if (this.Type == OverlayEndCreditsSectionV3Type.CustomSection)
            {
                foreach (var item in this.customTracking.ToList())
                {
                    string text = await OverlayHTMLHelper.ProcessSpecialIdentifiers(this.ItemTemplate, new CommandParametersModel(item.Item1),
                        properties: new Dictionary<string, object> { [TextPropertyName] = item.Item2 });
                    items.Add(text);
                }
                return items;
            }

            foreach (var item in this.itemTracking.ToList())
            {
                Dictionary<string, object> replacements = new Dictionary<string, object> { [UsernamePropertyName] = item.Key.DisplayName };
                switch (this.Type)
                {
                    case OverlayEndCreditsSectionV3Type.Raids:
                    case OverlayEndCreditsSectionV3Type.Resubscribers:
                    case OverlayEndCreditsSectionV3Type.GiftedSubscriptions:
                    case OverlayEndCreditsSectionV3Type.TwitchBits:
                    case OverlayEndCreditsSectionV3Type.TwitchPowerUps:
                    case OverlayEndCreditsSectionV3Type.YouTubeSuperChats:
                    case OverlayEndCreditsSectionV3Type.KickKicks:
                    case OverlayEndCreditsSectionV3Type.VeloraCheered:
                    case OverlayEndCreditsSectionV3Type.Donations:
                    case OverlayEndCreditsSectionV3Type.ChannelPoints:
                    case OverlayEndCreditsSectionV3Type.YouTubeJewels:
                    case OverlayEndCreditsSectionV3Type.Streamloots:
                        replacements[AmountPropertyName] = item.Value.ToNumberDisplayString();
                        break;
                }

                string text = await OverlayHTMLHelper.ProcessSpecialIdentifiers(this.ItemTemplate, new CommandParametersModel(item.Key), properties: replacements);
                items.Add(text);
            }
            return items.OrderBy(i => i);
        }

        // Patreon keeps its campaign roster refreshed in the background, so this section reads the
        // current membership at roll time rather than tracking pledges that happened during the stream.
        private async Task<IEnumerable<string>> GetPatreonMemberItems()
        {
            List<string> items = new List<string>();

            PatreonService patreon = ServiceManager.Get<PatreonService>();
            if (patreon == null || !patreon.IsConnected)
            {
                return items;
            }

            foreach (PatreonCampaignMember member in patreon.CampaignMembers.ToList())
            {
                if (!string.Equals(member.PatronStatus, PatreonService.ActivePatronStatus, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string name = member.User?.PlatformUsername;
                if (string.IsNullOrEmpty(name))
                {
                    name = member.FullName;
                }
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                string text = await OverlayHTMLHelper.ProcessSpecialIdentifiers(this.ItemTemplate, new CommandParametersModel(),
                    properties: new Dictionary<string, object>
                    {
                        [UsernamePropertyName] = name,
                        [AmountPropertyName] = CurrencyHelper.ToCurrencyString(member.Amount)
                    });

                items.Add(text);
            }

            return items.OrderBy(i => i);
        }

        private bool ShouldTrack(UserV2ViewModel user)
        {
            return !user.IsSpecialtyExcluded;
        }
    }

    [DataContract]
    public class OverlayEndCreditsV3Model : OverlayEventTrackingV3ModelBase
    {
        public const string EndCreditsStartedPacketType = "EndCreditsStarted";
        public const string EndCreditsCompletedPacketType = "EndCreditsCompleted";

        private static readonly HashSet<OverlayEndCreditsSectionV3Type> AllChatterSectionTypes = new HashSet<OverlayEndCreditsSectionV3Type>()
        {
            OverlayEndCreditsSectionV3Type.Chatters,
            OverlayEndCreditsSectionV3Type.Followers,
            OverlayEndCreditsSectionV3Type.Subscribers,
            OverlayEndCreditsSectionV3Type.Moderators,
            OverlayEndCreditsSectionV3Type.VIPs,
            OverlayEndCreditsSectionV3Type.Regulars,
            OverlayEndCreditsSectionV3Type.FirstTimeChatters
        };

        private static readonly HashSet<UserRoleEnum> VIPRoles = new HashSet<UserRoleEnum>()
        {
            UserRoleEnum.TwitchVIP,
            UserRoleEnum.KickVIP,
            UserRoleEnum.KickOG,
            UserRoleEnum.VeloraVIP
        };

        private static readonly HashSet<OverlayEndCreditsSectionV3Type> AllSubscriberSectionTypes = new HashSet<OverlayEndCreditsSectionV3Type>()
        {
            OverlayEndCreditsSectionV3Type.Subscribers,
            OverlayEndCreditsSectionV3Type.NewSubscribers,
            OverlayEndCreditsSectionV3Type.GiftedSubscriptions,
            OverlayEndCreditsSectionV3Type.AllSubscriptions
        };

        public static readonly string DefaultHTML = OverlayResources.OverlayEndCreditsDefaultHTML;
        public static readonly string DefaultCSS =  OverlayResources.OverlayEndCreditsDefaultCSS + Environment.NewLine + Environment.NewLine + OverlayResources.OverlayTextDefaultCSS;
        public static readonly string DefaultJavascript = OverlayResources.OverlayEndCreditsDefaultJavascript;

        [DataMember]
        public OverlayEndCreditsHeaderV3Model Header { get; set; }

        [DataMember]
        public int ScrollSpeed { get; set; }
        [DataMember]
        public double ScrollRate { get; set; }
        [DataMember]
        public string BackgroundColor { get; set; }
        [DataMember]
        public bool RunCreditsWhenVisible { get; set; }
        [DataMember]
        public bool RunEndlessly { get; set; }
        [DataMember]
        public bool DontShowNoDataError { get; set; }

        [DataMember]
        public List<OverlayEndCreditsSectionV3Model> Sections { get; set; } = new List<OverlayEndCreditsSectionV3Model>();

        [DataMember]
        public Guid StartedCommandID { get; set; }
        [DataMember]
        public Guid EndedCommandID { get; set; }

        [DataMember]
        public override bool Chatters { get { return this.Sections.Any(s => OverlayEndCreditsV3Model.AllChatterSectionTypes.Contains(s.Type)); } set { } }

        [DataMember]
        public override bool Follows { get { return this.Sections.Any(s => s.Type == OverlayEndCreditsSectionV3Type.Followers || s.Type == OverlayEndCreditsSectionV3Type.NewFollowers); } set { } }

        [DataMember]
        public override bool Raids { get { return this.Sections.Any(s => s.Type == OverlayEndCreditsSectionV3Type.Raids); } set { } }

        [DataMember]
        public override bool TwitchSubscriptions { get { return this.Sections.Any(s => OverlayEndCreditsV3Model.AllSubscriberSectionTypes.Contains(s.Type)); } set { } }
        [DataMember]
        public override bool TwitchBits { get { return this.Sections.Any(s => s.Type == OverlayEndCreditsSectionV3Type.TwitchBits); } set { } }
        [DataMember]
        public override bool TwitchPowerUps { get { return this.Sections.Any(s => s.Type == OverlayEndCreditsSectionV3Type.TwitchPowerUps); } set { } }

        [DataMember]
        public override bool YouTubeMemberships { get { return this.Sections.Any(s => OverlayEndCreditsV3Model.AllSubscriberSectionTypes.Contains(s.Type)); } set { } }
        [DataMember]
        public override bool YouTubeSuperChats { get { return this.Sections.Any(s => s.Type == OverlayEndCreditsSectionV3Type.YouTubeSuperChats); } set { } }
        [DataMember]
        public override bool YouTubeJewels { get { return this.Sections.Any(s => s.Type == OverlayEndCreditsSectionV3Type.YouTubeJewels); } set { } }

        [DataMember]
        public override bool KickSubscriptions { get { return this.Sections.Any(s => OverlayEndCreditsV3Model.AllSubscriberSectionTypes.Contains(s.Type)); } set { } }
        [DataMember]
        public override bool KickKicks { get { return this.Sections.Any(s => s.Type == OverlayEndCreditsSectionV3Type.KickKicks); } set { } }

        [DataMember]
        public override bool VeloraSubscriptions { get { return this.Sections.Any(s => OverlayEndCreditsV3Model.AllSubscriberSectionTypes.Contains(s.Type)); } set { } }
        [DataMember]
        public override bool VeloraCheered { get { return this.Sections.Any(s => s.Type == OverlayEndCreditsSectionV3Type.VeloraCheered); } set { } }

        public override bool VPZoneSubscriptions { get { return this.Sections.Any(s => OverlayEndCreditsV3Model.AllSubscriberSectionTypes.Contains(s.Type)); } set { } }

        public override bool VPZoneCheered { get { return this.Sections.Any(s => s.Type == OverlayEndCreditsSectionV3Type.VPZoneCheered); } set { } }

        [DataMember]
        public override bool Donations { get { return this.Sections.Any(s => s.Type == OverlayEndCreditsSectionV3Type.Donations); } set { } }

        [DataMember]
        public override bool ChannelPoints { get { return this.Sections.Any(s => s.Type == OverlayEndCreditsSectionV3Type.ChannelPoints); } set { } }
        [DataMember]
        public override bool Streamloots { get { return this.Sections.Any(s => s.Type == OverlayEndCreditsSectionV3Type.Streamloots); } set { } }
        [DataMember]
        public override bool Patreon { get { return this.Sections.Any(s => s.Type == OverlayEndCreditsSectionV3Type.Patreon); } set { } }

        [JsonIgnore]
        public string AnimationIterations { get { return this.RunEndlessly ? "Infinity" : "1"; } }

        [JsonIgnore]
        public override bool IsResettable { get { return true; } }

        [JsonIgnore]
        public override bool JQuery { get { return true; } }

        public OverlayEndCreditsV3Model() : base(OverlayItemV3Type.EndCredits) { }

        public override async Task Initialize()
        {
            if (string.Equals(this.Javascript, OverlayResources.OverlayEndCreditsDefaultJavascriptOld, System.StringComparison.OrdinalIgnoreCase))
            {
                this.Javascript = OverlayResources.OverlayEndCreditsDefaultJavascript;
            }

            if (this.PositionType != OverlayPositionV3Type.Pixel || this.XPosition != 0 || this.YPosition != 0)
            {
                this.PositionType = OverlayPositionV3Type.Pixel;
                this.XPosition = 0;
                this.YPosition = 0;
            }

            await base.Initialize();

            foreach (OverlayEndCreditsSectionV3Model section in Sections)
            {
                section.ClearTracking();
            }
        }

        public override async Task Reset()
        {
            await base.Reset();

            foreach (OverlayEndCreditsSectionV3Model section in Sections)
            {
                section.ClearTracking();
            }
        }

        public override void OnChatUserBanned(object sender, UserV2ViewModel user)
        {
            foreach (OverlayEndCreditsSectionV3Model section in this.Sections)
            {
                section.Untrack(user);
            }
        }

        public override void OnChatMessageReceived(object sender, ChatMessageViewModel message)
        {
            foreach (OverlayEndCreditsSectionV3Model section in this.Sections)
            {
                if (section.Type == OverlayEndCreditsSectionV3Type.Chatters)
                {
                    section.Track(message.User);
                }

                if (section.Type == OverlayEndCreditsSectionV3Type.Followers)
                {
                    if (message.User.IsFollower)
                    {
                        section.Track(message.User);
                    }
                }

                if (section.Type == OverlayEndCreditsSectionV3Type.Subscribers)
                {
                    if (message.User.IsSubscriber)
                    {
                        section.Track(message.User);
                    }
                }

                if (section.Type == OverlayEndCreditsSectionV3Type.Moderators)
                {
                    if (message.User.HasRole(UserRoleEnum.Moderator))
                    {
                        section.Track(message.User);
                    }
                }

                if (section.Type == OverlayEndCreditsSectionV3Type.VIPs)
                {
                    if (OverlayEndCreditsV3Model.VIPRoles.Any(r => message.User.HasRole(r)))
                    {
                        section.Track(message.User);
                    }
                }

                if (section.Type == OverlayEndCreditsSectionV3Type.Regulars)
                {
                    if (message.User.HasRole(UserRoleEnum.Regular))
                    {
                        section.Track(message.User);
                    }
                }

                if (section.Type == OverlayEndCreditsSectionV3Type.FirstTimeChatters)
                {
                    if (message.User.TotalChatMessageSent == 1)
                    {
                        section.Track(message.User);
                    }
                }
            }
        }

        public override void OnFollow(object sender, UserV2ViewModel user)
        {
            foreach (OverlayEndCreditsSectionV3Model section in this.Sections)
            {
                switch (section.Type)
                {
                    case OverlayEndCreditsSectionV3Type.NewFollowers:
                        section.Track(user);
                        break;
                }
            }
        }

        public override void OnRaid(object sender, Tuple<UserV2ViewModel, int> raid)
        {
            foreach (OverlayEndCreditsSectionV3Model section in this.Sections)
            {
                switch (section.Type)
                {
                    case OverlayEndCreditsSectionV3Type.Raids:
                        section.Track(raid.Item1, raid.Item2);
                        break;
                }
            }
        }

        public override void OnSubscribe(object sender, SubscriptionDetailsModel subscription)
        {
            foreach (OverlayEndCreditsSectionV3Model section in this.Sections)
            {
                switch (section.Type)
                {
                    case OverlayEndCreditsSectionV3Type.NewSubscribers:
                        if (subscription.Months == 1)
                        {
                            section.Track(subscription.User);
                        }
                        break;
                    case OverlayEndCreditsSectionV3Type.Resubscribers:
                        if (subscription.Months > 1)
                        {
                            section.Track(subscription.User, subscription.Months);
                        }
                        break;
                    case OverlayEndCreditsSectionV3Type.GiftedSubscriptions:
                        if (subscription.Gifter != null)
                        {
                            section.Track(subscription.Gifter, 1);
                        }
                        break;
                    case OverlayEndCreditsSectionV3Type.AllSubscriptions:
                        section.Track(subscription.User);
                        break;
                }
            }
        }

        public override void OnMassSubscription(object sender, IEnumerable<SubscriptionDetailsModel> subscriptions)
        {
            foreach (OverlayEndCreditsSectionV3Model section in this.Sections)
            {
                switch (section.Type)
                {
                    case OverlayEndCreditsSectionV3Type.NewSubscribers:
                        foreach (var subscription in subscriptions)
                        {
                            section.Track(subscription.User);
                        }
                        break;
                    case OverlayEndCreditsSectionV3Type.GiftedSubscriptions:
                        foreach (var subscription in subscriptions)
                        {
                            if (subscription.Gifter != null)
                            {
                                section.Track(subscription.Gifter, 1);
                            }
                        }
                        break;
                    case OverlayEndCreditsSectionV3Type.AllSubscriptions:
                        foreach (var subscription in subscriptions)
                        {
                            section.Track(subscription.User);
                        }
                        break;
                }
            }
        }

        public override void OnDonation(object sender, UserDonationModel donation)
        {
            foreach (OverlayEndCreditsSectionV3Model section in this.Sections)
            {
                switch (section.Type)
                {
                    case OverlayEndCreditsSectionV3Type.Donations:
                        section.Track(donation.User, donation.Amount);
                        break;
                }
            }
        }

        public override void OnTwitchBits(object sender, TwitchBitsCheeredEventModel bitsCheered)
        {
            foreach (OverlayEndCreditsSectionV3Model section in this.Sections)
            {
                switch (section.Type)
                {
                    case OverlayEndCreditsSectionV3Type.TwitchBits:
                        section.Track(bitsCheered.User, bitsCheered.Amount);
                        break;
                }
            }
        }

        public override void OnTwitchPowerUp(object sender, UserV2ViewModel user)
        {
            foreach (OverlayEndCreditsSectionV3Model section in this.Sections)
            {
                switch (section.Type)
                {
                    case OverlayEndCreditsSectionV3Type.TwitchPowerUps:
                        section.Track(user, 1);
                        break;
                }
            }
        }

        public override void OnYouTubeSuperChat(object sender, YouTubeSuperChatViewModel superChat)
        {
            foreach (OverlayEndCreditsSectionV3Model section in this.Sections)
            {
                switch (section.Type)
                {
                    case OverlayEndCreditsSectionV3Type.YouTubeSuperChats:
                        section.Track(superChat.User, superChat.Amount);
                        break;
                }
            }
        }

        public override void OnKickKicksGifted(object sender, KickKicksGiftedEventModel kicksGifted)
        {
            foreach (OverlayEndCreditsSectionV3Model section in this.Sections)
            {
                switch (section.Type)
                {
                    case OverlayEndCreditsSectionV3Type.KickKicks:
                        section.Track(kicksGifted.User, kicksGifted.Amount);
                        break;
                }
            }
        }

        public override void OnVeloraCheered(object sender, VeloraCheeredEventModel cheered)
        {
            foreach (OverlayEndCreditsSectionV3Model section in this.Sections)
            {
                switch (section.Type)
                {
                    case OverlayEndCreditsSectionV3Type.VeloraCheered:
                        section.Track(cheered.User, cheered.Amount);
                        break;
                }
            }
        }

        public override void OnVPZoneCheered(object sender, VPZoneCheeredEventModel cheered)
        {
            foreach (OverlayEndCreditsSectionV3Model section in this.Sections)
            {
                switch (section.Type)
                {
                    case OverlayEndCreditsSectionV3Type.VPZoneCheered:
                        section.Track(cheered.User, cheered.Amount);
                        break;
                }
            }
        }

        public override void OnYouTubeJewelsGift(object sender, YouTubeJewelsGiftViewModel jewelsGift)
        {
            foreach (OverlayEndCreditsSectionV3Model section in this.Sections)
            {
                switch (section.Type)
                {
                    case OverlayEndCreditsSectionV3Type.YouTubeJewels:
                        section.Track(jewelsGift.User, jewelsGift.JewelsAmount);
                        break;
                }
            }
        }

        public override void OnChannelPointsRedeemed(object sender, Tuple<UserV2ViewModel, int> redemption)
        {
            foreach (OverlayEndCreditsSectionV3Model section in this.Sections)
            {
                switch (section.Type)
                {
                    case OverlayEndCreditsSectionV3Type.ChannelPoints:
                        section.Track(redemption.Item1, redemption.Item2);
                        break;
                }
            }
        }

        public override void OnStreamloots(object sender, Tuple<UserV2ViewModel, int> purchase)
        {
            foreach (OverlayEndCreditsSectionV3Model section in this.Sections)
            {
                switch (section.Type)
                {
                    case OverlayEndCreditsSectionV3Type.Streamloots:
                        section.Track(purchase.Item1, purchase.Item2);
                        break;
                }
            }
        }

        public override void OnPatreonSubscribed(object sender, UserV2ViewModel user)
        {
            foreach (OverlayEndCreditsSectionV3Model section in this.Sections)
            {
                switch (section.Type)
                {
                    case OverlayEndCreditsSectionV3Type.Patreon:
                        section.Track(user);
                        break;
                }
            }
        }

        public override Dictionary<string, object> GetGenerationProperties()
        {
            Dictionary<string, object> properties = base.GetGenerationProperties();

            foreach (var kvp in this.Header.GetGenerationProperties())
            {
                properties[kvp.Key] = kvp.Value;
            }

            properties[nameof(this.ScrollSpeed)] = this.ScrollSpeed.ToString();
            properties[nameof(this.ScrollRate)] = this.ScrollRate.ToString();

            properties[nameof(this.RunEndlessly)] = this.RunEndlessly.ToString().ToLower();
            properties[nameof(this.AnimationIterations)] = this.AnimationIterations;

            properties[nameof(this.BackgroundColor)] = this.BackgroundColor;
            properties[nameof(this.RunCreditsWhenVisible)] = this.RunCreditsWhenVisible.ToString().ToLower();

            List<string> sectionsHTML = new List<string>();
            foreach (OverlayEndCreditsSectionV3Model section in this.Sections)
            {
                string sectionHTML = section.HTML;
                foreach (var kvp in section.GetGenerationProperties())
                {
                    sectionHTML = OverlayV3Service.ReplaceProperty(sectionHTML, kvp.Key, kvp.Value);
                }
                sectionsHTML.Add(sectionHTML);
            }

            properties[nameof(this.Sections)] = string.Join(Environment.NewLine + Environment.NewLine, sectionsHTML);

            //properties[nameof(this.ProgressOccurredAnimation)] = this.ProgressOccurredAnimation.GenerateAnimationJavascript(OverlayItemV3ModelBase.MainDivElement);
            //properties[nameof(this.SegmentCompletedAnimation)] = this.SegmentCompletedAnimation.GenerateAnimationJavascript(OverlayItemV3ModelBase.MainDivElement);

            return properties;
        }

        public override async Task ProcessPacket(OverlayV3Packet packet)
        {
            await base.ProcessPacket(packet);

            if (string.Equals(packet.Type, OverlayEndCreditsV3Model.EndCreditsStartedPacketType))
            {
                await ServiceManager.Get<CommandService>().Queue(this.StartedCommandID);
            }
            else if (string.Equals(packet.Type, EndCreditsCompletedPacketType))
            {
                await ServiceManager.Get<CommandService>().Queue(this.EndedCommandID);
            }
        }

        public void CustomTrack(Guid sectionID, UserV2ViewModel user, string text)
        {
            foreach (OverlayEndCreditsSectionV3Model section in this.Sections)
            {
                if (section.ID.Equals(sectionID) && section.Type == OverlayEndCreditsSectionV3Type.CustomSection)
                {
                    section.Track(user, text);
                    return;
                }
            }
        }

        public async Task PlayCredits()
        {
            if (this.IsLivePreview)
            {
                await OverlayEndCreditsV3ViewModel.LoadTestData(this);
            }

            List<OverlayEndCreditsSectionV3Model> applicableSections = new List<OverlayEndCreditsSectionV3Model>();

            Dictionary<Guid, IEnumerable<string>> sectionItems = new Dictionary<Guid, IEnumerable<string>>();
            foreach (OverlayEndCreditsSectionV3Model section in this.Sections)
            {
                IEnumerable<string> items = await section.GetItems();
                if (items.Count() > 0)
                {
                    sectionItems[section.ID] = items;
                    applicableSections.Add(section);
                }
            }

            if (applicableSections.Count > 0)
            {
                Dictionary<string, object> data = new Dictionary<string, object>();
                data["Order"] = applicableSections.Select(s => s.ID);
                data["Columns"] = applicableSections.ToDictionary(s => s.ID, s => s.Columns);
                data["Types"] = applicableSections.ToDictionary(s => s.ID, s => s.Type.ToString());
                data["Items"] = sectionItems;

                await this.CallFunction("startCredits", data);
            }
            else
            {
                if (!this.DontShowNoDataError)
                {
                    await ServiceManager.Get<ChatService>().SendMessage(Resources.OverlayWidgetEndCreditsNoDataCurrentlyAvailable);
                }
            }
        }

        protected override async Task Loaded()
        {
            if (this.RunCreditsWhenVisible)
            {
                await this.PlayCredits();
            }
        }
    }
}

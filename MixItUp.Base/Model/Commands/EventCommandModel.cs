using MixItUp.Base.Model.User;
using MixItUp.Base.Services;
using MixItUp.Base.Util;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Base.Model.Commands
{
    [DataContract]
    public class EventCommandModel : CommandModelBase
    {
        private const string genericImage = "https://static-cdn.jtvnw.net/jtv_user_pictures/12caba55-1276-49b7-a8bc-88b960ecb5da-profile_image-70x70.png";

        private static SemaphoreSlim followEventsInQueueSemaphore = new SemaphoreSlim(1);
        public static int FollowEventsInQueue = 0;

        public static Dictionary<string, string> GetEventTestSpecialIdentifiers(EventTypeEnum eventType)
        {
            Dictionary<string, string> specialIdentifiers = CommandModelBase.GetGeneralTestSpecialIdentifiers();
            switch (eventType)
            {
                // Generic
                case EventTypeEnum.ChannelRaided:
                    specialIdentifiers["raidviewercount"] = "123";
                    break;
                case EventTypeEnum.ChannelSubscribed:
                    specialIdentifiers["message"] = "Test Message";
                    specialIdentifiers["usersubplanname"] = "Plan Name";
                    specialIdentifiers["usersubplan"] = "Tier 1";
                    break;
                case EventTypeEnum.ChannelResubscribed:
                    specialIdentifiers["message"] = "Test Message";
                    specialIdentifiers["usersubplanname"] = "Plan Name";
                    specialIdentifiers["usersubplan"] = "Tier 1";
                    specialIdentifiers["usersubmonths"] = "5";
                    specialIdentifiers["usersubstreak"] = "3";
                    break;
                case EventTypeEnum.ChannelSubscriptionGifted:
                    specialIdentifiers["usersubplanname"] = "Plan Name";
                    specialIdentifiers["usersubplan"] = "Tier 1";
                    specialIdentifiers["usersubmonthsgifted"] = "3";
                    specialIdentifiers["isanonymous"] = "false";
                    break;
                case EventTypeEnum.ChannelMassSubscriptionsGifted:
                    specialIdentifiers["subsgiftedamount"] = "5";
                    specialIdentifiers["subsgiftedlifetimeamount"] = "100";
                    specialIdentifiers["usersubplan"] = "Tier 1";
                    specialIdentifiers["isanonymous"] = "false";
                    break;

                // Twitch
                case EventTypeEnum.TwitchChannelRaided:
                case EventTypeEnum.TwitchChannelOutgoingRaidCompleted:
                    specialIdentifiers["hostviewercount"] = "123";
                    specialIdentifiers["raidviewercount"] = "123";
                    break;
                case EventTypeEnum.TwitchChannelSubscribed:
                    specialIdentifiers["message"] = "Test Message";
                    specialIdentifiers["usersubplanname"] = "Plan Name";
                    specialIdentifiers["usersubplan"] = "Tier 1";
                    specialIdentifiers["usersubpoints"] = "1";
                    specialIdentifiers["usersubdurationmonths"] = "3";
                    specialIdentifiers["isprimeupgrade"] = "False";
                    specialIdentifiers["isgiftupgrade"] = "False";
                    break;
                case EventTypeEnum.TwitchChannelResubscribed:
                    specialIdentifiers["message"] = "Test Message";
                    specialIdentifiers["usersubplanname"] = "Plan Name";
                    specialIdentifiers["usersubplan"] = "Tier 1";
                    specialIdentifiers["usersubpoints"] = "1";
                    specialIdentifiers["usersubmonths"] = "5";
                    specialIdentifiers["usersubstreak"] = "3";
                    specialIdentifiers["usersubdurationmonths"] = "3";
                    break;
                case EventTypeEnum.TwitchChannelSubscriptionGifted:
                    specialIdentifiers["usersubplanname"] = "Plan Name";
                    specialIdentifiers["usersubplan"] = "Tier 1";
                    specialIdentifiers["usersubpoints"] = "1";
                    specialIdentifiers["usersubmonthsgifted"] = "3";
                    specialIdentifiers["isanonymous"] = "false";
                    break;
                case EventTypeEnum.TwitchChannelMassSubscriptionsGifted:
                    specialIdentifiers["subsgiftedamount"] = "5";
                    specialIdentifiers["substotalpoints"] = "5";
                    specialIdentifiers["subsgiftedlifetimeamount"] = "100";
                    specialIdentifiers["usersubplan"] = "Tier 1";
                    specialIdentifiers["isanonymous"] = "false";
                    break;
                case EventTypeEnum.TwitchChannelWatchStreak:
                    specialIdentifiers["userwatchstreak"] = "5";
                    specialIdentifiers["watchstreakchannelpointsawarded"] = "100";
                    specialIdentifiers["message"] = "Test Message";
                    break;
                case EventTypeEnum.TwitchChannelModiversary:
                    specialIdentifiers["usermodiversarymonths"] = "6";
                    specialIdentifiers["message"] = "Test Message";
                    break;

                case EventTypeEnum.TwitchChannelHighlightedMessage:
                    specialIdentifiers["message"] = "Test Message";
                    break;
                case EventTypeEnum.TwitchChannelUserIntro:
                    specialIdentifiers["message"] = "Test Message";
                    break;
                case EventTypeEnum.TwitchChannelPowerUpMessageEffect:
                    specialIdentifiers["message"] = "Test Message";
                    break;
                case EventTypeEnum.TwitchChannelPowerUpGigantifiedEmote:
                    specialIdentifiers["message"] = "LUL";
                    specialIdentifiers["emotename"] = "LUL";
                    specialIdentifiers["emoteurl"] = "https://static-cdn.jtvnw.net/emoticons/v2/425618/default/dark/4.0";
                    break;

                case EventTypeEnum.TwitchChannelBitsCheered:
                    specialIdentifiers["bitsamount"] = "10";
                    specialIdentifiers["messagenocheermotes"] = "Test Message";
                    specialIdentifiers["message"] = "Test Message";
                    specialIdentifiers["isanonymous"] = "false";
                    break;
                case EventTypeEnum.TwitchChannelPointsRedeemed:
                    specialIdentifiers["rewardname"] = "Test Reward";
                    specialIdentifiers["rewardcost"] = "100";
                    specialIdentifiers["message"] = "Test Message";
                    break;
                case EventTypeEnum.TwitchChannelCustomPowerUpRedeemed:
                    specialIdentifiers["powerupname"] = "Test Power-Up";
                    specialIdentifiers["powerupcost"] = "100";
                    specialIdentifiers["message"] = "Test Message";
                    break;
                case EventTypeEnum.TwitchChannelAdUpcoming:
                    specialIdentifiers["adsnoozecount"] = "3";
                    specialIdentifiers["adnextduration"] = "60";
                    specialIdentifiers["adnextminutes"] = "30";
                    specialIdentifiers["adnexttime"] = DateTimeOffset.Now.ToFriendlyTimeString();
                    break;
                case EventTypeEnum.TwitchChannelAdStarted:
                case EventTypeEnum.TwitchChannelAdEnded:
                    specialIdentifiers["adduration"] = "60";
                    specialIdentifiers["adisautomatic"] = "true";
                    break;
                case EventTypeEnum.TwitchChannelHypeTrainBegin:
                    specialIdentifiers["hypetraintype"] = "regular";
                    specialIdentifiers["hypetrainisgoldenkappa"] = "false";
                    specialIdentifiers["hypetrainistreasure"] = "false";
                    specialIdentifiers["hypetrainisshared"] = "false";
                    specialIdentifiers["hypetraintotalpoints"] = "1";
                    specialIdentifiers["hypetrainlevelpoints"] = "123";
                    specialIdentifiers["hypetrainlevelgoal"] = "500";
                    break;
                case EventTypeEnum.TwitchChannelHypeTrainProgress:
                    specialIdentifiers["hypetraintype"] = "regular";
                    specialIdentifiers["hypetrainisgoldenkappa"] = "false";
                    specialIdentifiers["hypetrainistreasure"] = "false";
                    specialIdentifiers["hypetrainisshared"] = "false";
                    specialIdentifiers["hypetraintotalpoints"] = "1";
                    specialIdentifiers["hypetrainlevelpoints"] = "123";
                    specialIdentifiers["hypetrainlevelgoal"] = "500";
                    specialIdentifiers["hypetrainlevel"] = "2";
                    break;
                case EventTypeEnum.TwitchChannelHypeTrainLevelUp:
                    specialIdentifiers["hypetraintype"] = "regular";
                    specialIdentifiers["hypetrainisgoldenkappa"] = "false";
                    specialIdentifiers["hypetrainistreasure"] = "false";
                    specialIdentifiers["hypetrainisshared"] = "false";
                    specialIdentifiers["hypetraintotalpoints"] = "1";
                    specialIdentifiers["hypetrainlevelpoints"] = "123";
                    specialIdentifiers["hypetrainlevelgoal"] = "500";
                    specialIdentifiers["hypetrainlevel"] = "2";
                    break;
                case EventTypeEnum.TwitchChannelHypeTrainEnd:
                    specialIdentifiers["hypetraintype"] = "regular";
                    specialIdentifiers["hypetrainisgoldenkappa"] = "false";
                    specialIdentifiers["hypetrainistreasure"] = "false";
                    specialIdentifiers["hypetrainisshared"] = "false";
                    specialIdentifiers["hypetraintotallevel"] = "5";
                    specialIdentifiers["hypetraintotalpoints"] = "1234";
                    break;

                case EventTypeEnum.TwitchChannelSharedChatStarted:
                case EventTypeEnum.TwitchChannelSharedChatEnded:
                    specialIdentifiers["sharedchatsessionid"] = Guid.NewGuid().ToString();
                    specialIdentifiers["sharedchathostid"] = "12345";
                    specialIdentifiers["sharedchathost"] = "examplehost";
                    break;
                case EventTypeEnum.TwitchChannelUserWarned:
                    specialIdentifiers["warnreason"] = "Be nice in chat";
                    break;
                case EventTypeEnum.TwitchChannelShoutoutReceived:
                    specialIdentifiers["shoutoutviewercount"] = "50";
                    break;
                case EventTypeEnum.TwitchChannelSuspiciousUserMessage:
                    specialIdentifiers["message"] = "Test Message";
                    specialIdentifiers["suspicioususertypes"] = "ban_evader";
                    specialIdentifiers["suspicioususerstatus"] = "active_monitoring";
                    specialIdentifiers["suspicioususerbanevasion"] = "likely";
                    break;
                case EventTypeEnum.TwitchChannelSuspiciousUserUpdated:
                    specialIdentifiers["suspicioususerstatus"] = "restricted";
                    break;
                case EventTypeEnum.TwitchChannelUnbanRequestCreated:
                    specialIdentifiers["message"] = "Test Message";
                    break;
                case EventTypeEnum.TwitchChannelUnbanRequestResolved:
                    specialIdentifiers["unbanrequeststatus"] = "approved";
                    specialIdentifiers["message"] = "Test Message";
                    break;
                case EventTypeEnum.TwitchChannelGoalStarted:
                    specialIdentifiers["goaltype"] = "follower";
                    specialIdentifiers["goaldescription"] = "Follow goal for the stream";
                    specialIdentifiers["goalcurrentamount"] = "50";
                    specialIdentifiers["goaltargetamount"] = "100";
                    break;
                case EventTypeEnum.TwitchChannelGoalProgress:
                    specialIdentifiers["goaltype"] = "follower";
                    specialIdentifiers["goaldescription"] = "Follow goal for the stream";
                    specialIdentifiers["goalcurrentamount"] = "75";
                    specialIdentifiers["goaltargetamount"] = "100";
                    break;
                case EventTypeEnum.TwitchChannelGoalEnded:
                    specialIdentifiers["goaltype"] = "follower";
                    specialIdentifiers["goaldescription"] = "Follow goal for the stream";
                    specialIdentifiers["goalcurrentamount"] = "100";
                    specialIdentifiers["goaltargetamount"] = "100";
                    specialIdentifiers["goalachieved"] = "True";
                    break;

                // YouTube
                case EventTypeEnum.YouTubeChannelNewMember:
                    specialIdentifiers["usersubplan"] = "Plan Name";
                    break;
                case EventTypeEnum.YouTubeChannelMemberMilestone:
                    specialIdentifiers["message"] = "Test Message";
                    specialIdentifiers["usersubmonths"] = "5";
                    specialIdentifiers["usersubplan"] = "Plan Name";
                    break;
                case EventTypeEnum.YouTubeChannelMembershipGifted:
                    specialIdentifiers["usersubplan"] = "Plan Name";
                    break;
                case EventTypeEnum.YouTubeChannelMassMembershipGifted:
                    specialIdentifiers["subsgiftedamount"] = "5";
                    specialIdentifiers["usersubplan"] = "Plan Name";
                    break;
                case EventTypeEnum.YouTubeChannelSuperChat:
                    specialIdentifiers["amountnumberdigits"] = "123";
                    specialIdentifiers["amountnumber"] = "1.23";
                    specialIdentifiers["amount"] = "$1.23";
                    specialIdentifiers["tier"] = "1";
                    specialIdentifiers["message"] = "Test Message";
                    specialIdentifiers["currencytype"] = "USD";
                    break;
                case EventTypeEnum.YouTubeChannelJewelsGift:
                    specialIdentifiers["message"] = "Test Message";
                    specialIdentifiers["jewelsamount"] = "100";
                    specialIdentifiers["giftname"] = "Gift Name";
                    specialIdentifiers["giftimageurl"] = "https://www.gstatic.com/youtube/img/pdg/gift/assets/love_is_in_the_air.png";
                    specialIdentifiers["giftdurationseconds"] = "5";
                    specialIdentifiers["gifthasvisualeffect"] = "true";
                    specialIdentifiers["giftcombocount"] = "3";
                    break;

                // Kick
                case EventTypeEnum.KickChannelSubscribed:
                    specialIdentifiers["usersubmonths"] = "1";
                    specialIdentifiers["usersubtier"] = "1";
                    specialIdentifiers["usersubplan"] = "Tier 1";
                    specialIdentifiers["usersubplanname"] = "Tier 1";
                    break;
                case EventTypeEnum.KickChannelResubscribed:
                    specialIdentifiers["usersubmonths"] = "5";
                    specialIdentifiers["usersubstreak"] = "5";
                    specialIdentifiers["usersubtier"] = "1";
                    specialIdentifiers["usersubplan"] = "Tier 1";
                    specialIdentifiers["usersubplanname"] = "Tier 1";
                    break;
                case EventTypeEnum.KickChannelSubscriptionGifted:
                    specialIdentifiers["isanonymous"] = "false";
                    specialIdentifiers["usersubtier"] = "1";
                    specialIdentifiers["usersubplan"] = "Tier 1";
                    specialIdentifiers["usersubplanname"] = "Tier 1";
                    break;
                case EventTypeEnum.KickChannelMassSubscriptionsGifted:
                    specialIdentifiers["subsgiftedamount"] = "5";
                    specialIdentifiers["subsgiftedlifetimeamount"] = "100";
                    specialIdentifiers["isanonymous"] = "false";
                    specialIdentifiers["usersubtier"] = "1";
                    specialIdentifiers["usersubplan"] = "Tier 1";
                    specialIdentifiers["usersubplanname"] = "Tier 1";
                    break;
                case EventTypeEnum.KickChannelPointsRedeemed:
                    specialIdentifiers["rewardname"] = "Hydrate";
                    specialIdentifiers["rewardcost"] = "5";
                    specialIdentifiers["message"] = "Test Message";
                    break;
                case EventTypeEnum.KickChannelKicksGifted:
                    specialIdentifiers["kicksamount"] = "100";
                    specialIdentifiers["giftname"] = "Full Send";
                    specialIdentifiers["gifttype"] = "BASIC";
                    specialIdentifiers["gifttier"] = "BASIC";
                    specialIdentifiers["message"] = "";
                    specialIdentifiers["giftpinnedseconds"] = "0";
                    break;

                // Velora
                case EventTypeEnum.VeloraChannelSubscribed:
                    specialIdentifiers["usersubmonths"] = "1";
                    specialIdentifiers["usersubtier"] = "1";
                    specialIdentifiers["usersubplan"] = "Tier 1";
                    specialIdentifiers["usersubplanname"] = "Tier 1";
                    break;
                case EventTypeEnum.VeloraChannelResubscribed:
                    specialIdentifiers["usersubmonths"] = "5";
                    specialIdentifiers["usersubstreak"] = "5";
                    specialIdentifiers["usersubtier"] = "1";
                    specialIdentifiers["usersubplan"] = "Tier 1";
                    specialIdentifiers["usersubplanname"] = "Tier 1";
                    break;
                case EventTypeEnum.VeloraChannelSubscriptionGifted:
                    specialIdentifiers["isanonymous"] = "false";
                    specialIdentifiers["usersubtier"] = "1";
                    specialIdentifiers["usersubplan"] = "Tier 1";
                    specialIdentifiers["usersubplanname"] = "Tier 1";
                    break;
                case EventTypeEnum.VeloraChannelMassSubscriptionsGifted:
                    specialIdentifiers["subsgiftedamount"] = "5";
                    specialIdentifiers["subsgiftedlifetimeamount"] = "100";
                    specialIdentifiers["isanonymous"] = "false";
                    specialIdentifiers["usersubtier"] = "1";
                    specialIdentifiers["usersubplan"] = "Tier 1";
                    specialIdentifiers["usersubplanname"] = "Tier 1";
                    break;
                case EventTypeEnum.VeloraChannelRaided:
                    specialIdentifiers["hostviewercount"] = "123";
                    specialIdentifiers["raidviewercount"] = "123";
                    break;
                case EventTypeEnum.VeloraChannelPointsRedeemed:
                    specialIdentifiers["rewardname"] = "Hydrate";
                    specialIdentifiers["rewardcost"] = "5";
                    specialIdentifiers["message"] = "Test Message";
                    break;
                case EventTypeEnum.VeloraChannelCheered:
                    specialIdentifiers["cheeramount"] = "100";
                    specialIdentifiers["voltsamount"] = "100";
                    specialIdentifiers["message"] = "Test Message";
                    break;

                // VPZone
                case EventTypeEnum.VPZoneChannelSubscribed:
                    specialIdentifiers["usersubmonths"] = "1";
                    specialIdentifiers["usersubtier"] = "1";
                    specialIdentifiers["usersubplan"] = "Tier 1";
                    specialIdentifiers["usersubplanname"] = "Tier 1";
                    break;
                case EventTypeEnum.VPZoneChannelResubscribed:
                    specialIdentifiers["usersubmonths"] = "5";
                    specialIdentifiers["usersubstreak"] = "5";
                    specialIdentifiers["usersubtier"] = "1";
                    specialIdentifiers["usersubplan"] = "Tier 1";
                    specialIdentifiers["usersubplanname"] = "Tier 1";
                    break;
                case EventTypeEnum.VPZoneChannelSubscriptionGifted:
                    specialIdentifiers["isanonymous"] = "false";
                    specialIdentifiers["usersubtier"] = "1";
                    specialIdentifiers["usersubplan"] = "Tier 1";
                    specialIdentifiers["usersubplanname"] = "Tier 1";
                    break;
                case EventTypeEnum.VPZoneChannelMassSubscriptionsGifted:
                    specialIdentifiers["subsgiftedamount"] = "5";
                    specialIdentifiers["subsgiftedlifetimeamount"] = "100";
                    specialIdentifiers["isanonymous"] = "false";
                    specialIdentifiers["usersubtier"] = "1";
                    specialIdentifiers["usersubplan"] = "Tier 1";
                    specialIdentifiers["usersubplanname"] = "Tier 1";
                    break;
                case EventTypeEnum.VPZoneChannelRaided:
                    specialIdentifiers["hostviewercount"] = "123";
                    specialIdentifiers["raidviewercount"] = "123";
                    break;
                case EventTypeEnum.VPZoneChannelPointsRedeemed:
                    specialIdentifiers["rewardname"] = "Hydrate";
                    specialIdentifiers["rewardcost"] = "5";
                    specialIdentifiers["message"] = "Test Message";
                    break;
                case EventTypeEnum.VPZoneChannelCheered:
                    specialIdentifiers["cheeramount"] = "100";
                    specialIdentifiers["pixelsamount"] = "100";
                    specialIdentifiers["message"] = "Test Message";
                    break;
                case EventTypeEnum.VPZoneChannelClipCreated:
                    specialIdentifiers["cliptitle"] = "Test Clip";
                    specialIdentifiers["clipurl"] = "https://vpzone.tv/clips/test";
                    specialIdentifiers["clipid"] = "00000000-0000-0000-0000-000000000000";
                    break;
                case EventTypeEnum.VPZoneChannelShoutout:
                    specialIdentifiers["shoutouttarget"] = "TestChannel";
                    break;

                // Chat
                case EventTypeEnum.ChatUserBan:
                    specialIdentifiers["banreason"] = "Be nice in chat";
                    break;
                case EventTypeEnum.ChatUserTimeout:
                    specialIdentifiers["timeoutlength"] = "300";
                    break;

                // Donation
                case EventTypeEnum.GenericDonation:
                case EventTypeEnum.StreamlabsDonation:
                case EventTypeEnum.TiltifyDonation:
                case EventTypeEnum.DonorDriveDonation:
                case EventTypeEnum.DonorDriveDonationIncentive:
                case EventTypeEnum.DonorDriveDonationTeamIncentive:
                case EventTypeEnum.TipeeeStreamDonation:
                case EventTypeEnum.TreatStreamDonation:
                case EventTypeEnum.RainmakerDonation:
                case EventTypeEnum.JustGivingDonation:
                case EventTypeEnum.StreamElementsDonation:
                case EventTypeEnum.TwitchChannelCharityDonation:
                case EventTypeEnum.FourthwallDonation:
                case EventTypeEnum.FourthwallOrderPlaced:
                case EventTypeEnum.FourthwallGiftPurchase:
                case EventTypeEnum.ThroneGiftPurchased:
                case EventTypeEnum.ThroneContribution:
                case EventTypeEnum.ThroneGiftCrowdfunded:
                case EventTypeEnum.KoFiTip:
                case EventTypeEnum.KoFiCommission:
                case EventTypeEnum.KoFiMembership:
                case EventTypeEnum.KoFiFirstMembership:
                case EventTypeEnum.KoFiShopOrder:
                case EventTypeEnum.PallyDonation:
                    UserDonationModel donation = new UserDonationModel()
                    {
                        Amount = 12.34,
                        Message = "Test donation message",
                        ImageLink = genericImage
                    };

                    switch (eventType)
                    {
                        case EventTypeEnum.StreamlabsDonation: donation.Source = UserDonationSourceEnum.Streamlabs; break;
                        case EventTypeEnum.TiltifyDonation: donation.Source = UserDonationSourceEnum.Tiltify; break;
                        case EventTypeEnum.DonorDriveDonation:
                        case EventTypeEnum.DonorDriveDonationIncentive:
                        case EventTypeEnum.DonorDriveDonationMilestone:
                            donation.Source = UserDonationSourceEnum.DonorDrive; break;
                        case EventTypeEnum.TipeeeStreamDonation: donation.Source = UserDonationSourceEnum.TipeeeStream; break;
                        case EventTypeEnum.TreatStreamDonation: donation.Source = UserDonationSourceEnum.TreatStream; break;
                        case EventTypeEnum.RainmakerDonation: donation.Source = UserDonationSourceEnum.Rainmaker; break;
                        case EventTypeEnum.JustGivingDonation: donation.Source = UserDonationSourceEnum.JustGiving; break;
                        case EventTypeEnum.StreamElementsDonation: donation.Source = UserDonationSourceEnum.StreamElements; break;
                        case EventTypeEnum.TwitchChannelCharityDonation: donation.Source = UserDonationSourceEnum.Twitch; break;
                        case EventTypeEnum.FourthwallDonation:
                        case EventTypeEnum.FourthwallOrderPlaced:
                        case EventTypeEnum.FourthwallGiftPurchase:
                            donation.Source = UserDonationSourceEnum.Fourthwall; break;
                        case EventTypeEnum.ThroneGiftPurchased:
                        case EventTypeEnum.ThroneContribution:
                        case EventTypeEnum.ThroneGiftCrowdfunded:
                            donation.Source = UserDonationSourceEnum.Throne; break;
                        case EventTypeEnum.KoFiTip:
                        case EventTypeEnum.KoFiCommission:
                        case EventTypeEnum.KoFiMembership:
                        case EventTypeEnum.KoFiFirstMembership:
                        case EventTypeEnum.KoFiShopOrder:
                            donation.Source = UserDonationSourceEnum.KoFi; break;
                        case EventTypeEnum.PallyDonation:
                            donation.Source = UserDonationSourceEnum.Pally; break;
                    }

                    foreach (var kvp in donation.GetSpecialIdentifiers())
                    {
                        specialIdentifiers[kvp.Key] = kvp.Value;
                    }

                    if (eventType == EventTypeEnum.TreatStreamDonation)
                    {
                        specialIdentifiers["donationtype"] = "Pizza";
                    }

                    if (eventType == EventTypeEnum.TwitchChannelCharityDonation)
                    {
                        specialIdentifiers["charityname"] = "Charity Name";
                        specialIdentifiers["charityimage"] = genericImage;
                    }

                    if (eventType == EventTypeEnum.DonorDriveDonationIncentive)
                    {
                        specialIdentifiers["donordriveincentivedescription"] = "Incentive Description";
                    }

                    if (eventType == EventTypeEnum.TiltifyDonation)
                    {
                        specialIdentifiers["tiltifyrewardid"] = "0b696369-5957-451f-b147-e200d48243d3";
                        specialIdentifiers["tiltifyrewardname"] = "Reward Name";
                        specialIdentifiers["tiltifyrewarddescription"] = "Reward Description";
                        specialIdentifiers["tiltifyrewardamount"] = "12.34";
                    }

                    if (eventType == EventTypeEnum.FourthwallDonation)
                    {
                        specialIdentifiers["donationtype"] = "DONATION";
                        specialIdentifiers["fourthwallitemname"] = "Test Item";
                    }

                    if (eventType == EventTypeEnum.FourthwallOrderPlaced)
                    {
                        specialIdentifiers["donationtype"] = "ORDER_PLACED";
                        specialIdentifiers["fourthwallitemname"] = "Test Item";
                    }

                    if (eventType == EventTypeEnum.FourthwallGiftPurchase)
                    {
                        specialIdentifiers["donationtype"] = "GIFT_PURCHASE";
                        specialIdentifiers["fourthwallitemname"] = "Test Item";
                    }

                    if (eventType == EventTypeEnum.ThroneGiftPurchased)
                    {
                        specialIdentifiers["donationtype"] = "gift_purchased";
                        specialIdentifiers["throneitemname"] = "Test Item";
                    }

                    if (eventType == EventTypeEnum.ThroneContribution)
                    {
                        specialIdentifiers["donationtype"] = "contribution_purchased";
                        specialIdentifiers["throneitemname"] = "Test Item";
                    }

                    if (eventType == EventTypeEnum.ThroneGiftCrowdfunded)
                    {
                        specialIdentifiers["donationtype"] = "gift_crowdfunded";
                        specialIdentifiers["throneitemname"] = "Test Item";
                    }

                    if (eventType == EventTypeEnum.KoFiTip)
                    {
                        specialIdentifiers["donationtype"] = "Tip";
                    }

                    if (eventType == EventTypeEnum.KoFiCommission)
                    {
                        specialIdentifiers["donationtype"] = "Commission";
                    }

                    if (eventType == EventTypeEnum.KoFiMembership || eventType == EventTypeEnum.KoFiFirstMembership)
                    {
                        specialIdentifiers["donationtype"] = "Subscription";
                        specialIdentifiers["kofitiername"] = "Test Tier";
                    }

                    if (eventType == EventTypeEnum.KoFiShopOrder)
                    {
                        specialIdentifiers["donationtype"] = "Shop Order";
                        specialIdentifiers["kofishopitemname"] = "Test Item x1";
                    }

                    if (eventType == EventTypeEnum.PallyDonation)
                    {
                        specialIdentifiers["pallypageslug"] = "test-page";
                        specialIdentifiers["pallypagetitle"] = "Test Page";
                    }
                    break;
                case EventTypeEnum.DonorDriveDonationMilestone:
                case EventTypeEnum.DonorDriveDonationTeamMilestone:
                    specialIdentifiers["donordrivemilestonedescription"] = "Milestone Description";
                    specialIdentifiers["donordrivemilestoneamountnumber"] = "12.34";
                    specialIdentifiers["donordrivemilestoneamount"] = "$12.34";
                    break;
                case EventTypeEnum.PatreonSubscribed:
                    specialIdentifiers[SpecialIdentifierStringBuilder.PatreonTierNameSpecialIdentifier] = "Super Tier";
                    specialIdentifiers[SpecialIdentifierStringBuilder.PatreonTierAmountSpecialIdentifier] = "12.34";
                    specialIdentifiers[SpecialIdentifierStringBuilder.PatreonTierImageSpecialIdentifier] = genericImage;
                    break;

                // Streamloots
                case EventTypeEnum.StreamlootsCardRedeemed:
                    specialIdentifiers["streamlootscardname"] = "Test Card";
                    specialIdentifiers["streamlootscarddescription"] = "Test Description";
                    specialIdentifiers["streamlootscardimage"] = "https://res.cloudinary.com/streamloots/image/upload/f_auto,c_scale,w_250,q_90/static/e19c7bf6-ca3e-49a8-807e-b2e9a1a47524/en_dl_character.png";
                    specialIdentifiers["streamlootscardvideo"] = "https://cdn.streamloots.com/uploads/5c645b78666f31002f2979d1/3a6bf1dc-7d61-4f93-be0a-f5dc1d0d33b6.webm";
                    specialIdentifiers["streamlootscardsound"] = "https://static.streamloots.com/b355d1ef-d931-4c16-a48f-8bed0076401b/alerts/default.mp3";
                    specialIdentifiers["streamlootscardalertmessage"] = "This is an alert message";
                    specialIdentifiers["streamlootsmessage"] = "Test Message";
                    break;
                case EventTypeEnum.StreamlootsPackPurchased:
                case EventTypeEnum.StreamlootsPackGifted:
                case EventTypeEnum.StreamlootsPackCommunityGifted:
                    specialIdentifiers["streamlootspurchasequantity"] = "1";
                    break;
                case EventTypeEnum.CrowdControlEffectRedeemed:
                    foreach (var kvp in CrowdControlEffectCommandModel.GetEffectTestSpecialIdentifiers())
                    {
                        specialIdentifiers[kvp.Key] = kvp.Value;
                    }
                    break;
                case EventTypeEnum.PulsoidHeartRateChanged:
                    specialIdentifiers["pulsoidheartrate"] = "80";
                    break;
                case EventTypeEnum.VeadotubeAvatarStateChanged:
                    specialIdentifiers["veadotubestateid"] = "surprised";
                    specialIdentifiers["veadotubestatename"] = "surprised";
                    specialIdentifiers["veadotubepreviousstateid"] = "idle";
                    specialIdentifiers["veadotubepreviousstatename"] = "idle";
                    break;
                case EventTypeEnum.VeadotubePushToTalkChanged:
                    specialIdentifiers["veadotubepushtotalk"] = "True";
                    break;

                // VConnect
                case EventTypeEnum.VConnectTriggerActivated:
                    specialIdentifiers["vconnecttriggeruid"] = "a0ce3831-3c3e-4474-b4e4-19ab98dc73dd";
                    specialIdentifiers["vconnecttriggername"] = "New Follow";
                    break;
                case EventTypeEnum.VConnectTriggerEnded:
                    specialIdentifiers["vconnecttriggeruid"] = "a0ce3831-3c3e-4474-b4e4-19ab98dc73dd";
                    specialIdentifiers["vconnecttriggername"] = "New Follow";
                    specialIdentifiers["vconnecttriggersuccess"] = "True";
                    specialIdentifiers["vconnecttriggererror"] = string.Empty;
                    break;
                case EventTypeEnum.VConnectAssetSpawned:
                case EventTypeEnum.VConnectAssetDespawned:
                    specialIdentifiers["vconnectassetuid"] = "d1a7f8d2-1e2f-4b3c-9a4d-5e6f7a8b9c0d";
                    specialIdentifiers["vconnectassetname"] = "banana";
                    specialIdentifiers["vconnecttriggeruid"] = "ebb7170b-40e6-4832-8b05-bc6c809d79f1";
                    specialIdentifiers["vconnecttriggernodeuid"] = "3fa2c1d4-5b6e-4f70-8192-a3b4c5d6e7f8";
                    break;
                case EventTypeEnum.VConnectAssetHit:
                    specialIdentifiers["vconnectassetuid"] = "d1a7f8d2-1e2f-4b3c-9a4d-5e6f7a8b9c0d";
                    specialIdentifiers["vconnectassetname"] = "banana";
                    specialIdentifiers["vconnecttriggeruid"] = "ebb7170b-40e6-4832-8b05-bc6c809d79f1";
                    specialIdentifiers["vconnecttriggernodeuid"] = "3fa2c1d4-5b6e-4f70-8192-a3b4c5d6e7f8";
                    specialIdentifiers["vconnecthitpointx"] = "0.1";
                    specialIdentifiers["vconnecthitpointy"] = "0.4";
                    specialIdentifiers["vconnecthitpointz"] = "0";
                    specialIdentifiers["vconnecthitnormalx"] = "0";
                    specialIdentifiers["vconnecthitnormaly"] = "1";
                    specialIdentifiers["vconnecthitnormalz"] = "0";
                    specialIdentifiers["vconnecthitvelocityx"] = "0.33";
                    specialIdentifiers["vconnecthitvelocityy"] = "0.93";
                    specialIdentifiers["vconnecthitvelocityz"] = "0.19";
                    specialIdentifiers["vconnecthitforce"] = "24.5";
                    break;
                case EventTypeEnum.VConnectMessageReceived:
                    specialIdentifiers["vconnectmessagechannel"] = "my-event";
                    specialIdentifiers["vconnectmessagedata"] = "[\"arg1\",42,true]";
                    specialIdentifiers["vconnectmessageargumentcount"] = "3";
                    specialIdentifiers["vconnectmessageargument1"] = "arg1";
                    specialIdentifiers["vconnectmessageargument2"] = "42";
                    specialIdentifiers["vconnectmessageargument3"] = "True";
                    break;

                // OBS Studio
                case EventTypeEnum.OBSStudioRecordingStopped:
                    specialIdentifiers["obsrecordingfilepath"] = "C:\\Videos\\2024-01-01 12-00-00.mkv";
                    break;
                case EventTypeEnum.OBSStudioSceneChanged:
                    specialIdentifiers["obsscenename"] = "Gameplay";
                    specialIdentifiers["obspreviousscenename"] = "Starting Soon";
                    break;
                case EventTypeEnum.OBSStudioSourceVisibilityChanged:
                    specialIdentifiers["obsscenename"] = "Gameplay";
                    specialIdentifiers["obssourcename"] = "Webcam";
                    specialIdentifiers["obssourcevisible"] = "True";
                    break;
                case EventTypeEnum.OBSStudioFilterVisibilityChanged:
                    specialIdentifiers["obssourcename"] = "Webcam";
                    specialIdentifiers["obsfiltername"] = "Color Correction";
                    specialIdentifiers["obsfiltervisible"] = "True";
                    break;
                case EventTypeEnum.OBSStudioReplayBufferSaved:
                    specialIdentifiers["obsreplayfilepath"] = "C:\\Videos\\Replay 2024-01-01 12-00-00.mkv";
                    break;
                case EventTypeEnum.OBSStudioSceneTransitionStarted:
                case EventTypeEnum.OBSStudioSceneTransitionEnded:
                    specialIdentifiers["obstransitionname"] = "Fade";
                    break;
            }

            int eventNumber = (int)eventType;
            if (eventNumber >= 200 && eventNumber < 300)
            {
                specialIdentifiers[SpecialIdentifierStringBuilder.StreamingPlatformSpecialIdentifier] = StreamingPlatformTypeEnum.Twitch.ToString();
            }
            else if (eventNumber >= 300 && eventNumber < 400)
            {
                specialIdentifiers[SpecialIdentifierStringBuilder.StreamingPlatformSpecialIdentifier] = StreamingPlatformTypeEnum.YouTube.ToString();
            }
            else if (eventNumber >= 600 && eventNumber < 700)
            {
                specialIdentifiers[SpecialIdentifierStringBuilder.StreamingPlatformSpecialIdentifier] = StreamingPlatformTypeEnum.Kick.ToString();
            }
            else if (eventNumber >= 700 && eventNumber < 800)
            {
                specialIdentifiers[SpecialIdentifierStringBuilder.StreamingPlatformSpecialIdentifier] = StreamingPlatformTypeEnum.Velora.ToString();
            }
            else if (eventNumber >= 800 && eventNumber < 900)
            {
                specialIdentifiers[SpecialIdentifierStringBuilder.StreamingPlatformSpecialIdentifier] = StreamingPlatformTypeEnum.VPZone.ToString();
            }
            else
            {
                specialIdentifiers[SpecialIdentifierStringBuilder.StreamingPlatformSpecialIdentifier] = ChannelSession.Settings.DefaultStreamingPlatform.ToString();
            }

            return specialIdentifiers;
        }

        [DataMember]
        public EventTypeEnum EventType { get; set; }

        public EventCommandModel(EventTypeEnum eventType) : base(EnumLocalizationHelper.GetLocalizedName(eventType), CommandTypeEnum.Event) { this.EventType = eventType; }

        [Obsolete]
        public EventCommandModel() : base() { }

        public override Dictionary<string, string> GetTestSpecialIdentifiers() { return EventCommandModel.GetEventTestSpecialIdentifiers(this.EventType); }

        public override async Task<Result> CustomValidation(CommandParametersModel parameters)
        {
            if (this.UpdateFollowEventModerationCount())
            {
                bool allowFollowEvent = false;
                await EventCommandModel.followEventsInQueueSemaphore.WaitAsync();

                if (EventCommandModel.FollowEventsInQueue < ChannelSession.Settings.ModerationFollowEventMaxInQueue)
                {
                    EventCommandModel.FollowEventsInQueue++;
                    allowFollowEvent = true;
                }

                EventCommandModel.followEventsInQueueSemaphore.Release();

                if (!allowFollowEvent)
                {
                    return new Result(MixItUp.Base.Resources.ModerationFollowEventCommandCanceledMessage) { DisplayMessage = false };
                }
            }

            return await base.CustomValidation(parameters);
        }

        public override async Task PostRun(CommandParametersModel parameters)
        {
            await base.PostRun(parameters);

            if (this.UpdateFollowEventModerationCount())
            {
                EventCommandModel.FollowEventsInQueue = Math.Max(EventCommandModel.FollowEventsInQueue - 1, 0);
            }
        }

        private bool UpdateFollowEventModerationCount()
        {
            if (ChannelSession.Settings.ModerationFollowEvent)
            {
                if (this.EventType == EventTypeEnum.TwitchChannelFollowed)
                {
                    return true;
                }
                else if (this.EventType == EventTypeEnum.KickChannelFollowed)
                {
                    return true;
                }
                else if (this.EventType == EventTypeEnum.VeloraChannelFollowed)
                {
                    return true;
                }
                else if (this.EventType == EventTypeEnum.VPZoneChannelFollowed)
                {
                    return true;
                }
            }
            return false;
        }

        public override void TrackTelemetry() { ServiceManager.Get<ITelemetryService>().TrackCommand(this.Type, this.EventType.ToString()); }
    }
}

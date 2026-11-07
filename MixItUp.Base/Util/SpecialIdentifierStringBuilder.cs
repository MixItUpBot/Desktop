using Google.Apis.YouTube.v3.Data;
using MixItUp.Base.Model;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.Currency;
using MixItUp.Base.Model.Kick.Kicks;
using MixItUp.Base.Model.Overlay;
using MixItUp.Base.Model.Overlay.Widgets;
using MixItUp.Base.Model.Settings;
using MixItUp.Base.Model.Twitch.Ads;
using MixItUp.Base.Model.Twitch.Bits;
using MixItUp.Base.Model.Twitch.Clips;
using MixItUp.Base.Model.Twitch.Games;
using MixItUp.Base.Model.User;
using MixItUp.Base.Model.User.Platform;
using MixItUp.Base.Services;
using MixItUp.Base.Services.External;
using MixItUp.Base.Services.Kick.New;
using MixItUp.Base.Services.Twitch.New;
using MixItUp.Base.Services.YouTube.New;
using MixItUp.Base.ViewModel.User;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace MixItUp.Base.Util
{
    public class SpecialIdentifierStringBuilder
    {
        public const string SpecialIdentifierHeader = "$";

        public const string SpecialIdentifierNumberRegexPattern = "\\d+";
        public const string SpecialIdentifierNumberRangeRegexPattern = "\\d+:\\d+";

        public const string StreamingPlatformSpecialIdentifier = "streamingplatform";

        public const string TopSpecialIdentifierHeader = "top";
        public const string TopTimeSpecialIdentifier = TopSpecialIdentifierHeader + "time";
        public const string TopTimeRegexSpecialIdentifier = TopSpecialIdentifierHeader + "\\d+time";

        public const string TopBitsCheeredSpecialIdentifier = TopSpecialIdentifierHeader + "bitscheered";
        public const string TopBitsCheeredRegexSpecialIdentifier = TopSpecialIdentifierHeader + "\\d+bitscheered";

        public const string TopKicksGiftedSpecialIdentifier = TopSpecialIdentifierHeader + "kicksgifted";
        public const string TopKicksGiftedRegexSpecialIdentifier = TopSpecialIdentifierHeader + "\\d+kicksgifted";

        public const string TopSubscribersSpecialIdentifier = TopSpecialIdentifierHeader + "subscriber";
        public const string TopSubscribersRegexSpecialIdentifier = TopSpecialIdentifierHeader + "\\d+subscribers";

        public const string TopSubGiftersSpecialIdentifier = TopSpecialIdentifierHeader + "subgifter";
        public const string TopSubGiftersRegexSpecialIdentifier = TopSpecialIdentifierHeader + "\\d+subgifters";

        public const string TopSubStreakSpecialIdentifier = TopSpecialIdentifierHeader + "substreak";
        public const string TopSubStreakRegexSpecialIdentifier = TopSpecialIdentifierHeader + "\\d+substreaks";

        public const string UserSpecialIdentifierHeader = "user";
        public const string ArgSpecialIdentifierHeader = "arg";
        public const string ArgDelimitedSpecialIdentifierHeader = ArgSpecialIdentifierHeader + "delimited";
        public const string StreamerSpecialIdentifierHeader = "streamer";
        public const string BotSpecialIdentifierHeader = "bot";
        public const string TargetSpecialIdentifierHeader = "target";

        public const string RandomSpecialIdentifierHeader = "random";
        public const string RandomNumberRegexSpecialIdentifier = RandomSpecialIdentifierHeader + "number";
        public const string RandomFollowerSpecialIdentifierHeader = RandomSpecialIdentifierHeader + "follower";
        public const string RandomSubscriberSpecialIdentifierHeader = RandomSpecialIdentifierHeader + "subscriber";
        public const string RandomRegularSpecialIdentifierHeader = RandomSpecialIdentifierHeader + "regular";
        public const string RandomAnySpecialIdentifierHeader = RandomSpecialIdentifierHeader + "any";
        public const string RandomAnyFollowerSpecialIdentifierHeader = RandomAnySpecialIdentifierHeader + "follower";
        public const string RandomAnySubscriberSpecialIdentifierHeader = RandomAnySpecialIdentifierHeader + "subscriber";
        public const string RandomAnyRegularSpecialIdentifierHeader = RandomAnySpecialIdentifierHeader + "regular";

        public const string StreamSpecialIdentifierHeader = "stream";
        public const string StreamUptimeSpecialIdentifierHeader = StreamSpecialIdentifierHeader + "uptime";
        public const string StreamStartSpecialIdentifierHeader = StreamSpecialIdentifierHeader + "start";

        public const string MusicPlayerSpecialIdentifierHeader = "musicplayer";

        public const string TwitchSpecialIdentifierHeader = "twitch";
        public const string YouTubeSpecialIdentifierHeader = "youtube";

        public const string QuoteSpecialIdentifierHeader = "quote";

        public const string GameQueueSpecialIdentifierHeader = "gamequeue";
        public const string GameQueueUsersRegexSpecialIdentifier = GameQueueSpecialIdentifierHeader + "users\\d+";

        public const string DonationSourceSpecialIdentifier = "donationsource";
        public const string DonationTypeSpecialIdentifier = "donationtype";
        public const string DonationAmountNumberSpecialIdentifier = "donationamountnumber";
        public const string DonationAmountNumberDigitsSpecialIdentifier = "donationamountnumberdigits";
        public const string DonationAmountSpecialIdentifier = "donationamount";
        public const string DonationCurrencySpecialIdentifier = "donationcurrency";
        public const string DonationMessageSpecialIdentifier = "donationmessage";
        public const string DonationImageSpecialIdentifier = "donationimage";

        public const string PatreonTierNameSpecialIdentifier = "patreontiername";
        public const string PatreonTierAmountSpecialIdentifier = "patreontieramount";
        public const string PatreonTierImageSpecialIdentifier = "patreontierimage";

        public const string DonorDriveSpecialIdentifierHeader = "donordrive";
        public const string TiltifySpecialIdentifierHeader = "tiltify";
        public const string PulsoidSpecialIdentifierHeader = "pulsoid";

        public const string UnicodeRegexSpecialIdentifier = "unicode";

        public const string LatestFollowerUserData = "latestfollower";
        public const string LatestHostUserData = "latesthost";
        public const string LatestRaidUserData = "latestraid";
        public const string LatestRaidViewerCountData = "latestraidviewercount";
        public const string LatestSubscriberUserData = "latestsubscriber";
        public const string LatestSubscriberSubMonthsData = "latestsubscribersubmonths";
        public const string LatestBitsCheeredUserData = "latestbitscheered";
        public const string LatestBitsCheeredAmountData = "latestbitscheeredamount";
        public const string LatestDonationUserData = "latestdonation";
        public const string LatestDonationAmountData = "latestdonationamount";
        public const string LatestSuperChatUserData = "latestsuperchat";
        public const string LatestSuperChatAmountData = "latestsuperchatamount";

        public const string InteractiveTextBoxTextEntrySpecialIdentifierHelpText = "User Text Entered = " + SpecialIdentifierStringBuilder.SpecialIdentifierHeader +
            SpecialIdentifierStringBuilder.ArgSpecialIdentifierHeader + "1text";

        private static Dictionary<string, string> GlobalSpecialIdentifiers = new Dictionary<string, string>();

        public static async Task<string> ProcessSpecialIdentifiers(string str, CommandParametersModel parameters, bool encode = false)
        {
            SpecialIdentifierStringBuilder siString = new SpecialIdentifierStringBuilder(str, encode);
            await siString.ReplaceCommonSpecialModifiers(parameters);
            return siString.ToString();
        }

        internal static async Task<string> ProcessSpecialIdentifiers(string str, CommandParametersModel parameters, Func<string, string> encodeReplacement)
        {
            SpecialIdentifierStringBuilder siString = new SpecialIdentifierStringBuilder(str);
            siString.encodeReplacement = encodeReplacement;
            siString.replacementCharacters = Enumerable.Repeat(false, siString.text.Length).ToList();
            await siString.ReplaceCommonSpecialModifiers(parameters);
            return siString.ToString();
        }

        public static void AddGlobalSpecialIdentifier(string specialIdentifier, string replacement)
        {
            SpecialIdentifierStringBuilder.GlobalSpecialIdentifiers[specialIdentifier] = replacement;
        }

        public static void RemoveGlobalSpecialIdentifier(string specialIdentifier)
        {
            SpecialIdentifierStringBuilder.GlobalSpecialIdentifiers.Remove(specialIdentifier);
        }

        public static bool IsValidSpecialIdentifier(string text)
        {
            return !string.IsNullOrEmpty(text) && text.All(c => Char.IsLetterOrDigit(c));
        }

        public static string ConvertToSpecialIdentifier(string text, int maxLength = 0)
        {
            if (!string.IsNullOrEmpty(text))
            {
                text = text.Trim();

                StringBuilder specialIdentifier = new StringBuilder();
                for (int i = 0; i < text.Length; i++)
                {
                    if (char.IsLetterOrDigit(text[i]))
                    {
                        specialIdentifier.Append(text[i]);
                    }
                }
                string result = specialIdentifier.ToString().ToLower();

                if (maxLength > 0 && result.Length > maxLength)
                {
                    result = result.Substring(0, maxLength);
                }

                return result;
            }
            return null;
        }

        public static string ReplaceParameterVariablesEntries(string text, string pattern, string preReplacement, string postReplacement = null)
        {
            int startIndex = 0;
            do
            {
                startIndex = text.IndexOf(pattern);
                if (startIndex >= 0)
                {
                    int endIndex = text.IndexOf(")", startIndex);
                    if (endIndex >= 0)
                    {
                        string fullEntry = text.Substring(startIndex, endIndex - startIndex + 1);
                        string leftOver = fullEntry.Replace(pattern, "").Replace(")", "");
                        text = text.Replace(fullEntry, preReplacement + leftOver + (!string.IsNullOrEmpty(postReplacement) ? postReplacement : string.Empty));
                    }
                }
            } while (startIndex >= 0);
            return text;
        }

        public static async Task<IEnumerable<UserV2Model>> GetUserOrderedCurrencyList(CurrencyModel currency)
        {
            IEnumerable<UserV2Model> applicableUsers = await SpecialIdentifierStringBuilder.GetAllNonExemptUsers();
            return applicableUsers.OrderByDescending(u => currency.GetAmount(u));
        }

        public static async Task<IEnumerable<UserV2Model>> GetAllNonExemptUsers()
        {
            await ServiceManager.Get<UserService>().LoadAllUserData();

            List<UserV2Model> exemptUsers = new List<UserV2Model>(ChannelSession.Settings.Users.Values.Where(u => !u.IsSpecialtyExcluded));
            exemptUsers.Remove(ChannelSession.User.Model);
            return exemptUsers;
        }

        public static bool ContainsSpecialIdentifiers(string text) { return !string.IsNullOrEmpty(text) && text.Contains(SpecialIdentifierHeader); }

        private string text;
        private bool encode;
        private Func<string, string> encodeReplacement;
        private List<bool> replacementCharacters;

        public SpecialIdentifierStringBuilder(string text, bool encode = false)
        {
            this.text = !string.IsNullOrEmpty(text) ? text : string.Empty;
            this.encode = encode;
        }

        public async Task ReplaceCommonSpecialModifiers(CommandParametersModel parameters)
        {
            StreamingPlatformTypeEnum platform = parameters.Platform;
            if (platform == StreamingPlatformTypeEnum.All)
            {
                platform = ChannelSession.Settings.DefaultStreamingPlatform;
            }

            if (!SpecialIdentifierStringBuilder.ContainsSpecialIdentifiers(this.text))
            {
                return;
            }

            foreach (var kvp in parameters.SpecialIdentifiers.OrderByDescending(kvp => kvp.Key))
            {
                this.ReplaceSpecialIdentifier(kvp.Key, kvp.Value);
            }

            foreach (var kvp in SpecialIdentifierStringBuilder.GlobalSpecialIdentifiers.OrderByDescending(kvp => kvp.Key))
            {
                this.ReplaceSpecialIdentifier(kvp.Key, kvp.Value);
            }

            foreach (CounterModel counter in ChannelSession.Settings.Counters.Values.OrderByDescending(c => c.Name))
            {
                this.ReplaceSpecialIdentifier(counter.Name + "display", counter.Amount.ToNumberDisplayString());
                this.ReplaceSpecialIdentifier(counter.Name, counter.Amount.ToString());
            }

            this.ReplaceSpecialIdentifier("dayoftheweekname", DateTimeOffset.Now.ToString("dddd"));
            this.ReplaceSpecialIdentifier("dayoftheweek", DateTimeOffset.Now.DayOfWeek.ToString());
            this.ReplaceSpecialIdentifier("datetime", DateTimeOffset.Now.ToString("g"));
            this.ReplaceSpecialIdentifier("dateyear", DateTimeOffset.Now.ToString("yyyy"));
            this.ReplaceSpecialIdentifier("datemonthname", DateTimeOffset.Now.ToString("MMMM"));
            this.ReplaceSpecialIdentifier("datemonth", DateTimeOffset.Now.ToString("MM"));
            this.ReplaceSpecialIdentifier("dateday", DateTimeOffset.Now.ToString("dd"));
            this.ReplaceSpecialIdentifier("date", DateTimeOffset.Now.ToString("d"));
            this.ReplaceSpecialIdentifier("timedigits", DateTimeOffset.Now.ToString("HHmm"));
            this.ReplaceSpecialIdentifier("timehour", DateTimeOffset.Now.ToString("HH"));
            this.ReplaceSpecialIdentifier("timeminute", DateTimeOffset.Now.ToString("mm"));
            this.ReplaceSpecialIdentifier("timesecond", DateTimeOffset.Now.ToString("ss"));
            this.ReplaceSpecialIdentifier("time", DateTimeOffset.Now.ToString("t"));
            this.ReplaceSpecialIdentifier("linebreak", Environment.NewLine);

            if (this.ContainsSpecialIdentifier("streamcurrentscene"))
            {
                IStreamingSoftwareService ssService = null;
                switch (ChannelSession.Settings.DefaultStreamingSoftware)
                {
                    case Model.Actions.StreamingSoftwareTypeEnum.OBSStudio:
                        ssService = ServiceManager.Get<IOBSStudioService>();
                        break;
                    case Model.Actions.StreamingSoftwareTypeEnum.XSplit:
                        ssService = ServiceManager.Get<XSplitService>();
                        break;
                    case Model.Actions.StreamingSoftwareTypeEnum.StreamlabsDesktop:
                        ssService = ServiceManager.Get<StreamlabsDesktopService>();
                        break;
                }

                string currentScene = "Unknown";
                if (ssService != null && ssService.IsEnabled)
                {
                    if (!ssService.IsConnected)
                    {
                        Result result = await ssService.Connect();
                        if (!result.Success)
                        {
                            Logger.Log(LogLevel.Error, result.Message);
                        }
                    }

                    if (ssService.IsConnected)
                    {
                        currentScene = await ssService.GetCurrentScene();
                    }
                }

                this.ReplaceSpecialIdentifier("streamcurrentscene", currentScene);
            }

            if (this.ContainsSpecialIdentifier("voicemodcurrentvoice"))
            {
                IVoicemodService vmService = ServiceManager.Get<IVoicemodService>();

                string currentVoice = "Unknown";
                if (ChannelSession.Settings.EnableVoicemodStudio)
                {
                    if (!vmService.IsConnected)
                    {
                        Result result = await vmService.Connect();
                        if (!result.Success)
                        {
                            Logger.Log(LogLevel.Error, result.Message);
                        }
                    }

                    if (vmService.IsConnected)
                    {
                        string voicemodCurrentVoice = await vmService.GetCurrentVoice();
                        if (!string.IsNullOrEmpty(voicemodCurrentVoice))
                        {
                            currentVoice = voicemodCurrentVoice;
                        }
                    }
                }

                this.ReplaceSpecialIdentifier("voicemodcurrentvoice", currentVoice);
            }

            if (ServiceManager.Get<GameQueueService>().IsEnabled && this.ContainsSpecialIdentifier(GameQueueSpecialIdentifierHeader))
            {
                this.ReplaceSpecialIdentifier(GameQueueSpecialIdentifierHeader + "total", ServiceManager.Get<GameQueueService>().Queue.Count().ToString());
                if (this.ContainsRegexSpecialIdentifier(GameQueueUsersRegexSpecialIdentifier))
                {
                    await this.ReplaceNumberBasedRegexSpecialIdentifier(GameQueueUsersRegexSpecialIdentifier, (total) =>
                    {
                        IEnumerable<CommandParametersModel> applicableUsers = ServiceManager.Get<GameQueueService>().Queue.Take(total).ToList();
                        string result = MixItUp.Base.Resources.NoUsersFound;
                        if (applicableUsers.Count() > 0)
                        {
                            result = string.Join(" ", applicableUsers.Select(p => $"@{p.User.Username}"));
                        }
                        return Task.FromResult(result);
                    });
                }
            }

            if (this.ContainsSpecialIdentifier(SpecialIdentifierStringBuilder.TopSpecialIdentifierHeader))
            {
                if (this.ContainsRegexSpecialIdentifier(SpecialIdentifierStringBuilder.TopBitsCheeredRegexSpecialIdentifier))
                {
                    await this.HandleTopBitsCheeredRegex(BitsLeaderboardPeriodEnum.Day);
                    await this.HandleTopBitsCheeredRegex(BitsLeaderboardPeriodEnum.Week);
                    await this.HandleTopBitsCheeredRegex(BitsLeaderboardPeriodEnum.Month);
                    await this.HandleTopBitsCheeredRegex(BitsLeaderboardPeriodEnum.Year);
                    await this.HandleTopBitsCheeredRegex(BitsLeaderboardPeriodEnum.All);
                }

                if (this.ContainsSpecialIdentifier(SpecialIdentifierStringBuilder.TopBitsCheeredSpecialIdentifier))
                {
                    await this.HandleTopBitsCheered(BitsLeaderboardPeriodEnum.Day);
                    await this.HandleTopBitsCheered(BitsLeaderboardPeriodEnum.Week);
                    await this.HandleTopBitsCheered(BitsLeaderboardPeriodEnum.Month);
                    await this.HandleTopBitsCheered(BitsLeaderboardPeriodEnum.Year);
                    await this.HandleTopBitsCheered(BitsLeaderboardPeriodEnum.All);
                }

                if (this.ContainsRegexSpecialIdentifier(SpecialIdentifierStringBuilder.TopKicksGiftedRegexSpecialIdentifier))
                {
                    await this.HandleTopKicksGiftedRegex(KicksLeaderboardPeriodEnum.Lifetime);
                    await this.HandleTopKicksGiftedRegex(KicksLeaderboardPeriodEnum.Month);
                    await this.HandleTopKicksGiftedRegex(KicksLeaderboardPeriodEnum.Week);
                }

                if (this.ContainsSpecialIdentifier(SpecialIdentifierStringBuilder.TopKicksGiftedSpecialIdentifier))
                {
                    await this.HandleTopKicksGifted(KicksLeaderboardPeriodEnum.Lifetime);
                    await this.HandleTopKicksGifted(KicksLeaderboardPeriodEnum.Month);
                    await this.HandleTopKicksGifted(KicksLeaderboardPeriodEnum.Week);
                }

                if (this.ContainsRegexSpecialIdentifier(SpecialIdentifierStringBuilder.TopTimeRegexSpecialIdentifier))
                {
                    await this.ReplaceNumberBasedRegexSpecialIdentifier(SpecialIdentifierStringBuilder.TopTimeRegexSpecialIdentifier, async (total) =>
                    {
                        IEnumerable<UserV2Model> applicableUsers = await SpecialIdentifierStringBuilder.GetAllNonExemptUsers();
                        List<string> timeUserList = new List<string>();
                        int userPosition = 1;
                        foreach (UserV2Model timeUser in applicableUsers.OrderByDescending(u => u.OnlineViewingMinutes).Take(total))
                        {
                            UserV2ViewModel timeUserViewModel = new UserV2ViewModel(timeUser);
                            timeUserList.Add($"#{userPosition}) {timeUserViewModel.Username} - {timeUserViewModel.OnlineViewingTimeString}");
                            userPosition++;
                        }

                        string result = MixItUp.Base.Resources.NoUsersFound;
                        if (timeUserList.Count > 0)
                        {
                            result = string.Join(", ", timeUserList);
                        }
                        return result;
                    });
                }

                if (this.ContainsSpecialIdentifier(SpecialIdentifierStringBuilder.TopTimeSpecialIdentifier))
                {
                    IEnumerable<UserV2Model> applicableUsers = await SpecialIdentifierStringBuilder.GetAllNonExemptUsers();
                    UserV2Model topUserData = applicableUsers.Top(u => u.OnlineViewingMinutes);
                    UserV2ViewModel topUser = ServiceManager.Get<UserService>().GetActiveUserByID(parameters.Platform, topUserData.ID);
                    if (topUser == null)
                    {
                        topUser = new UserV2ViewModel(topUserData);
                    }
                    await this.HandleUserSpecialIdentifiers(topUser, SpecialIdentifierStringBuilder.TopTimeSpecialIdentifier);
                }

                if (this.ContainsRegexSpecialIdentifier(SpecialIdentifierStringBuilder.TopSubscribersRegexSpecialIdentifier))
                {
                    await this.HandleTopSubscribersRegex();
                }

                if (this.ContainsSpecialIdentifier(SpecialIdentifierStringBuilder.TopSubscribersSpecialIdentifier))
                {
                    await this.HandleTopSubscriber(parameters);
                }

                if (this.ContainsRegexSpecialIdentifier(SpecialIdentifierStringBuilder.TopSubGiftersRegexSpecialIdentifier))
                {
                    await this.HandleTopSubGiftersRegex();
                }

                if (this.ContainsSpecialIdentifier(SpecialIdentifierStringBuilder.TopSubGiftersSpecialIdentifier))
                {
                    await this.HandleTopSubGifter(parameters);
                }

                if (this.ContainsRegexSpecialIdentifier(SpecialIdentifierStringBuilder.TopSubStreakRegexSpecialIdentifier))
                {
                    await this.HandleTopSubStreaksRegex();
                }

                if (this.ContainsSpecialIdentifier(SpecialIdentifierStringBuilder.TopSubStreakSpecialIdentifier))
                {
                    await this.HandleTopSubStreak(parameters);
                }

                foreach (CurrencyModel currency in ChannelSession.Settings.Currency.Values)
                {
                    if (this.ContainsRegexSpecialIdentifier(currency.TopRegexSpecialIdentifier))
                    {
                        await this.ReplaceNumberBasedRegexSpecialIdentifier(currency.TopRegexSpecialIdentifier, async (total) =>
                        {
                            List<string> currencyUserList = new List<string>();
                            int userPosition = 1;
                            foreach (UserV2Model userData in (await SpecialIdentifierStringBuilder.GetUserOrderedCurrencyList(currency)).Take(total))
                            {
                                UserV2ViewModel userViewModel = new UserV2ViewModel(userData);
                                currencyUserList.Add($"#{userPosition}) {userViewModel.Username} - {currency.GetAmount(userData).ToNumberDisplayString()}");
                                userPosition++;
                            }

                            string result = MixItUp.Base.Resources.NoUsersFound;
                            if (currencyUserList.Count > 0)
                            {
                                result = string.Join(", ", currencyUserList);
                            }
                            return result;
                        });
                    }

                    if (this.ContainsSpecialIdentifier(currency.TopUserSpecialIdentifier))
                    {
                        IEnumerable<UserV2Model> applicableUsers = await SpecialIdentifierStringBuilder.GetAllNonExemptUsers();
                        UserV2Model topUserData = applicableUsers.Top(u => currency.GetAmount(u));
                        UserV2ViewModel topUser = ServiceManager.Get<UserService>().GetActiveUserByID(parameters.Platform, topUserData.ID);
                        if (topUser == null)
                        {
                            topUser = new UserV2ViewModel(topUserData);
                        }
                        await this.HandleUserSpecialIdentifiers(topUser, currency.TopSpecialIdentifier);
                    }
                }

                foreach (InventoryModel inventory in ChannelSession.Settings.Inventory.Values)
                {
                    if (this.ContainsRegexSpecialIdentifier(inventory.TopTotalRegexSpecialIdentifier))
                    {
                        await this.ReplaceNumberBasedRegexSpecialIdentifier(inventory.TopTotalRegexSpecialIdentifier, async (total) =>
                        {
                            IEnumerable<UserV2Model> applicableUsers = await SpecialIdentifierStringBuilder.GetAllNonExemptUsers();
                            List<string> inventoryUserList = new List<string>();
                            int userPosition = 1;
                            foreach (UserV2Model userData in applicableUsers.OrderByDescending(u => inventory.GetTotalAmount(u)).Take(total))
                            {
                                UserV2ViewModel userViewModel = new UserV2ViewModel(userData);
                                inventoryUserList.Add($"#{userPosition}) {userViewModel.Username} - {inventory.GetTotalAmount(userData).ToNumberDisplayString()}");
                                userPosition++;
                            }

                            string result = MixItUp.Base.Resources.NoUsersFound;
                            if (inventoryUserList.Count > 0)
                            {
                                result = string.Join(", ", inventoryUserList);
                            }
                            return result;
                        });
                    }

                    if (this.ContainsSpecialIdentifier(inventory.TopTotalUserSpecialIdentifier))
                    {
                        IEnumerable<UserV2Model> applicableUsers = await SpecialIdentifierStringBuilder.GetAllNonExemptUsers();
                        UserV2Model topUserData = applicableUsers.Top(u => inventory.GetTotalAmount(u));
                        if (topUserData != null)
                        {
                            UserV2ViewModel topUser = ServiceManager.Get<UserService>().GetActiveUserByID(parameters.Platform, topUserData.ID);
                            if (topUser == null)
                            {
                                topUser = new UserV2ViewModel(topUserData);
                            }
                            await this.HandleUserSpecialIdentifiers(topUser, inventory.TopTotalSpecialIdentifier);
                        }
                    }

                    if (this.ContainsRegexSpecialIdentifier(inventory.TopUniqueRegexSpecialIdentifier))
                    {
                        await this.ReplaceNumberBasedRegexSpecialIdentifier(inventory.TopUniqueRegexSpecialIdentifier, async (total) =>
                        {
                            IEnumerable<UserV2Model> applicableUsers = await SpecialIdentifierStringBuilder.GetAllNonExemptUsers();
                            List<string> inventoryUserList = new List<string>();
                            int userPosition = 1;
                            foreach (UserV2Model userData in applicableUsers.OrderByDescending(u => inventory.GetUniqueItemCount(u)).Take(total))
                            {
                                UserV2ViewModel userViewModel = new UserV2ViewModel(userData);
                                inventoryUserList.Add($"#{userPosition}) {userViewModel.Username} - {inventory.GetUniqueItemCount(userData).ToNumberDisplayString()}");
                                userPosition++;
                            }

                            string result = MixItUp.Base.Resources.NoUsersFound;
                            if (inventoryUserList.Count > 0)
                            {
                                result = string.Join(", ", inventoryUserList);
                            }
                            return result;
                        });
                    }

                    if (this.ContainsSpecialIdentifier(inventory.TopUniqueUserSpecialIdentifier))
                    {
                        IEnumerable<UserV2Model> applicableUsers = await SpecialIdentifierStringBuilder.GetAllNonExemptUsers();
                        UserV2Model topUserData = applicableUsers.Top(u => inventory.GetUniqueItemCount(u));
                        if (topUserData != null)
                        {
                            UserV2ViewModel topUser = ServiceManager.Get<UserService>().GetActiveUserByID(parameters.Platform, topUserData.ID);
                            if (topUser == null)
                            {
                                topUser = new UserV2ViewModel(topUserData);
                            }
                            await this.HandleUserSpecialIdentifiers(topUser, inventory.TopUniqueSpecialIdentifier);
                        }
                    }

                    foreach (InventoryItemModel item in inventory.Items.Values)
                    {
                        string itemTopRegex = string.Format("{0}\\d+{1}{2}", SpecialIdentifierStringBuilder.TopSpecialIdentifierHeader, inventory.SpecialIdentifier, item.SpecialIdentifier);
                        if (this.ContainsRegexSpecialIdentifier(itemTopRegex))
                        {
                            await this.ReplaceNumberBasedRegexSpecialIdentifier(itemTopRegex, async (total) =>
                            {
                                IEnumerable<UserV2Model> applicableUsers = await SpecialIdentifierStringBuilder.GetAllNonExemptUsers();
                                List<string> itemUserList = new List<string>();
                                int userPosition = 1;
                                foreach (UserV2Model userData in applicableUsers.OrderByDescending(u => inventory.GetAmount(u, item)).Take(total))
                                {
                                    UserV2ViewModel userViewModel = new UserV2ViewModel(userData);
                                    itemUserList.Add($"#{userPosition}) {userViewModel.Username} - {inventory.GetAmount(userData, item).ToNumberDisplayString()}");
                                    userPosition++;
                                }

                                string result = MixItUp.Base.Resources.NoUsersFound;
                                if (itemUserList.Count > 0)
                                {
                                    result = string.Join(", ", itemUserList);
                                }
                                return result;
                            });
                        }

                        string itemTopUser = string.Format("{0}{1}{2}{3}", SpecialIdentifierStringBuilder.TopSpecialIdentifierHeader, inventory.SpecialIdentifier, item.SpecialIdentifier, SpecialIdentifierStringBuilder.UserSpecialIdentifierHeader);
                        if (this.ContainsSpecialIdentifier(itemTopUser))
                        {
                            IEnumerable<UserV2Model> applicableUsers = await SpecialIdentifierStringBuilder.GetAllNonExemptUsers();
                            UserV2Model topUserData = applicableUsers.Top(u => inventory.GetAmount(u, item));
                            if (topUserData != null)
                            {
                                UserV2ViewModel topUser = ServiceManager.Get<UserService>().GetActiveUserByID(parameters.Platform, topUserData.ID);
                                if (topUser == null)
                                {
                                    topUser = new UserV2ViewModel(topUserData);
                                }
                                string itemTopPrefix = string.Format("{0}{1}{2}", SpecialIdentifierStringBuilder.TopSpecialIdentifierHeader, inventory.SpecialIdentifier, item.SpecialIdentifier);
                                await this.HandleUserSpecialIdentifiers(topUser, itemTopPrefix);
                            }
                        }
                    }
                }
            }

            foreach (CurrencyModel currency in ChannelSession.Settings.Currency.Values)
            {
                if (this.ContainsRegexSpecialIdentifier(currency.AllTotalAmountSpecialIdentifier) || this.ContainsRegexSpecialIdentifier(currency.AllTotalAmountDisplaySpecialIdentifier))
                {
                    await ServiceManager.Get<UserService>().LoadAllUserData();

                    IEnumerable<UserV2Model> applicableUsers = await SpecialIdentifierStringBuilder.GetAllNonExemptUsers();
                    int total = applicableUsers.Sum(u => currency.GetAmount(u));

                    this.ReplaceSpecialIdentifier(currency.AllTotalAmountDisplaySpecialIdentifier, total.ToNumberDisplayString());
                    this.ReplaceSpecialIdentifier(currency.AllTotalAmountSpecialIdentifier, total.ToString());
                }

                if (currency.IsRank && this.ContainsSpecialIdentifier(currency.RankCountSpecialIdentifierHeader))
                {
                    await ServiceManager.Get<UserService>().LoadAllUserData();

                    IEnumerable<UserV2Model> applicableUsers = await SpecialIdentifierStringBuilder.GetAllNonExemptUsers();

                    foreach (RankModel rank in currency.Ranks.OrderByDescending(r => r.Name.Length))
                    {
                        string rankSpecialIdentifier = SpecialIdentifierStringBuilder.ConvertToSpecialIdentifier(rank.Name);
                        string fullIdentifier = currency.RankCountSpecialIdentifierHeader + rankSpecialIdentifier;

                        if (this.ContainsSpecialIdentifier(fullIdentifier))
                        {
                            int count = applicableUsers.Count(u => currency.GetRank(u).Name.Equals(rank.Name, StringComparison.OrdinalIgnoreCase));
                            this.ReplaceSpecialIdentifier(fullIdentifier, count.ToString());
                        }
                    }
                }
            }

            if (ChannelSession.Settings.QuotesEnabled && ChannelSession.Settings.Quotes.Count > 0)
            {
                if (this.ContainsSpecialIdentifier(QuoteSpecialIdentifierHeader))
                {
                    this.ReplaceSpecialIdentifier(QuoteSpecialIdentifierHeader + "random", ChannelSession.Settings.Quotes.PickRandom().ToString());
                    this.ReplaceSpecialIdentifier(QuoteSpecialIdentifierHeader + "latest", ChannelSession.Settings.Quotes.Last().ToString());
                    this.ReplaceSpecialIdentifier(QuoteSpecialIdentifierHeader + "total", ChannelSession.Settings.Quotes.Count.ToString());

                    if (this.ContainsRegexSpecialIdentifier(QuoteSpecialIdentifierHeader + SpecialIdentifierNumberRegexPattern))
                    {
                        await this.ReplaceNumberBasedRegexSpecialIdentifier(QuoteSpecialIdentifierHeader + SpecialIdentifierNumberRegexPattern, (index) =>
                        {
                            UserQuoteModel quote = ChannelSession.Settings.Quotes.SingleOrDefault(q => q.ID == index);
                            if (quote != null)
                            {
                                return Task.FromResult(quote.ToString());
                            }
                            return Task.FromResult<string>(null);
                        });
                    }   
                }
            }

            if (ServiceManager.Get<GiveawayService>().IsRunning)
            {
                foreach (var kvp in ServiceManager.Get<GiveawayService>().GetSpecialIdentifiers())
                {
                    this.ReplaceSpecialIdentifier(kvp.Key, kvp.Value);
                }
            }

            if (ServiceManager.Get<DonorDriveService>().IsConnected)
            {
                if (this.ContainsSpecialIdentifier(DonorDriveSpecialIdentifierHeader + "user") ||
                    this.ContainsSpecialIdentifier(DonorDriveSpecialIdentifierHeader + "event") ||
                    this.ContainsSpecialIdentifier(DonorDriveSpecialIdentifierHeader + "team"))
                {
                    await ServiceManager.Get<DonorDriveService>().RefreshData();

                    DonorDriveParticipant participant = ServiceManager.Get<DonorDriveService>().Participant;
                    this.ReplaceSpecialIdentifier(DonorDriveSpecialIdentifierHeader + "userdonationgoal", participant.fundraisingGoal.ToString());
                    this.ReplaceSpecialIdentifier(DonorDriveSpecialIdentifierHeader + "userdonationcount", participant.numDonations.ToString());
                    this.ReplaceSpecialIdentifier(DonorDriveSpecialIdentifierHeader + "userdonationamount", participant.sumDonations.ToString());

                    DonorDriveEvent ddEvent = ServiceManager.Get<DonorDriveService>().Event;
                    this.ReplaceSpecialIdentifier(DonorDriveSpecialIdentifierHeader + "eventdonationgoal", ddEvent.fundraisingGoal.ToString());
                    this.ReplaceSpecialIdentifier(DonorDriveSpecialIdentifierHeader + "eventdonationcount", ddEvent.numDonations.ToString());
                    this.ReplaceSpecialIdentifier(DonorDriveSpecialIdentifierHeader + "eventdonationamount", ddEvent.sumDonations.ToString());

                    DonorDriveTeam team = ServiceManager.Get<DonorDriveService>().Team;
                    if (team != null)
                    {
                        this.ReplaceSpecialIdentifier(DonorDriveSpecialIdentifierHeader + "teamdonationgoal", team.fundraisingGoal.ToString());
                        this.ReplaceSpecialIdentifier(DonorDriveSpecialIdentifierHeader + "teamdonationcount", team.numDonations.ToString());
                        this.ReplaceSpecialIdentifier(DonorDriveSpecialIdentifierHeader + "teamdonationamount", team.sumDonations.ToString());
                    }
                }
            }

            if (ServiceManager.Get<TiltifyService>().IsConnected)
            {
                if (this.ContainsSpecialIdentifier(TiltifySpecialIdentifierHeader))
                {
                    await ServiceManager.Get<TiltifyService>().RefreshCampaign();

                    TiltifyCampaign campaign = ServiceManager.Get<TiltifyService>().Campaign;
                    if (campaign != null)
                    {
                        this.ReplaceSpecialIdentifier(TiltifySpecialIdentifierHeader + "campaignurl", campaign.url);
                        this.ReplaceSpecialIdentifier(TiltifySpecialIdentifierHeader + "donationurl", campaign.donate_url);
                        this.ReplaceSpecialIdentifier(TiltifySpecialIdentifierHeader + "donationgoal", campaign.Goal.ToString());
                        this.ReplaceSpecialIdentifier(TiltifySpecialIdentifierHeader + "donationamount", campaign.AmountRaised.ToString());
                    }
                }
            }

            if (ServiceManager.Get<PulsoidService>().IsConnected)
            {
                if (this.ContainsSpecialIdentifier(PulsoidSpecialIdentifierHeader))
                {
                    PulsoidHeartRate heartRate = ServiceManager.Get<PulsoidService>().LastHeartRate;
                    if (heartRate != null)
                    {
                        this.ReplaceSpecialIdentifier(PulsoidSpecialIdentifierHeader + "heartrate", heartRate.HeartRate.ToString());
                    }
                }
            }

            if (this.ContainsSpecialIdentifier(MusicPlayerSpecialIdentifierHeader))
            {
                this.ReplaceSpecialIdentifier(MusicPlayerSpecialIdentifierHeader + "state", ServiceManager.Get<IMusicPlayerService>().State.ToString());

                MusicPlayerSong song = ServiceManager.Get<IMusicPlayerService>().CurrentSong;
                if (this.ContainsSpecialIdentifier(MusicPlayerSpecialIdentifierHeader + "albumart") || this.ContainsSpecialIdentifier(MusicPlayerSpecialIdentifierHeader + "albumcover"))
                {
                    string albumArt = await ServiceManager.Get<IMusicPlayerService>().GetAlbumArt(song);
                    this.ReplaceSpecialIdentifier(MusicPlayerSpecialIdentifierHeader + "albumart", albumArt);
                    this.ReplaceSpecialIdentifier(MusicPlayerSpecialIdentifierHeader + "albumcover", albumArt);
                }
                if (song != null)
                {
                    this.ReplaceSpecialIdentifier(MusicPlayerSpecialIdentifierHeader + "title", song.Title);
                    this.ReplaceSpecialIdentifier(MusicPlayerSpecialIdentifierHeader + "artist", song.Artist);
                    this.ReplaceSpecialIdentifier(MusicPlayerSpecialIdentifierHeader + "display", song.ToString());
                    this.ReplaceSpecialIdentifier(MusicPlayerSpecialIdentifierHeader + "length", song.Length.ToString());
                    this.ReplaceSpecialIdentifier(MusicPlayerSpecialIdentifierHeader + "duration", song.LengthString);
                }
            }

            if (this.ContainsSpecialIdentifier(StreamSpecialIdentifierHeader))
            {
                if (this.ContainsSpecialIdentifier(StreamUptimeSpecialIdentifierHeader) || this.ContainsSpecialIdentifier(StreamStartSpecialIdentifierHeader))
                {
                    DateTimeOffset startTime = await UptimePreMadeChatCommandModel.GetStartTime(parameters.Platform);
                    Logger.Log(LogLevel.Debug, $"Channel stream info: {JSONSerializerHelper.SerializeToString(ServiceManager.Get<TwitchSession>().Stream)} - {JSONSerializerHelper.SerializeToString(ServiceManager.Get<YouTubeSession>().LiveBroadcasts.Values.FirstOrDefault())}");
                    if (startTime > DateTimeOffset.MinValue)
                    {
                        TimeSpan duration = DateTimeOffset.Now.Subtract(startTime);

                        this.ReplaceSpecialIdentifier(StreamStartSpecialIdentifierHeader + "datetime", startTime.ToString("g"));
                        this.ReplaceSpecialIdentifier(StreamStartSpecialIdentifierHeader + "date", startTime.ToString("d"));
                        this.ReplaceSpecialIdentifier(StreamStartSpecialIdentifierHeader + "time", startTime.ToString("t"));

                        this.ReplaceSpecialIdentifier(StreamUptimeSpecialIdentifierHeader + "total", (int)duration.TotalHours + duration.ToString("\\:mm"));
                        this.ReplaceSpecialIdentifier(StreamUptimeSpecialIdentifierHeader + "hours", ((int)duration.TotalHours).ToString());
                        this.ReplaceSpecialIdentifier(StreamUptimeSpecialIdentifierHeader + "minutes", duration.ToString("mm"));
                        this.ReplaceSpecialIdentifier(StreamUptimeSpecialIdentifierHeader + "seconds", duration.ToString("ss"));
                    }
                }

                StreamingPlatformSessionBase session = StreamingPlatforms.GetPlatformSession(platform);
                if (session.IsConnected)
                {
                    this.ReplaceSpecialIdentifier(StreamSpecialIdentifierHeader + "islive", session.IsLive.ToString());
                    this.ReplaceSpecialIdentifier(StreamSpecialIdentifierHeader + "viewercount", session.StreamViewerCount.ToString());
                    this.ReplaceSpecialIdentifier(StreamSpecialIdentifierHeader + "title", session.StreamTitle);
                    this.ReplaceSpecialIdentifier(StreamSpecialIdentifierHeader + "gameid", session.StreamCategoryID);
                    this.ReplaceSpecialIdentifier(StreamSpecialIdentifierHeader + "gamename", session.StreamCategoryName);
                    this.ReplaceSpecialIdentifier(StreamSpecialIdentifierHeader + "gameimage", session.StreamCategoryImageURL);
                    this.ReplaceSpecialIdentifier(StreamSpecialIdentifierHeader + "game", session.StreamCategoryName);
                }

                if (platform == StreamingPlatformTypeEnum.Twitch && ServiceManager.Get<TwitchSession>().IsConnected)
                {
                    string thumbnail = ServiceManager.Get<TwitchSession>().Stream?.thumbnail_url;
                    if (!string.IsNullOrEmpty(thumbnail))
                    {
                        thumbnail = thumbnail.Replace("{width}", "1920");
                        thumbnail = thumbnail.Replace("{height}", "1080");
                        this.ReplaceSpecialIdentifier(StreamSpecialIdentifierHeader + "thumbnail", thumbnail);
                    }

                    if (this.ContainsSpecialIdentifier(StreamSpecialIdentifierHeader + "followercount"))
                    {
                        long followCount = await ServiceManager.Get<TwitchSession>().StreamerService.GetFollowerCount(ServiceManager.Get<TwitchSession>().StreamerModel);
                        this.ReplaceSpecialIdentifier(StreamSpecialIdentifierHeader + "followercount", followCount.ToString());
                    }

                    if (this.ContainsSpecialIdentifier(StreamSpecialIdentifierHeader + "subscribercount"))
                    {
                        long subCount = await ServiceManager.Get<TwitchSession>().StreamerService.GetSubscriberCount(ServiceManager.Get<TwitchSession>().StreamerModel);
                        this.ReplaceSpecialIdentifier(StreamSpecialIdentifierHeader + "subscribercount", subCount.ToString());
                    }

                    if (this.ContainsSpecialIdentifier(StreamSpecialIdentifierHeader + "subscriberpoints"))
                    {
                        long subCount = await ServiceManager.Get<TwitchSession>().StreamerService.GetSubscriberPoints(ServiceManager.Get<TwitchSession>().StreamerModel);
                        this.ReplaceSpecialIdentifier(StreamSpecialIdentifierHeader + "subscriberpoints", subCount.ToString());
                    }

                    if (this.ContainsSpecialIdentifier(StreamSpecialIdentifierHeader + "twitchtags"))
                    {
                        var tags = ServiceManager.Get<TwitchSession>().Channel?.tags;
                        if (tags != null && tags.Count() > 0)
                        {
                            this.ReplaceSpecialIdentifier(StreamSpecialIdentifierHeader + "twitchtags", string.Join(", ", tags));
                        }
                    }
                }
                else if (platform == StreamingPlatformTypeEnum.YouTube && ServiceManager.Get<YouTubeSession>().IsConnected)
                {
                    LiveBroadcast broadcast = ServiceManager.Get<YouTubeSession>().LiveBroadcasts.Values.FirstOrDefault();

                    this.ReplaceSpecialIdentifier(StreamSpecialIdentifierHeader + "description", broadcast?.Snippet?.Description);
                    this.ReplaceSpecialIdentifier(StreamSpecialIdentifierHeader + "followercount", ServiceManager.Get<YouTubeSession>().StreamerModel.Statistics.SubscriberCount.GetValueOrDefault().ToString());
                    if (ServiceManager.Get<YouTubeSession>().IsLive)
                    {
                        this.ReplaceSpecialIdentifier(StreamSpecialIdentifierHeader + "youtubeid", broadcast?.Id);
                        this.ReplaceSpecialIdentifier(StreamSpecialIdentifierHeader + "youtubeurl", ServiceManager.Get<YouTubeSession>().StreamLink);
                    }
                }
                else if (platform == StreamingPlatformTypeEnum.Kick && ServiceManager.Get<MixItUp.Base.Services.Kick.New.KickSession>().IsConnected)
                {
                    var channel = ServiceManager.Get<MixItUp.Base.Services.Kick.New.KickSession>().Channel;
                    if (channel != null)
                    {
                        this.ReplaceSpecialIdentifier(StreamSpecialIdentifierHeader + "subscribercount", channel.ActiveSubscribersCount.ToString());
                        this.ReplaceSpecialIdentifier(StreamSpecialIdentifierHeader + "description", channel.ChannelDescription);
                        this.ReplaceSpecialIdentifier(StreamSpecialIdentifierHeader + "thumbnail", channel.Stream?.Thumbnail);
                        if (channel.Stream?.CustomTags != null && channel.Stream.CustomTags.Count > 0)
                        {
                            this.ReplaceSpecialIdentifier(StreamSpecialIdentifierHeader + "kicktags", string.Join(", ", channel.Stream.CustomTags));
                        }
                    }
                }
                else if (platform == StreamingPlatformTypeEnum.Velora && ServiceManager.Get<MixItUp.Base.Services.Velora.New.VeloraSession>().IsConnected)
                {
                    var veloraSession = ServiceManager.Get<MixItUp.Base.Services.Velora.New.VeloraSession>();
                    this.ReplaceSpecialIdentifier(StreamSpecialIdentifierHeader + "subscribercount", veloraSession.SubscriberCount.ToString());
                    this.ReplaceSpecialIdentifier(StreamSpecialIdentifierHeader + "description", veloraSession.StreamDescription ?? string.Empty);
                    if (veloraSession.StreamTags != null && veloraSession.StreamTags.Count > 0)
                    {
                        this.ReplaceSpecialIdentifier(StreamSpecialIdentifierHeader + "veloratags", string.Join(", ", veloraSession.StreamTags));
                    }
                }
                else if (platform == StreamingPlatformTypeEnum.VPZone && ServiceManager.Get<MixItUp.Base.Services.VPZone.New.VPZoneSession>().IsConnected)
                {
                    var vpzoneSession = ServiceManager.Get<MixItUp.Base.Services.VPZone.New.VPZoneSession>();
                    this.ReplaceSpecialIdentifier(StreamSpecialIdentifierHeader + "subscribercount", vpzoneSession.SubscriberCount.ToString());
                    this.ReplaceSpecialIdentifier(StreamSpecialIdentifierHeader + "description", vpzoneSession.StreamDescription ?? string.Empty);
                    if (vpzoneSession.StreamTags != null && vpzoneSession.StreamTags.Count > 0)
                    {
                        this.ReplaceSpecialIdentifier(StreamSpecialIdentifierHeader + "vpzonetags", string.Join(", ", vpzoneSession.StreamTags));
                    }
                }
                this.ReplaceSpecialIdentifier(StreamSpecialIdentifierHeader + "chattercount", ServiceManager.Get<UserService>().ActiveUserCount.ToString());
            }

            if (this.ContainsSpecialIdentifier(UserSpecialIdentifierHeader))
            {
                await this.HandleUserSpecialIdentifiers(parameters.User, string.Empty);
            }

            if (parameters.Arguments != null)
            {
                for (int i = 0; i < parameters.Arguments.Count(); i++)
                {
                    string currentArgumentSpecialIdentifierHeader = ArgSpecialIdentifierHeader + (i + 1);
                    if (this.ContainsSpecialIdentifier(currentArgumentSpecialIdentifierHeader))
                    {
                        if (this.ContainsSpecialIdentifier(currentArgumentSpecialIdentifierHeader + UserSpecialIdentifierHeader))
                        {
                            UserV2ViewModel argUser = await ServiceManager.Get<UserService>().GetUserByPlatform(platform, platformUsername: parameters.Arguments.ElementAt(i), performPlatformSearch: true);
                            if (argUser != null)
                            {
                                await this.HandleUserSpecialIdentifiers(argUser, currentArgumentSpecialIdentifierHeader);
                            }
                        }

                        this.ReplaceSpecialIdentifier(currentArgumentSpecialIdentifierHeader + "text", parameters.Arguments.ElementAt(i));
                        if (double.TryParse(parameters.Arguments.ElementAt(i), out double result))
                        {
                            this.ReplaceSpecialIdentifier(currentArgumentSpecialIdentifierHeader + "numberdisplay", result.ToNumberDisplayString());
                        }
                    }
                }

                string allArgs = string.Join(" ", parameters.Arguments);
                this.ReplaceSpecialIdentifier("allargs", allArgs);
                this.ReplaceSpecialIdentifier("argcount", parameters.Arguments.Count().ToString());

                if (!string.IsNullOrEmpty(allArgs))
                {
                    if (this.ContainsSpecialIdentifier(ArgDelimitedSpecialIdentifierHeader) || this.ContainsSpecialIdentifier(ArgDelimitedSpecialIdentifierHeader + "count"))
                    {
                        List<string> delimitedArgs = new List<string>(allArgs.Split(new string[] { ChannelSession.Settings.DelimitedArgumentsSeparator }, StringSplitOptions.RemoveEmptyEntries));

                        for (int i = 0; i < delimitedArgs.Count(); i++)
                        {
                            string currentArgumentSpecialIdentifierHeader = ArgDelimitedSpecialIdentifierHeader + (i + 1);
                            this.ReplaceSpecialIdentifier(currentArgumentSpecialIdentifierHeader + "text", delimitedArgs[i].Trim());
                        }

                        this.ReplaceSpecialIdentifier(ArgDelimitedSpecialIdentifierHeader + "count", delimitedArgs.Count.ToString());
                    }
                }

                await this.ReplaceNumberRangeBasedRegexSpecialIdentifier(ArgSpecialIdentifierHeader + SpecialIdentifierNumberRangeRegexPattern + "text", (min, max) =>
                {
                    string result = "";

                    min = min - 1;
                    max = Math.Min(max, parameters.Arguments.Count());
                    int total = max - min;

                    if (total > 0 && min <= parameters.Arguments.Count())
                    {
                        result = string.Join(" ", parameters.Arguments.Skip(min).Take(total));
                    }

                    return Task.FromResult(result);
                });
            }

            if (this.ContainsSpecialIdentifier(TargetSpecialIdentifierHeader))
            {
                await parameters.SetTargetUser();
                await this.HandleUserSpecialIdentifiers(parameters.TargetUser, TargetSpecialIdentifierHeader);
            }

            if (this.ContainsSpecialIdentifier(StreamerSpecialIdentifierHeader))
            {
                await this.HandleUserSpecialIdentifiers(ChannelSession.User, StreamerSpecialIdentifierHeader);
            }

            if (this.ContainsSpecialIdentifier(BotSpecialIdentifierHeader))
            {
                await this.HandleUserSpecialIdentifiers(ChannelSession.Bot, BotSpecialIdentifierHeader);
            }

            if (this.ContainsSpecialIdentifier(OverlayStreamBossV3Model.StreamBossSpecialIdentifierPrefix))
            {
                OverlayWidgetV3Model streamBossWidget = ChannelSession.Settings.OverlayWidgetsV3.FirstOrDefault(w => w.Type == OverlayItemV3Type.StreamBoss);
                if (streamBossWidget != null)
                {
                    OverlayStreamBossV3Model streamBossOverlay = (OverlayStreamBossV3Model)streamBossWidget.Item;
                    if (streamBossOverlay != null)
                    {
                        this.ReplaceSpecialIdentifier(OverlayStreamBossV3Model.StreamBossHealthSpecialIdentifier, streamBossOverlay.CurrentHealth.ToString());

                        UserV2ViewModel streamBossUser = await streamBossOverlay.GetCurrentBoss();
                        await this.HandleUserSpecialIdentifiers(streamBossUser, OverlayStreamBossV3Model.StreamBossSpecialIdentifierPrefix);
                    }
                }
            }

            if (this.ContainsSpecialIdentifier(RandomSpecialIdentifierHeader))
            {
                if (this.ContainsSpecialIdentifier(RandomNumberRegexSpecialIdentifier))
                {
                    await this.ReplaceNumberRangeBasedRegexSpecialIdentifier(RandomNumberRegexSpecialIdentifier + SpecialIdentifierNumberRangeRegexPattern, (min, max) =>
                    {
                        int number = RandomHelper.GenerateRandomNumber(min, max + 1);
                        return Task.FromResult(number.ToString());
                    });

                    await this.ReplaceNumberBasedRegexSpecialIdentifier(RandomNumberRegexSpecialIdentifier + SpecialIdentifierNumberRegexPattern, (maxNumber) =>
                    {
                        int number = RandomHelper.GenerateRandomNumber(maxNumber) + 1;
                        return Task.FromResult(number.ToString());
                    });
                }

                IEnumerable<UserV2ViewModel> workableUsers = ServiceManager.Get<UserService>().GetActiveUsers(excludeSpecialtyExcluded: true);
                if (this.ContainsSpecialIdentifier(SpecialIdentifierStringBuilder.RandomSpecialIdentifierHeader + "user"))
                {
                    if (workableUsers != null && workableUsers.Count() > 0)
                    {
                        await this.HandleUserSpecialIdentifiers(workableUsers.Random(), RandomSpecialIdentifierHeader);
                    }
                }

                if (this.ContainsSpecialIdentifier(SpecialIdentifierStringBuilder.RandomFollowerSpecialIdentifierHeader))
                {
                    IEnumerable<UserV2ViewModel> users = workableUsers.Where(u => u.IsFollower);
                    if (users != null && users.Count() > 0)
                    {
                        await this.HandleUserSpecialIdentifiers(users.Random(), RandomFollowerSpecialIdentifierHeader);
                    }
                }

                if (this.ContainsSpecialIdentifier(SpecialIdentifierStringBuilder.RandomSubscriberSpecialIdentifierHeader))
                {
                    IEnumerable<UserV2ViewModel> users = workableUsers.Where(u => u.IsSubscriber);
                    if (users != null && users.Count() > 0)
                    {
                        await this.HandleUserSpecialIdentifiers(users.Random(), RandomSubscriberSpecialIdentifierHeader);
                    }
                }

                if (this.ContainsRegexSpecialIdentifier(SpecialIdentifierStringBuilder.RandomRegularSpecialIdentifierHeader))
                {
                    IEnumerable<UserV2ViewModel> users = workableUsers.Where(u => u.IsRegular);
                    if (users != null && users.Count() > 0)
                    {
                        await this.HandleUserSpecialIdentifiers(users.Random(), RandomRegularSpecialIdentifierHeader);
                    }
                }

                if (this.ContainsSpecialIdentifier(RandomAnySpecialIdentifierHeader))
                {
                    await ServiceManager.Get<UserService>().LoadAllUserData();
                    IEnumerable<UserV2Model> allUsers = ChannelSession.Settings.Users.Values.Where(u => !u.IsSpecialtyExcluded && u.ID != ChannelSession.User.Model.ID);

                    if (platform != StreamingPlatformTypeEnum.All && platform != StreamingPlatformTypeEnum.None)
                    {
                        allUsers = allUsers.Where(u => u.HasPlatformData(platform));
                    }

                    if (this.ContainsSpecialIdentifier(RandomAnySpecialIdentifierHeader + "user"))
                    {
                        if (allUsers.Any())
                        {
                            UserV2ViewModel randomUser = new UserV2ViewModel(platform, allUsers.Random());
                            await this.HandleUserSpecialIdentifiers(randomUser, RandomAnySpecialIdentifierHeader);
                        }
                    }

                    if (this.ContainsSpecialIdentifier(RandomAnyFollowerSpecialIdentifierHeader))
                    {
                        IEnumerable<UserV2Model> followers = allUsers.Where(u => u.PlatformData.ContainsKey(platform) && u.PlatformData[platform].FollowDate != null);
                        if (followers.Any())
                        {
                            UserV2ViewModel randomUser = new UserV2ViewModel(platform, followers.Random());
                            await this.HandleUserSpecialIdentifiers(randomUser, RandomAnyFollowerSpecialIdentifierHeader);
                        }
                    }

                    if (this.ContainsSpecialIdentifier(RandomAnySubscriberSpecialIdentifierHeader))
                    {
                        IEnumerable<UserV2Model> subscribers = allUsers.Where(u => u.PlatformData.ContainsKey(platform) && u.PlatformData[platform].SubscribeDate != null);
                        if (subscribers.Any())
                        {
                            UserV2ViewModel randomUser = new UserV2ViewModel(platform, subscribers.Random());
                            await this.HandleUserSpecialIdentifiers(randomUser, RandomAnySubscriberSpecialIdentifierHeader);
                        }
                    }

                    if (this.ContainsSpecialIdentifier(RandomAnyRegularSpecialIdentifierHeader))
                    {
                        int regularMinutes = ChannelSession.Settings.RegularUserMinimumHours * 60;
                        IEnumerable<UserV2Model> regulars = allUsers.Where(u => u.OnlineViewingMinutes >= regularMinutes);
                        if (regulars.Any())
                        {
                            UserV2ViewModel randomUser = new UserV2ViewModel(platform, regulars.Random());
                            await this.HandleUserSpecialIdentifiers(randomUser, RandomAnyRegularSpecialIdentifierHeader);
                        }
                    }
                }
            }

            if (ServiceManager.Get<TwitchSession>().IsConnected && this.ContainsSpecialIdentifier(SpecialIdentifierStringBuilder.TwitchSpecialIdentifierHeader))
            {
                if (this.ContainsSpecialIdentifier(SpecialIdentifierStringBuilder.TwitchSpecialIdentifierHeader + "subcount"))
                {
                    long subCount = await ServiceManager.Get<TwitchSession>().StreamerService.GetSubscriberCount(ServiceManager.Get<TwitchSession>().StreamerModel);
                    this.ReplaceSpecialIdentifier(SpecialIdentifierStringBuilder.TwitchSpecialIdentifierHeader + "subcount", subCount.ToString());
                }

                if (this.ContainsSpecialIdentifier(SpecialIdentifierStringBuilder.TwitchSpecialIdentifierHeader + "subpoints"))
                {
                    long subPoints = await ServiceManager.Get<TwitchSession>().StreamerService.GetSubscriberPoints(ServiceManager.Get<TwitchSession>().StreamerModel);
                    this.ReplaceSpecialIdentifier(SpecialIdentifierStringBuilder.TwitchSpecialIdentifierHeader + "subpoints", subPoints.ToString());
                }

                if (ServiceManager.Get<TwitchSession>().AdSchedule != null && this.ContainsSpecialIdentifier(SpecialIdentifierStringBuilder.TwitchSpecialIdentifierHeader + "ad"))
                {
                    AdScheduleModel adSchedule = ServiceManager.Get<TwitchSession>().AdSchedule;
                    DateTimeOffset nextAd = adSchedule.NextAdTimestamp();
                    int nextAdMinutes = adSchedule.NextAdMinutesFromNow();
                    
                    this.ReplaceSpecialIdentifier(SpecialIdentifierStringBuilder.TwitchSpecialIdentifierHeader + "adsnoozecount", adSchedule.snooze_count.ToString());
                    this.ReplaceSpecialIdentifier(SpecialIdentifierStringBuilder.TwitchSpecialIdentifierHeader + "adnextduration", adSchedule.duration.ToString());
                    this.ReplaceSpecialIdentifier(SpecialIdentifierStringBuilder.TwitchSpecialIdentifierHeader + "adnextminutes", nextAdMinutes.ToString());
                    this.ReplaceSpecialIdentifier(SpecialIdentifierStringBuilder.TwitchSpecialIdentifierHeader + "adnexttime", nextAd.ToFriendlyTimeString());
                }

                if (this.ContainsSpecialIdentifier(SpecialIdentifierStringBuilder.TwitchSpecialIdentifierHeader + "cliprandom"))
                {
                    IEnumerable<ClipModel> clips = await ServiceManager.Get<TwitchSession>().StreamerService.GetClips(ServiceManager.Get<TwitchSession>().StreamerModel, maxResults: 100);
                    if (clips != null && clips.Count() > 0)
                    {
                        ClipModel randomClip = clips.Random();
                        this.ReplaceSpecialIdentifier(SpecialIdentifierStringBuilder.TwitchSpecialIdentifierHeader + "cliprandom", randomClip.url);
                    }
                }
            }

            if (ServiceManager.Get<YouTubeSession>().IsConnected && this.ContainsSpecialIdentifier(SpecialIdentifierStringBuilder.YouTubeSpecialIdentifierHeader))
            {
                if (this.ContainsSpecialIdentifier(SpecialIdentifierStringBuilder.YouTubeSpecialIdentifierHeader + "latestvideo"))
                {
                    Video video = await ServiceManager.Get<YouTubeSession>().GetLatestNonStreamVideo();
                    if (video != null)
                    {
                        this.ReplaceSpecialIdentifier(SpecialIdentifierStringBuilder.YouTubeSpecialIdentifierHeader + "latestvideoid", video.Id);
                        this.ReplaceSpecialIdentifier(SpecialIdentifierStringBuilder.YouTubeSpecialIdentifierHeader + "latestvideotitle", video.Snippet.Title);
                        this.ReplaceSpecialIdentifier(SpecialIdentifierStringBuilder.YouTubeSpecialIdentifierHeader + "latestvideourl", $"https://www.youtube.com/watch?v={video.Id}");
                    }
                }

                if (this.ContainsSpecialIdentifier(SpecialIdentifierStringBuilder.YouTubeSpecialIdentifierHeader + "latestshort"))
                {
                    Video video = await ServiceManager.Get<YouTubeSession>().GetLatestShort();
                    if (video != null)
                    {
                        this.ReplaceSpecialIdentifier(SpecialIdentifierStringBuilder.YouTubeSpecialIdentifierHeader + "latestshortid", video.Id);
                        this.ReplaceSpecialIdentifier(SpecialIdentifierStringBuilder.YouTubeSpecialIdentifierHeader + "latestshorttitle", video.Snippet.Title);
                        this.ReplaceSpecialIdentifier(SpecialIdentifierStringBuilder.YouTubeSpecialIdentifierHeader + "latestshorturl", $"https://www.youtube.com/shorts/{video.Id}");
                    }
                }
            }

            await this.HandleLatestSpecialIdentifier(SpecialIdentifierStringBuilder.LatestFollowerUserData);
            await this.HandleLatestSpecialIdentifier(SpecialIdentifierStringBuilder.LatestHostUserData);
            await this.HandleLatestSpecialIdentifier(SpecialIdentifierStringBuilder.LatestRaidUserData, SpecialIdentifierStringBuilder.LatestRaidViewerCountData);
            await this.HandleLatestSpecialIdentifier(SpecialIdentifierStringBuilder.LatestSubscriberUserData, SpecialIdentifierStringBuilder.LatestSubscriberSubMonthsData);
            await this.HandleLatestSpecialIdentifier(SpecialIdentifierStringBuilder.LatestBitsCheeredUserData, SpecialIdentifierStringBuilder.LatestBitsCheeredAmountData);
            await this.HandleLatestSpecialIdentifier(SpecialIdentifierStringBuilder.LatestDonationUserData, SpecialIdentifierStringBuilder.LatestDonationAmountData);

            foreach (InventoryModel inventory in ChannelSession.Settings.Inventory.Values.OrderByDescending(c => c.SpecialIdentifier))
            {
                if (this.ContainsSpecialIdentifier(inventory.RandomItemSpecialIdentifier))
                {
                    this.ReplaceSpecialIdentifier(inventory.RandomItemSpecialIdentifier, inventory.Items.Values.Random().Name);
                }

                if (this.ContainsSpecialIdentifier(inventory.UniqueItemsTotalSpecialIdentifier))
                {
                    this.ReplaceSpecialIdentifier(inventory.UniqueItemsTotalSpecialIdentifier, inventory.Items.Count().ToString());
                }

                if (this.ContainsSpecialIdentifier(inventory.AllItemsSpecialIdentifier))
                {
                    this.ReplaceSpecialIdentifier(inventory.AllItemsSpecialIdentifier, string.Join(", ", inventory.Items.Values.Select(i => i.Name)));
                }
            }

            if (this.ContainsSpecialIdentifier(UnicodeRegexSpecialIdentifier))
            {
                await this.ReplaceNumberBasedRegexSpecialIdentifier(UnicodeRegexSpecialIdentifier + SpecialIdentifierNumberRegexPattern, (number) =>
                {
                    char uChar = (char)number;
                    return Task.FromResult(uChar.ToString());
                });
            }
        }

        public void ReplaceSpecialIdentifier(string identifier, string replacement, bool includeSpecialIdentifierHeader = true)
        {
            replacement = (replacement == null) ? string.Empty : replacement;
            if (this.encode)
            {
                replacement = HttpUtility.UrlEncode(replacement);
            }

            // Both sides are literal text, not a pattern. Counter names reach this unsanitized, so the
            // streamer can put ( or [ into the identifier, and the replacement is routinely whatever a
            // viewer typed, where $& and $$ used to be read as substitution syntax.
            string search = (includeSpecialIdentifierHeader ? SpecialIdentifierHeader : string.Empty) + identifier;
            if (string.IsNullOrEmpty(search))
            {
                return;
            }

            if (this.encodeReplacement == null)
            {
                this.text = this.text.Replace(search, replacement, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                // Resolve nested identifiers before encoding, without treating their output as template code.
                int index = 0;
                while ((index = this.text.IndexOf(search, index, StringComparison.OrdinalIgnoreCase)) >= 0)
                {
                    this.text = this.text.Remove(index, search.Length).Insert(index, replacement);
                    this.replacementCharacters.RemoveRange(index, search.Length);
                    this.replacementCharacters.InsertRange(index, Enumerable.Repeat(true, replacement.Length));
                    index += replacement.Length;
                }
            }
        }

        public bool ContainsSpecialIdentifier(string identifier)
        {
            return this.text.Contains(SpecialIdentifierHeader + identifier, StringComparison.OrdinalIgnoreCase);
        }

        public int GetFirstInstanceOfSpecialIdentifier(string identifier, int startIndex)
        {
            return this.text.IndexOf(SpecialIdentifierHeader + identifier, startIndex, StringComparison.OrdinalIgnoreCase);
        }

        public bool ContainsRegexSpecialIdentifier(string identifier)
        {
            return Regex.IsMatch(this.text, "\\" + SpecialIdentifierHeader + identifier, RegexOptions.IgnoreCase);
        }

        public async Task ReplaceNumberBasedRegexSpecialIdentifier(string regex, Func<int, Task<string>> replacer)
        {
            foreach (Match match in Regex.Matches(this.text, "\\" + SpecialIdentifierHeader + regex))
            {
                string text = new String(match.Value.Where(c => char.IsDigit(c)).ToArray());
                if (int.TryParse(text, out int number))
                {
                    string replacement = await replacer(number);
                    if (replacement != null)
                    {
                        this.ReplaceSpecialIdentifier(match.Value, replacement, includeSpecialIdentifierHeader: false);
                    }
                }
            }
        }

        public async Task ReplaceNumberRangeBasedRegexSpecialIdentifier(string regex, Func<int, int, Task<string>> replacer)
        {
            foreach (Match match in Regex.Matches(this.text, "\\" + SpecialIdentifierHeader + regex))
            {
                string text = new String(match.Value.Where(c => char.IsDigit(c) || c == ':').ToArray());
                string[] splits = text.Split(':');
                if (splits.Length == 2 && int.TryParse(splits[0], out int min) && int.TryParse(splits[1], out int max) && max >= min)
                {
                    string replacement = await replacer(min, max);
                    if (replacement != null)
                    {
                        this.ReplaceSpecialIdentifier(match.Value, replacement, includeSpecialIdentifierHeader: false);
                    }
                }
            }
        }

        public override string ToString()
        {
            if (this.encodeReplacement == null) { return this.text; }
            System.Text.StringBuilder result = new System.Text.StringBuilder();
            for (int start = 0; start < this.text.Length;)
            {
                bool replacement = this.replacementCharacters[start];
                int end = start + 1;
                while (end < this.text.Length && this.replacementCharacters[end] == replacement) { end++; }
                string part = this.text.Substring(start, end - start);
                result.Append(replacement ? this.encodeReplacement(part) : part);
                start = end;
            }
            return result.ToString();
        }

        private async Task HandleUserSpecialIdentifiers(UserV2ViewModel user, string identifierHeader)
        {
            if (user != null && this.ContainsSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader))
            {
                await user.Refresh();

                foreach (CurrencyModel currency in ChannelSession.Settings.Currency.Values.OrderByDescending(c => c.UserAmountSpecialIdentifier))
                {
                    if (this.ContainsSpecialIdentifier(identifierHeader + currency.UserAmountSpecialIdentifier))
                    {
                        if (this.ContainsSpecialIdentifier(identifierHeader + currency.UserPositionSpecialIdentifier))
                        {
                            List<UserV2Model> sortedUsers = (await SpecialIdentifierStringBuilder.GetUserOrderedCurrencyList(currency)).ToList();
                            int index = sortedUsers.IndexOf(user.Model);
                            this.ReplaceSpecialIdentifier(identifierHeader + currency.UserPositionSpecialIdentifier, (index + 1).ToString());
                        }

                        int amount = currency.GetAmount(user);
                        RankModel rank = currency.GetRank(user);
                        RankModel nextRank = currency.GetNextRank(user);

                        this.ReplaceSpecialIdentifier(identifierHeader + currency.UserRankNextNameSpecialIdentifier, nextRank.Name);
                        this.ReplaceSpecialIdentifier(identifierHeader + currency.UserAmountNextDisplaySpecialIdentifier, nextRank.Amount.ToNumberDisplayString());
                        this.ReplaceSpecialIdentifier(identifierHeader + currency.UserAmountNextSpecialIdentifier, nextRank.Amount.ToString());

                        this.ReplaceSpecialIdentifier(identifierHeader + currency.UserRankNameSpecialIdentifier, rank.Name);
                        this.ReplaceSpecialIdentifier(identifierHeader + currency.UserAmountDisplaySpecialIdentifier, amount.ToNumberDisplayString());
                        this.ReplaceSpecialIdentifier(identifierHeader + currency.UserAmountSpecialIdentifier, amount.ToString());
                    }
                }

                foreach (InventoryModel inventory in ChannelSession.Settings.Inventory.Values.OrderByDescending(c => c.UserAmountSpecialIdentifierHeader))
                {
                    if (this.ContainsSpecialIdentifier(identifierHeader + inventory.UserAmountSpecialIdentifierHeader))
                    {
                        Dictionary<string, int> userItems = new Dictionary<string, int>();

                        foreach (InventoryItemModel item in inventory.Items.Values.OrderByDescending(i => i.Name))
                        {
                            var quantity = inventory.GetAmount(user, item);
                            if (quantity > 0)
                            {
                                userItems[item.Name] = quantity;
                            }

                            string itemHeaderSpecialIdentifier = identifierHeader + inventory.UserAmountSpecialIdentifierHeader;

                            this.ReplaceSpecialIdentifier(itemHeaderSpecialIdentifier + item.MaxAmountSpecialIdentifier, item.MaxAmount.ToString());
                            this.ReplaceSpecialIdentifier(itemHeaderSpecialIdentifier + item.ShopBuyPriceSpecialIdentifier, item.BuyAmount.ToString());
                            this.ReplaceSpecialIdentifier(itemHeaderSpecialIdentifier + item.ShopSellPriceSpecialIdentifier, item.SellAmount.ToString());
                            this.ReplaceSpecialIdentifier(itemHeaderSpecialIdentifier + item.SpecialIdentifier, quantity.ToString());
                        }

                        if (userItems.Count > 0)
                        {
                            int totalQuantity = userItems.Values.Sum();

                            this.ReplaceSpecialIdentifier(identifierHeader + inventory.UserUniqueItemsTotalSpecialIdentifier, userItems.Count.ToString());
                            this.ReplaceSpecialIdentifier(identifierHeader + inventory.UserItemsTotalSpecialIdentifier, totalQuantity.ToString());

                            List<string> userAllItems = new List<string>();
                            foreach (var kvp in userItems.OrderBy(i => i.Key))
                            {
                                if (kvp.Value > 0)
                                {
                                    userAllItems.Add(kvp.Key + " x" + kvp.Value);
                                }
                            }
                            this.ReplaceSpecialIdentifier(identifierHeader + inventory.UserAllAmountSpecialIdentifier, string.Join(", ", userAllItems));
                            this.ReplaceSpecialIdentifier(identifierHeader + inventory.UserRandomItemSpecialIdentifier, userItems.Keys.Random());
                        }
                        else
                        {
                            this.ReplaceSpecialIdentifier(identifierHeader + inventory.UserUniqueItemsTotalSpecialIdentifier, userItems.Count.ToString());
                            this.ReplaceSpecialIdentifier(identifierHeader + inventory.UserItemsTotalSpecialIdentifier, userItems.Values.Sum().ToString());

                            this.ReplaceSpecialIdentifier(identifierHeader + inventory.UserAllAmountSpecialIdentifier, Resources.Nothing);
                            this.ReplaceSpecialIdentifier(identifierHeader + inventory.UserRandomItemSpecialIdentifier, Resources.Nothing);
                        }
                    }
                }

                foreach (StreamPassModel streamPass in ChannelSession.Settings.StreamPass.Values.OrderByDescending(c => c.UserAmountSpecialIdentifier))
                {
                    if (this.ContainsSpecialIdentifier(identifierHeader + streamPass.UserAmountSpecialIdentifier))
                    {
                        this.ReplaceSpecialIdentifier(identifierHeader + streamPass.UserLevelSpecialIdentifier, streamPass.GetLevel(user).ToString());
                        this.ReplaceSpecialIdentifier(identifierHeader + streamPass.UserPointsDisplaySpecialIdentifier, streamPass.GetAmount(user).ToNumberDisplayString());
                        this.ReplaceSpecialIdentifier(identifierHeader + streamPass.UserAmountSpecialIdentifier, streamPass.GetAmount(user).ToString());
                    }
                }

                if (this.ContainsSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "gamequeueposition"))
                {
                    string gameQueuePosition = MixItUp.Base.Resources.QueueNotIn;
                    if (ServiceManager.Get<GameQueueService>().IsEnabled)
                    {
                        int position = ServiceManager.Get<GameQueueService>().GetUserPosition(user);
                        if (position > 0)
                        {
                            gameQueuePosition = position.ToString();
                        }
                    }
                    this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "gamequeueposition", gameQueuePosition);
                }

                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "id", user.ID.ToString());
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "name", user.Username);
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "displayname", user.DisplayName);
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "fulldisplayname", user.FullDisplayName);
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "url", user.ChannelLink);
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "avatar", user.AvatarLink);
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "roles", user.RolesString);
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "displayroles", user.DisplayRolesString);
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "primaryrole", user.PrimaryRoleString);
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "title", user.Title);
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "alejopronouns", user.AlejoPronoun);
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "moderationstrikes", user.ModerationStrikes.ToString());
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "color", string.IsNullOrEmpty(user.Color) ? "#000000" : user.Color);
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "inchat", ServiceManager.Get<UserService>().IsUserActive(user.ID).ToString());

                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "time", user.OnlineViewingTimeString);
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "hours", user.OnlineViewingHoursOnly.ToString());
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "mins", user.OnlineViewingMinutesOnly.ToString());
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "accountdays", user.AccountDays.ToString());
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "accountage", user.AccountAgeString);
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "accountdate", user.AccountDateString);
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "followdays", user.FollowDays.ToString());
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "followmonths", user.FollowMonths.ToString());
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "followyears", user.FollowYears.ToString());
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "followage", user.FollowAgeString);
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "followdate", user.FollowDateString);
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "lastseendays", user.LastActivityDays.ToString());
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "lastseenage", user.LastActivityAgeString);
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "lastseendate", user.LastActivityDateString);
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "lastseenprecise", user.LastActivityPreciseDateTimeString);
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "subdays", user.SubscribeDays.ToString());
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "subage", user.SubscribeAgeString);
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "subdate", user.SubscribeDateString);
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "subtier", user.SubscriberTierString);
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "submonths", user.SubscribeMonths.ToString());
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "subbadge", user.PlatformSubscriberBadgeLink);
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "isfollower", user.IsFollower.ToString());
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "isregular", user.IsRegular.ToString());
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "issubscriber", user.IsPlatformSubscriber.ToString());
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "isvip", (user.HasRole(UserRoleEnum.TwitchVIP) || user.HasRole(UserRoleEnum.KickVIP) || user.HasRole(UserRoleEnum.VeloraVIP)).ToString());
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "ismod", user.MeetsRole(UserRoleEnum.Moderator).ToString());
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "isspecialtyexcluded", user.IsSpecialtyExcluded.ToString());
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "notes", user.Notes);

                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "totalstreamswatched", user.TotalStreamsWatched.ToString());
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "totalamountdonated", CurrencyHelper.ToCurrencyString(user.TotalAmountDonated));
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "totalsubsgifted", user.TotalSubsGifted.ToString());
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "totalsubsreceived", user.TotalSubsReceived.ToString());
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "totalchatmessagessent", user.TotalChatMessageSent.ToString());
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "totaltimestagged", user.TotalTimesTagged.ToString());
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "totalcommandsrun", user.TotalCommandsRun.ToString());
                this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "totalmonthssubbed", user.TotalMonthsSubbed.ToString());

                if (user.HasPlatformData(StreamingPlatformTypeEnum.Twitch))
                {
                    TwitchUserPlatformV2Model pUser = user.GetPlatformData<TwitchUserPlatformV2Model>(StreamingPlatformTypeEnum.Twitch);
                    this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "twitchid", pUser?.ID);
                    this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "twitchcolor", pUser?.Color);
                    this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "totalbitscheered", pUser?.TotalBitsCheered.ToString());

                    if (this.ContainsSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "bitslifetimeamount") &&
                        user.Platform == StreamingPlatformTypeEnum.Twitch && ServiceManager.Get<TwitchSession>().IsConnected)
                    {
                        long amount = await ServiceManager.Get<TwitchSession>().StreamerService.GetUserLifetimeBits(user.PlatformID);
                        this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "bitslifetimeamount", amount.ToString());
                    }
                }

                if (user.HasPlatformData(StreamingPlatformTypeEnum.YouTube))
                {
                    YouTubeUserPlatformV2Model pUser = user.GetPlatformData<YouTubeUserPlatformV2Model>(StreamingPlatformTypeEnum.YouTube);
                    this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "youtubeid", pUser?.ID);
                }

                if (user.HasPlatformData(StreamingPlatformTypeEnum.Kick))
                {
                    KickUserPlatformV2Model pUser = user.GetPlatformData<KickUserPlatformV2Model>(StreamingPlatformTypeEnum.Kick);
                    this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "kickid", pUser?.ID);
                }

                if (user.HasPlatformData(StreamingPlatformTypeEnum.Velora))
                {
                    VeloraUserPlatformV2Model pUser = user.GetPlatformData<VeloraUserPlatformV2Model>(StreamingPlatformTypeEnum.Velora);
                    this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "veloraid", pUser?.ID);
                    this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "veloracolor", pUser?.Color);
                }

                if (user.HasPlatformData(StreamingPlatformTypeEnum.VPZone))
                {
                    VPZoneUserPlatformV2Model pUser = user.GetPlatformData<VPZoneUserPlatformV2Model>(StreamingPlatformTypeEnum.VPZone);
                    this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "vpzoneid", pUser?.ID);
                    this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "vpzonecolor", pUser?.Color);
                }

                string userStreamHeader = identifierHeader + UserSpecialIdentifierHeader + "stream";
                if (this.ContainsSpecialIdentifier(userStreamHeader))
                {
                    if (user.Platform == StreamingPlatformTypeEnum.Twitch && ServiceManager.Get<TwitchSession>().IsConnected)
                    {
                        TwitchUserPlatformV2Model twitchUser = user.GetPlatformData<TwitchUserPlatformV2Model>(StreamingPlatformTypeEnum.Twitch);
                        if (twitchUser != null)
                        {
                            MixItUp.Base.Model.Twitch.User.UserModel tUser = await ServiceManager.Get<TwitchSession>().StreamerService.GetNewAPIUserByID(twitchUser.ID);
                            if (tUser != null)
                            {
                                Task<MixItUp.Base.Model.Twitch.Channels.ChannelInformationModel> tChannel = ServiceManager.Get<TwitchSession>().StreamerService.GetChannelInformation(tUser);
                                Task<MixItUp.Base.Model.Twitch.Streams.StreamModel> stream = ServiceManager.Get<TwitchSession>().StreamerService.GetLatestStream(tUser);

                                await Task.WhenAll(tChannel, stream);

                                if (this.ContainsSpecialIdentifier(userStreamHeader + "gameimage"))
                                {
                                    Task<GameModel> game = ServiceManager.Get<TwitchSession>().StreamerService.GetNewAPIGameByID(tChannel.Result?.game_id);
                                    this.ReplaceSpecialIdentifier(userStreamHeader + "gameimage", game.Result?.box_art_url);
                                }

                                this.ReplaceSpecialIdentifier(userStreamHeader + "title", tChannel.Result?.title);
                                this.ReplaceSpecialIdentifier(userStreamHeader + "gamename", tChannel.Result?.game_name);
                                this.ReplaceSpecialIdentifier(userStreamHeader + "game", tChannel.Result?.game_name);
                                this.ReplaceSpecialIdentifier(userStreamHeader + "islive", (stream.Result != null).ToString());
                            }
                        }
                    }
                    else if (user.Platform == StreamingPlatformTypeEnum.YouTube && ServiceManager.Get<YouTubeSession>().IsConnected)
                    {
                        YouTubeUserPlatformV2Model youtubeUser = user.GetPlatformData<YouTubeUserPlatformV2Model>(StreamingPlatformTypeEnum.YouTube);
                        if (youtubeUser != null)
                        {
                            Google.Apis.YouTube.v3.Data.Channel yChannel = await ServiceManager.Get<YouTubeSession>().StreamerService.GetChannelByID(youtubeUser.ID);
                            if (yChannel != null)
                            {

                            }
                        }
                    }
                    else if (user.Platform == StreamingPlatformTypeEnum.Kick && ServiceManager.Get<KickSession>().IsConnected)
                    {
                        KickUserPlatformV2Model kickUser = user.GetPlatformData<KickUserPlatformV2Model>(StreamingPlatformTypeEnum.Kick);
                        if (kickUser != null)
                        {
                            MixItUp.Base.Model.Kick.Channels.ChannelModel kChannel = await ServiceManager.Get<KickSession>().StreamerService.GetChannelByUserID(kickUser.ID);
                            if (kChannel != null)
                            {
                                this.ReplaceSpecialIdentifier(userStreamHeader + "title", kChannel.StreamTitle);
                                this.ReplaceSpecialIdentifier(userStreamHeader + "gamename", kChannel.Category?.Name);
                                this.ReplaceSpecialIdentifier(userStreamHeader + "game", kChannel.Category?.Name);
                                this.ReplaceSpecialIdentifier(userStreamHeader + "gameimage", kChannel.Category?.Thumbnail);
                                this.ReplaceSpecialIdentifier(userStreamHeader + "islive", (kChannel.Stream?.IsLive ?? false).ToString());
                            }
                        }
                    }
                    else if (user.Platform == StreamingPlatformTypeEnum.Velora && ServiceManager.Get<MixItUp.Base.Services.Velora.New.VeloraSession>().IsConnected)
                    {
                        VeloraUserPlatformV2Model veloraUser = user.GetPlatformData<VeloraUserPlatformV2Model>(StreamingPlatformTypeEnum.Velora);

                        MixItUp.Base.Model.Velora.Streams.UserStreamModel vStream = null;
                        if (veloraUser != null && !string.IsNullOrWhiteSpace(veloraUser.Username))
                        {
                            vStream = await ServiceManager.Get<MixItUp.Base.Services.Velora.New.VeloraSession>().StreamerService.GetUserStream(veloraUser.Username);
                        }

                        // A user who has never streamed has no stream record at all, so these are
                        // filled in either way rather than left as literal $... text in the output.
                        this.ReplaceSpecialIdentifier(userStreamHeader + "title", vStream?.Title);
                        this.ReplaceSpecialIdentifier(userStreamHeader + "gamename", vStream?.CategoryName);
                        this.ReplaceSpecialIdentifier(userStreamHeader + "game", vStream?.CategoryName);
                        this.ReplaceSpecialIdentifier(userStreamHeader + "islive", (vStream?.IsLive ?? false).ToString());

                        if (this.ContainsSpecialIdentifier(userStreamHeader + "gameimage"))
                        {
                            // streams/user carries no artwork, so the slug is resolved against category search.
                            this.ReplaceSpecialIdentifier(userStreamHeader + "gameimage", vStream?.CategoryImageUrl
                                ?? await ServiceManager.Get<MixItUp.Base.Services.Velora.New.VeloraSession>().GetCategoryImageUrl(vStream?.CategorySlug, vStream?.CategoryName));
                        }
                    }
                    else if (user.Platform == StreamingPlatformTypeEnum.VPZone && ServiceManager.Get<MixItUp.Base.Services.VPZone.New.VPZoneSession>().IsConnected)
                    {
                        VPZoneUserPlatformV2Model vpzoneUser = user.GetPlatformData<VPZoneUserPlatformV2Model>(StreamingPlatformTypeEnum.VPZone);

                        // VPZone has no per-user stream lookup, so the member's own channel record is
                        // what carries their title, category and live state.
                        MixItUp.Base.Model.VPZone.Streams.VPZoneChannelModel vChannel = null;
                        if (vpzoneUser != null && !string.IsNullOrWhiteSpace(vpzoneUser.Username))
                        {
                            vChannel = await ServiceManager.Get<MixItUp.Base.Services.VPZone.New.VPZoneSession>().StreamerService.GetChannel(vpzoneUser.Username);
                        }

                        // A user who has never streamed has no channel record at all, so these are
                        // filled in either way rather than left as literal $... text in the output.
                        this.ReplaceSpecialIdentifier(userStreamHeader + "title", vChannel?.Title);
                        this.ReplaceSpecialIdentifier(userStreamHeader + "gamename", vChannel?.Category);
                        this.ReplaceSpecialIdentifier(userStreamHeader + "game", vChannel?.Category);
                        this.ReplaceSpecialIdentifier(userStreamHeader + "islive", (vChannel?.IsLive ?? false).ToString());

                        if (this.ContainsSpecialIdentifier(userStreamHeader + "gameimage"))
                        {
                            // The channel record carries no category art, so it comes from the catalog.
                            this.ReplaceSpecialIdentifier(userStreamHeader + "gameimage",
                                (await ServiceManager.Get<MixItUp.Base.Services.VPZone.New.VPZoneSession>().FindCategory(vChannel?.Category))?.CoverUrl);
                        }
                    }
                }

                if (ServiceManager.Get<PatreonService>().IsConnected)
                {
                    string tierName = MixItUp.Base.Resources.NotSubscribed;
                    PatreonTier tier = user.PatreonTier;
                    if (tier != null)
                    {
                        tierName = tier.Title;
                    }
                    this.ReplaceSpecialIdentifier(identifierHeader + UserSpecialIdentifierHeader + "patreontier", tierName);
                }
            }
        }

        private async Task HandleTopBitsCheeredRegex(BitsLeaderboardPeriodEnum period)
        {
            if (ServiceManager.Get<TwitchSession>().IsConnected && this.ContainsRegexSpecialIdentifier(SpecialIdentifierStringBuilder.TopBitsCheeredRegexSpecialIdentifier + period.ToString().ToLower()))
            {
                await this.ReplaceNumberBasedRegexSpecialIdentifier(SpecialIdentifierStringBuilder.TopBitsCheeredRegexSpecialIdentifier + period.ToString().ToLower(), async (total) =>
                {
                    string result = MixItUp.Base.Resources.NoUsersFound;
                    BitsLeaderboardModel leaderboard = await ServiceManager.Get<TwitchSession>().StreamerService.GetBitsLeaderboard(period, total);
                    if (leaderboard != null && leaderboard.users != null && leaderboard.users.Count > 0)
                    {
                        IEnumerable<BitsLeaderboardUserModel> users = leaderboard.users.OrderBy(l => l.rank);

                        List<string> leaderboardsList = new List<string>();
                        int position = 1;
                        for (int i = 0; i < total && i < users.Count(); i++)
                        {
                            BitsLeaderboardUserModel user = users.ElementAt(i);
                            leaderboardsList.Add($"#{i + 1}) {user.user_name} - {user.score}");
                            position++;
                        }

                        if (leaderboardsList.Count > 0)
                        {
                            result = string.Join(", ", leaderboardsList);
                        }
                    }
                    return result;
                });
            }
        }

        private async Task HandleTopBitsCheered(BitsLeaderboardPeriodEnum period)
        {
            if (ServiceManager.Get<TwitchSession>().IsConnected && this.ContainsSpecialIdentifier(SpecialIdentifierStringBuilder.TopBitsCheeredSpecialIdentifier + period.ToString().ToLower()))
            {
                BitsLeaderboardModel leaderboard = await ServiceManager.Get<TwitchSession>().StreamerService.GetBitsLeaderboard(period, 1);
                if (leaderboard != null && leaderboard.users != null && leaderboard.users.Count > 0)
                {
                    BitsLeaderboardUserModel bitsUser = leaderboard.users.OrderBy(u => u.rank).First();

                    this.ReplaceSpecialIdentifier(SpecialIdentifierStringBuilder.TopBitsCheeredSpecialIdentifier + period.ToString().ToLower() + "amount", bitsUser.score.ToString());

                    UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Twitch, platformID: bitsUser.user_id, platformUsername: bitsUser.user_name);
                    if (user == null)
                    {
                        user = UserV2ViewModel.CreateUnassociated(bitsUser.user_name);
                    }
                    await this.HandleUserSpecialIdentifiers(user, SpecialIdentifierStringBuilder.TopBitsCheeredSpecialIdentifier + period.ToString().ToLower());
                }
            }
        }

        private async Task HandleTopKicksGiftedRegex(KicksLeaderboardPeriodEnum period)
        {
            if (ServiceManager.Get<KickSession>().IsConnected && this.ContainsRegexSpecialIdentifier(SpecialIdentifierStringBuilder.TopKicksGiftedRegexSpecialIdentifier + period.ToString().ToLower()))
            {
                await this.ReplaceNumberBasedRegexSpecialIdentifier(SpecialIdentifierStringBuilder.TopKicksGiftedRegexSpecialIdentifier + period.ToString().ToLower(), async (total) =>
                {
                    string result = MixItUp.Base.Resources.NoUsersFound;
                    LeaderboardModel leaderboard = await ServiceManager.Get<KickSession>().StreamerService.GetKicksLeaderboard(total);
                    if (leaderboard != null)
                    {
                        List<LeaderboardEntryModel> entries = null;
                        switch (period)
                        {
                            case KicksLeaderboardPeriodEnum.Lifetime: entries = leaderboard.Lifetime; break;
                            case KicksLeaderboardPeriodEnum.Month: entries = leaderboard.Month; break;
                            case KicksLeaderboardPeriodEnum.Week: entries = leaderboard.Week; break;
                        }

                        if (entries != null && entries.Count > 0)
                        {
                            IEnumerable<LeaderboardEntryModel> sorted = entries.OrderBy(e => e.Rank);

                            List<string> leaderboardsList = new List<string>();
                            int position = 1;
                            for (int i = 0; i < total && i < sorted.Count(); i++)
                            {
                                LeaderboardEntryModel entry = sorted.ElementAt(i);
                                leaderboardsList.Add($"#{i + 1}) {entry.Username} - {entry.GiftedAmount}");
                                position++;
                            }

                            if (leaderboardsList.Count > 0)
                            {
                                result = string.Join(", ", leaderboardsList);
                            }
                        }
                    }
                    return result;
                });
            }
        }

        private async Task HandleTopKicksGifted(KicksLeaderboardPeriodEnum period)
        {
            if (ServiceManager.Get<KickSession>().IsConnected && this.ContainsSpecialIdentifier(SpecialIdentifierStringBuilder.TopKicksGiftedSpecialIdentifier + period.ToString().ToLower()))
            {
                LeaderboardModel leaderboard = await ServiceManager.Get<KickSession>().StreamerService.GetKicksLeaderboard(1);
                if (leaderboard != null)
                {
                    List<LeaderboardEntryModel> entries = null;
                    switch (period)
                    {
                        case KicksLeaderboardPeriodEnum.Lifetime: entries = leaderboard.Lifetime; break;
                        case KicksLeaderboardPeriodEnum.Month: entries = leaderboard.Month; break;
                        case KicksLeaderboardPeriodEnum.Week: entries = leaderboard.Week; break;
                    }

                    if (entries != null && entries.Count > 0)
                    {
                        LeaderboardEntryModel kicksUser = entries.OrderBy(e => e.Rank).First();

                        this.ReplaceSpecialIdentifier(SpecialIdentifierStringBuilder.TopKicksGiftedSpecialIdentifier + period.ToString().ToLower() + "amount", kicksUser.GiftedAmount.ToString());

                        UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByPlatform(StreamingPlatformTypeEnum.Kick, platformID: kicksUser.UserID.ToString(), platformUsername: kicksUser.Username);
                        if (user == null)
                        {
                            user = UserV2ViewModel.CreateUnassociated(kicksUser.Username);
                        }
                        await this.HandleUserSpecialIdentifiers(user, SpecialIdentifierStringBuilder.TopKicksGiftedSpecialIdentifier + period.ToString().ToLower());
                    }
                }
            }
        }

        private static IEnumerable<UserV2Model> OrderUsersForLeaderboard(IEnumerable<UserV2Model> users, Func<UserV2Model, long> selector)
        {
            return users.Where(u => u.GetPlatforms().Count > 0 && selector(u) > 0).OrderByDescending(selector);
        }

        private static IEnumerable<Tuple<UserV2Model, DateTimeOffset>> OrderUsersBySubscribeStreak(IEnumerable<UserV2Model> users)
        {
            return users.Where(u => u.GetPlatforms().Count > 0)
                .Select(u => new Tuple<UserV2Model, DateTimeOffset?>(u, u.GetEarliestSubscribeDate()))
                .Where(u => u.Item2.HasValue)
                .Select(u => new Tuple<UserV2Model, DateTimeOffset>(u.Item1, u.Item2.Value))
                .OrderBy(u => u.Item2);
        }

        private string BuildLeaderboardList(IEnumerable<Tuple<UserV2Model, string>> entries, int total)
        {
            List<string> results = new List<string>();
            int position = 1;
            foreach (var entry in entries.Take(total))
            {
                results.Add($"#{position}) {new UserV2ViewModel(entry.Item1).Username} - {entry.Item2}");
                position++;
            }
            return (results.Count > 0) ? string.Join(", ", results) : MixItUp.Base.Resources.NoUsersFound;
        }

        private UserV2ViewModel GetTopUserViewModel(CommandParametersModel parameters, UserV2Model userData)
        {
            UserV2ViewModel user = ServiceManager.Get<UserService>().GetActiveUserByID(parameters.Platform, userData.ID);
            return user ?? new UserV2ViewModel(userData);
        }

        private async Task HandleTopSubscribersRegex()
        {
            await this.ReplaceNumberBasedRegexSpecialIdentifier(SpecialIdentifierStringBuilder.TopSubscribersRegexSpecialIdentifier, async (total) =>
            {
                IEnumerable<UserV2Model> applicableUsers = await SpecialIdentifierStringBuilder.GetAllNonExemptUsers();
                return this.BuildLeaderboardList(OrderUsersForLeaderboard(applicableUsers, u => u.TotalMonthsSubbed)
                    .Select(u => new Tuple<UserV2Model, string>(u, ((double)u.TotalMonthsSubbed).ToNumberDisplayString())), total);
            });
        }

        private async Task HandleTopSubscriber(CommandParametersModel parameters)
        {
            IEnumerable<UserV2Model> applicableUsers = await SpecialIdentifierStringBuilder.GetAllNonExemptUsers();
            UserV2Model topUserData = OrderUsersForLeaderboard(applicableUsers, u => u.TotalMonthsSubbed).FirstOrDefault();
            if (topUserData != null)
            {
                this.ReplaceSpecialIdentifier(SpecialIdentifierStringBuilder.TopSubscribersSpecialIdentifier + "amount", topUserData.TotalMonthsSubbed.ToString());
                await this.HandleUserSpecialIdentifiers(this.GetTopUserViewModel(parameters, topUserData), SpecialIdentifierStringBuilder.TopSubscribersSpecialIdentifier);
            }
        }

        private async Task HandleTopSubGiftersRegex()
        {
            await this.ReplaceNumberBasedRegexSpecialIdentifier(SpecialIdentifierStringBuilder.TopSubGiftersRegexSpecialIdentifier, async (total) =>
            {
                IEnumerable<UserV2Model> applicableUsers = await SpecialIdentifierStringBuilder.GetAllNonExemptUsers();
                return this.BuildLeaderboardList(OrderUsersForLeaderboard(applicableUsers, u => u.TotalSubsGifted)
                    .Select(u => new Tuple<UserV2Model, string>(u, ((double)u.TotalSubsGifted).ToNumberDisplayString())), total);
            });
        }

        private async Task HandleTopSubGifter(CommandParametersModel parameters)
        {
            IEnumerable<UserV2Model> applicableUsers = await SpecialIdentifierStringBuilder.GetAllNonExemptUsers();
            UserV2Model topUserData = OrderUsersForLeaderboard(applicableUsers, u => u.TotalSubsGifted).FirstOrDefault();
            if (topUserData != null)
            {
                this.ReplaceSpecialIdentifier(SpecialIdentifierStringBuilder.TopSubGiftersSpecialIdentifier + "amount", topUserData.TotalSubsGifted.ToString());
                await this.HandleUserSpecialIdentifiers(this.GetTopUserViewModel(parameters, topUserData), SpecialIdentifierStringBuilder.TopSubGiftersSpecialIdentifier);
            }
        }

        private async Task HandleTopSubStreaksRegex()
        {
            await this.ReplaceNumberBasedRegexSpecialIdentifier(SpecialIdentifierStringBuilder.TopSubStreakRegexSpecialIdentifier, async (total) =>
            {
                IEnumerable<UserV2Model> applicableUsers = await SpecialIdentifierStringBuilder.GetAllNonExemptUsers();
                return this.BuildLeaderboardList(OrderUsersBySubscribeStreak(applicableUsers)
                    .Select(u => new Tuple<UserV2Model, string>(u.Item1, u.Item2.GetAge())), total);
            });
        }

        private async Task HandleTopSubStreak(CommandParametersModel parameters)
        {
            IEnumerable<UserV2Model> applicableUsers = await SpecialIdentifierStringBuilder.GetAllNonExemptUsers();
            Tuple<UserV2Model, DateTimeOffset> top = OrderUsersBySubscribeStreak(applicableUsers).FirstOrDefault();
            if (top != null)
            {
                this.ReplaceSpecialIdentifier(SpecialIdentifierStringBuilder.TopSubStreakSpecialIdentifier + "length", top.Item2.GetAge());
                this.ReplaceSpecialIdentifier(SpecialIdentifierStringBuilder.TopSubStreakSpecialIdentifier + "amount", top.Item2.TotalMonthsFromNow().ToString());
                await this.HandleUserSpecialIdentifiers(this.GetTopUserViewModel(parameters, top.Item1), SpecialIdentifierStringBuilder.TopSubStreakSpecialIdentifier);
            }
        }

        private async Task HandleLatestSpecialIdentifier(string userkey, string extrakey = null)
        {
            if (this.ContainsSpecialIdentifier(extrakey) && !string.IsNullOrEmpty(extrakey) && ChannelSession.Settings.LatestSpecialIdentifiersData.ContainsKey(extrakey) && ChannelSession.Settings.LatestSpecialIdentifiersData[extrakey] != null)
            {
                this.ReplaceSpecialIdentifier(extrakey, ChannelSession.Settings.LatestSpecialIdentifiersData[extrakey].ToString());
            }

            if (this.ContainsSpecialIdentifier(userkey) && ChannelSession.Settings.LatestSpecialIdentifiersData.ContainsKey(userkey) && ChannelSession.Settings.LatestSpecialIdentifiersData[userkey] != null)
            {
                if (Guid.TryParse(ChannelSession.Settings.LatestSpecialIdentifiersData[userkey].ToString(), out Guid userID))
                {
                    UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByID(StreamingPlatformTypeEnum.All, userID);
                    if (user != null)
                    {
                        await this.HandleUserSpecialIdentifiers(user, userkey);
                    }
                }
            }
        }
    }
}

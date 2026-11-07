using MixItUp.Base.Model.Actions;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.Commands.Games;
using MixItUp.Base.Model.Currency;
using MixItUp.Base.Model.Overlay;
using MixItUp.Base.Model.Overlay.Widgets;
using MixItUp.Base.Model.Requirements;
using MixItUp.Base.Model.Serial;
using MixItUp.Base.Model.User;
using MixItUp.Base.Model.Web;
using MixItUp.Base.Services;
using MixItUp.Base.Services.External;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Dashboard;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace MixItUp.Base.Model.Settings
{
    public enum ChatEmoteAnimationEnum
    {
        None = 0,
        Short = 1,
        Loop = 2,
    }

    [DataContract]
    public class SettingsV3Model
    {
        public const int LatestVersion = 8;

        public const string SettingsDirectoryName = "Settings";
        public const string DefaultAutomaticBackupSettingsDirectoryName = "AutomaticBackups";

        public const string SettingsTemplateDatabaseFileName = "SettingsTemplateDatabase.db";

        public const string SettingsFileExtension = "miu3";
        public const string DatabaseFileExtension = "db3";
        public const string SettingsLocalBackupFileExtension = "backup";

        public const string SettingsBackupFileExtension = "miubackup";

        public static async Task RestoreSettingsBackup()
        {
            if (await DialogHelper.ShowConfirmation(Resources.RestoreSettingsConfirmation))
            {
                string filePath = ServiceManager.Get<IFileService>().ShowOpenFileDialog(string.Format("Mix It Up Settings V2 Backup (*.{0})|*.{0}|All files (*.*)|*.*", SettingsV3Model.SettingsBackupFileExtension));
                if (!string.IsNullOrEmpty(filePath))
                {
                    Result<SettingsV3Model> result = await ServiceManager.Get<SettingsService>().RestorePackagedBackup(filePath);
                    if (result.Success)
                    {
                        ChannelSession.AppSettings.SettingsRestoreFilePath = filePath;
                        ChannelSession.AppSettings.SettingsToReplaceDuringRestore = (ChannelSession.Settings != null) ? ChannelSession.Settings.ID : Guid.Empty;
                        ChannelSession.RestartRequested();
                    }
                    else
                    {
                        await DialogHelper.ShowMessage(result.Message);
                    }
                }
            }
        }

        [JsonExtensionData]
        private IDictionary<string, Newtonsoft.Json.Linq.JToken> AdditionalSettings { get; set; }

        [DataMember]
        public int Version { get; set; } = SettingsV3Model.LatestVersion;

        [DataMember]
        public Guid ID { get; set; } = Guid.NewGuid();
        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public string TelemetryUserID { get; set; }

        [DataMember]
        public string SettingsBackupLocation { get; set; }
        [DataMember]
        public SettingsBackupRateEnum SettingsBackupRate { get; set; }
        [DataMember]
        public DateTimeOffset SettingsLastBackup { get; set; }
        [DataMember]
        public DateTimeOffset BackupWarningLastShown { get; set; } = DateTimeOffset.MinValue;

        #region Authentication

        [DataMember]
        public Dictionary<StreamingPlatformTypeEnum, StreamingPlatformAuthenticationSettingsModel> StreamingPlatformAuthentications { get; set; } = new Dictionary<StreamingPlatformTypeEnum, StreamingPlatformAuthenticationSettingsModel>();

        [DataMember]
        public TwitchAuthenticationMigrationModel TwitchAuthenticationMigration { get; set; }

        [DataMember]
        public OAuthTokenModel StreamlabsOAuthToken { get; set; }
        [DataMember]
        public OAuthTokenModel StreamElementsOAuthToken { get; set; }
        [DataMember]
        public OAuthTokenModel DiscordOAuthToken { get; set; }
        [DataMember]
        public OAuthTokenModel TiltifyOAuthToken { get; set; }
        [DataMember]
        public OAuthTokenModel TipeeeStreamOAuthToken { get; set; }
        [DataMember]
        public OAuthTokenModel TreatStreamOAuthToken { get; set; }
        [DataMember]
        public OAuthTokenModel PatreonOAuthToken { get; set; }
        [DataMember]
        public OAuthTokenModel IFTTTOAuthToken { get; set; }
        [DataMember]
        public OAuthTokenModel StreamlootsOAuthToken { get; set; }
        [DataMember]
        public OAuthTokenModel RainMakerOAuthToken { get; set; }
        [DataMember]
        public OAuthTokenModel PixelChatOAuthToken { get; set; }
        [DataMember]
        public OAuthTokenModel VTubeStudioOAuthToken { get; set; }
        [DataMember]
        [Obsolete]
        public OAuthTokenModel InfiniteAlbumOAuthToken { get; set; }
        [DataMember]
        public OAuthTokenModel TITSOAuthToken { get; set; }
        [DataMember]
        public OAuthTokenModel LumiaStreamOAuthToken { get; set; }
        [DataMember]
        public OAuthTokenModel PulsoidOAuthToken { get; set; }
        [DataMember]
        public OAuthTokenModel PallyOAuthToken { get; set; }
        [DataMember]
        public bool EnableVoicemodStudio { get; set; }
        [DataMember]
        public bool EnableCrowdControl { get; set; }
        [DataMember]
        public bool EnableSAMMI { get; set; }
        [DataMember]
        public int SAMMIPortNumber { get; set; } = 9450;
        [DataMember]
        public OAuthTokenModel TTSMonsterOAuthToken { get; set; }
        [DataMember]
        public OAuthTokenModel TTSMonsterAPIOAuthToken { get; set; }

        #endregion Authentication

        #region General

        [DataMember]
        public bool OptOutTracking { get; set; }
        [DataMember]
        public bool FeatureMe { get; set; }
        [DataMember]
        public StreamingPlatformTypeEnum DefaultStreamingPlatform { get; set; } = StreamingPlatformTypeEnum.None;
        [DataMember]
        public StreamingSoftwareTypeEnum DefaultStreamingSoftware { get; set; } = StreamingSoftwareTypeEnum.OBSStudio;
        [DataMember]
        public string DefaultAudioOutput { get; set; }

        #endregion General

        #region Chat

        [DataMember]
        public int MaxMessagesInChat { get; set; } = 100;
        [DataMember]
        public int MaxUsersShownInChat { get; set; } = 100;

        [DataMember]
        public bool SaveChatEventLogs { get; set; }

        /// <summary>
        /// Chat message font size, in device-independent pixels. Named so the theme reset resolves
        /// to the same value this property is initialised with.
        /// </summary>
        public const int DefaultChatFontSize = 13;

        [DataMember]
        public int ChatFontSize { get; set; } = DefaultChatFontSize;
        [DataMember]
        public bool AddSeparatorsBetweenMessages { get; set; }
        [DataMember]
        public bool UseAlternatingBackgroundColors { get; set; }
        [DataMember]
        public ChatEmoteAnimationEnum ChatEmoteAnimation { get; set; } = ChatEmoteAnimationEnum.None;

        [DataMember]
        public bool OnlyShowAlertsInDashboard { get; set; }
        [DataMember]
        public bool LatestChatAtTop { get; set; }
        [DataMember]
        public bool TrackWhispererNumber { get; set; }
        [DataMember]
        public bool ShowChatMessageTimestamps { get; set; }

        [DataMember]
        public bool HideViewerAndChatterNumbers { get; set; }
        [DataMember]
        public bool HideChatUserList { get; set; }
        [DataMember]
        public bool HideDeletedMessages { get; set; }
        [DataMember]
        public bool HideBotMessages { get; set; }
        [DataMember]
        public bool HideWhisperMessages { get; set; }
        [DataMember]
        public HashSet<string> HideSpecificUserMessages { get; set; } = new HashSet<string>();

        [DataMember]
        public bool ShowAlejoPronouns { get; set; }
        [DataMember]
        public bool ShowBetterTTVEmotes { get; set; }
        [DataMember]
        public bool ShowFrankerFaceZEmotes { get; set; }

        [DataMember]
        public bool HideUserAvatar { get; set; }
        [DataMember]
        public bool HideUserRoleBadge { get; set; }
        [DataMember]
        public bool HideUserSubscriberBadge { get; set; }
        [DataMember]
        public bool HideUserSpecialtyBadge { get; set; }

        [DataMember]
        public bool UseCustomUsernameColors { get; set; }
        [DataMember]
        public Dictionary<UserRoleEnum, string> CustomUsernameRoleColors { get; set; } = new Dictionary<UserRoleEnum, string>();

        #endregion Chat

        #region Commands

        [DataMember]
        public bool AllowCommandWhispering { get; set; }
        [DataMember]
        public bool IgnoreBotAccountCommands { get; set; }
        [DataMember]
        public bool DeleteChatCommandsWhenRun { get; set; }
        [DataMember]
        [Obsolete]
        public bool UnlockAllCommands { get; set; }
        [DataMember]
        public CommandServiceLockTypeEnum CommandServiceLockType { get; set; } = CommandServiceLockTypeEnum.PerCommandType;
        [DataMember]
        public bool AlwaysUseCommandLocksWhenTestingCommands { get; set; }
        [DataMember]
        public int MassGiftedSubsFilterAmount { get; set; } = 1;
        [DataMember]
        public bool UserEntranceCommandsOnlyWhenLive { get; set; } = false;

        // YouTube gives us no way to ask whether an upload is a Short, so length is the dividing line.
        // Anything at or under this is treated as a Short, anything longer as a regular video.
        [DataMember]
        public int YouTubeShortsVideoLengthCap { get; set; } = 180;

        // The channel owner's uploads include private, unlisted, and scheduled videos, none of which an
        // audience can watch. Off, the latest video identifiers report those too.
        [DataMember]
        public bool YouTubeOnlyPublicVideos { get; set; } = true;

        [DataMember]
        public RequirementErrorCooldownTypeEnum RequirementErrorsCooldownType { get; set; } = RequirementErrorCooldownTypeEnum.Default;
        [DataMember]
        public int RequirementErrorsCooldownAmount { get; set; } = 10;
        [DataMember]
        public bool IncludeUsernameWithRequirementErrors { get; set; }
        [DataMember]
        public string DelimitedArgumentsSeparator { get; set; } = "|";

        [Obsolete]
        [DataMember]
        public int TwitchMassGiftedSubsFilterAmount { get; set; } = 1;
        [DataMember]
        public bool TwitchReplyToCommandChatMessages { get; set; }
        [DataMember]
        public bool TwitchSlashMeForAllChatMessages { get; set; }
        [DataMember]
        public int TwitchUpcomingAdCommandTriggerAmount { get; set; } = 5;

        [DataMember]
        public string PythonExecutablePath { get; set; }

        [DataMember]
        public bool HideScriptActionChatErrors { get; set; }

        [DataMember]
        public GameInfoLookupOrderEnum GameInfoLookupOrder { get; set; } = GameInfoLookupOrderEnum.XboxThenSteam;

        [DataMember]
        public HashSet<ActionTypeEnum> ActionsToHide { get; set; } = new HashSet<ActionTypeEnum>();

        [DataMember]
        public HashSet<StreamingPlatformTypeEnum> PlatformsToHide { get; set; } = new HashSet<StreamingPlatformTypeEnum>();

        [DataMember]
        public Dictionary<string, CommandGroupSettingsModel> CommandGroups { get; set; } = new Dictionary<string, CommandGroupSettingsModel>();
        [DataMember]
        public Dictionary<string, int> CooldownGroupAmounts { get; set; } = new Dictionary<string, int>();

        [DataMember]
        public List<PreMadeChatCommandSettingsModel> PreMadeChatCommandSettings { get; set; } = new List<PreMadeChatCommandSettingsModel>();

        #endregion Commands

        #region Alerts

        [DataMember]
        public string AlertUserJoinLeaveColor { get; set; }
        [DataMember]
        public string AlertUserFirstMessageColor { get; set; }
        [DataMember]
        public string AlertFollowColor { get; set; }
        [DataMember]
        public string AlertHostColor { get; set; }
        [DataMember]
        public string AlertRaidColor { get; set; }
        [DataMember]
        public string AlertSubColor { get; set; }
        [DataMember]
        public string AlertGiftedSubColor { get; set; }
        [DataMember]
        public string AlertMassGiftedSubColor { get; set; }
        [DataMember]
        public string AlertTwitchBitsCheeredColor { get; set; }
        [DataMember]
        public string AlertTwitchChannelPointsColor { get; set; }
        [DataMember]
        public string AlertTwitchCustomPowerUpsColor { get; set; }
        [DataMember]
        public string AlertTwitchHypeTrainColor { get; set; }
        [DataMember]
        public string AlertTwitchAdsColor { get; set; }
        [DataMember]
        public string AlertTwitchWatchStreakColor { get; set; }
        [DataMember]
        public string AlertTwitchModiversaryColor { get; set; }
        [DataMember]
        public string AlertYouTubeSuperChatColor { get; set; }
        [DataMember]
        public string AlertYouTubeJewelsGiftColor { get; set; }
        [DataMember]
        public string AlertDonationColor { get; set; }
        [DataMember]
        public string AlertModerationColor { get; set; }
        [DataMember]
        public string AlertStreamlootsColor { get; set; }
        [DataMember]
        public string AlertKickChannelPointsColor { get; set; }
        [DataMember]
        public string AlertKickKicksColor { get; set; }
        [DataMember]
        public string AlertVeloraChannelPointsColor { get; set; }
        [DataMember]
        public string AlertVeloraCheeredColor { get; set; }
        [DataMember]
        public string AlertVPZoneChannelPointsColor { get; set; }
        [DataMember]
        public string AlertVPZoneCheeredColor { get; set; }
        [DataMember]
        public string AlertTwitchUserWarnedColor { get; set; }
        [DataMember]
        public string AlertTwitchShoutoutReceivedColor { get; set; }
        [DataMember]
        public string AlertTwitchSuspiciousUserMessageColor { get; set; }
        [DataMember]
        public string AlertTwitchSuspiciousUserUpdatedColor { get; set; }
        [DataMember]
        public string AlertTwitchShieldModeStartedColor { get; set; }
        [DataMember]
        public string AlertTwitchShieldModeEndedColor { get; set; }
        [DataMember]
        public string AlertTwitchGoalStartedColor { get; set; }
        [DataMember]
        public string AlertTwitchGoalEndedColor { get; set; }

        [Obsolete]
        [DataMember]
        public string AlertBitsCheeredColor { get; set; }
        [Obsolete]
        [DataMember]
        public string AlertChannelPointsColor { get; set; }
        [Obsolete]
        [DataMember]
        public string AlertHypeTrainColor { get; set; }

        #endregion Alerts

        #region Notifications

        [DataMember]
        public string NotificationsAudioOutput { get; set; }
        [DataMember]
        public int NotificationCooldownAmount { get; set; }
        [JsonIgnore]
        public DateTimeOffset NotificationLastTrigger { get; set; } = DateTimeOffset.MinValue;

        [DataMember]
        public string NotificationChatMessageSoundFilePath { get; set; }
        [DataMember]
        public int NotificationChatMessageSoundVolume { get; set; } = 100;
        [DataMember]
        public string NotificationChatTaggedSoundFilePath { get; set; }
        [DataMember]
        public int NotificationChatTaggedSoundVolume { get; set; } = 100;
        [DataMember]
        public string NotificationChatWhisperSoundFilePath { get; set; }
        [DataMember]
        public int NotificationChatWhisperSoundVolume { get; set; } = 100;
        [DataMember]
        public string NotificationServiceConnectSoundFilePath { get; set; }
        [DataMember]
        public int NotificationServiceConnectSoundVolume { get; set; } = 100;
        [DataMember]
        public string NotificationServiceDisconnectSoundFilePath { get; set; }
        [DataMember]
        public int NotificationServiceDisconnectSoundVolume { get; set; } = 100;

        #endregion Notifications

        #region Users

        [DataMember]
        public int RegularUserMinimumHours { get; set; }
        [DataMember]
        public bool ExplicitUserRoleRequirements { get; set; }
        [DataMember]
        public List<UserTitleModel> UserTitles { get; set; } = new List<UserTitleModel>();

        #endregion Users

        #region Game Queue

        [DataMember]
        public bool GameQueueSubPriority { get; set; }
        [DataMember]
        public Guid GameQueueUserJoinedCommandID { get; set; }
        [DataMember]
        public Guid GameQueueUserSelectedCommandID { get; set; }

        #endregion Game Queue

        #region Quotes

        [DataMember]
        public bool QuotesEnabled { get; set; }
        [DataMember]
        public string QuotesFormat { get; set; }
        [DataMember]
        public Guid QuoteAddedCommandID { get; set; }

        #endregion Quotes

        #region Timers

        [DataMember]
        public int TimerCommandsInterval { get; set; } = 10;
        [DataMember]
        public int TimerCommandsMinimumMessages { get; set; } = 10;
        [DataMember]
        public bool RunTimersOnlyWhenLive { get; set; } = true;
        [DataMember]
        public bool RandomizeTimers { get; set; }
        [DataMember]
        public bool DisableAllTimers { get; set; }

        #endregion Timers

        #region Giveaway

        [DataMember]
        public string GiveawayCommand { get; set; } = "giveaway";
        [DataMember]
        public int GiveawayTimer { get; set; } = 1;
        [DataMember]
        public int GiveawayMaximumEntries { get; set; } = 1;
        [DataMember]
        public RequirementsSetModel GiveawayRequirementsSet { get; set; } = new RequirementsSetModel();
        [DataMember]
        public int GiveawayReminderInterval { get; set; } = 5;
        [DataMember]
        public bool GiveawayRequireClaim { get; set; } = true;
        [DataMember]
        public bool GiveawayAllowPastWinners { get; set; }

        [DataMember]
        public Guid GiveawayStartedReminderCommandID { get; set; }
        [DataMember]
        public Guid GiveawayUserJoinedCommandID { get; set; }
        [DataMember]
        public Guid GiveawayWinnerSelectedCommandID { get; set; }

        #endregion Giveaway

        #region Moderation

        [DataMember]
        public bool ModerationUseCommunityFilteredWords { get; set; }
        [DataMember]
        public List<string> FilteredWords { get; set; } = new List<string>();
        [DataMember]
        public List<string> BannedWords { get; set; } = new List<string>();

        [DataMember]
        public int ModerationFilteredWordsTimeout1MinuteOffenseCount { get; set; }
        [DataMember]
        public int ModerationFilteredWordsTimeout5MinuteOffenseCount { get; set; }
        [DataMember]
        public UserRoleEnum ModerationFilteredWordsExcemptUserRole { get; set; } = UserRoleEnum.Moderator;
        [DataMember]
        public bool ModerationFilteredWordsApplyStrikes { get; set; } = true;

        [DataMember]
        public int ModerationCapsBlockCount { get; set; }
        [DataMember]
        public bool ModerationCapsBlockIsPercentage { get; set; } = true;
        [DataMember]
        public int ModerationPunctuationBlockCount { get; set; }
        [DataMember]
        public bool ModerationPunctuationBlockIsPercentage { get; set; } = true;
        [DataMember]
        public UserRoleEnum ModerationChatTextExcemptUserRole { get; set; } = UserRoleEnum.Moderator;
        [DataMember]
        public bool ModerationChatTextApplyStrikes { get; set; } = true;

        [DataMember]
        public bool ModerationBlockLinks { get; set; }
        [DataMember]
        public UserRoleEnum ModerationBlockLinksExcemptUserRole { get; set; } = UserRoleEnum.Moderator;
        [DataMember]
        public bool ModerationBlockLinksApplyStrikes { get; set; } = true;

        [DataMember]
        public ModerationChatInteractiveParticipationEnum ModerationChatInteractiveParticipation { get; set; } = ModerationChatInteractiveParticipationEnum.None;
        [DataMember]
        public UserRoleEnum ModerationChatInteractiveParticipationExcemptUserRole { get; set; } = UserRoleEnum.Moderator;

        [DataMember]
        public bool ModerationFollowEvent { get; set; }
        [DataMember]
        public int ModerationFollowEventMaxInQueue { get; set; } = 10;

        [DataMember]
        public bool ModerationResetStrikesOnLaunch { get; set; }

        [DataMember]
        public Guid ModerationStrike1CommandID { get; set; }
        [DataMember]
        public Guid ModerationStrike2CommandID { get; set; }
        [DataMember]
        public Guid ModerationStrike3CommandID { get; set; }

        #endregion Moderation

        #region Overlay

        [DataMember]
        public bool EnableOverlay { get; set; }
        [DataMember]
        public int OverlayPortNumber { get; set; } = OverlayV3Service.DefaultOverlayPort;
        [DataMember]
        public string OverlaySourceName { get; set; }
        [DataMember]
        public bool DisableOverlayIFrameRemoval { get; set; }
        [DataMember]
        public List<OverlayEndpointV3Model> OverlayEndpointsV3 { get; set; } = new List<OverlayEndpointV3Model>();
        [DataMember]
        public List<OverlayWidgetV3Model> OverlayWidgetsV3 { get; set; } = new List<OverlayWidgetV3Model>();

        [DataMember]
        [Obsolete]
        public Dictionary<string, int> OverlayCustomNameAndPorts { get; set; } = new Dictionary<string, int>();
        [DataMember]
        [Obsolete]
        public List<OverlayWidgetModel> OverlayWidgets { get; set; } = new List<OverlayWidgetModel>();

        #endregion Overlay

        #region Services

        [DataMember]
        [Obsolete]
        public string OvrStreamServerIP { get; set; }

        [DataMember]
        public string OBSStudioServerIP { get; set; }
        [DataMember]
        public string OBSStudioServerPassword { get; set; }
        [DataMember]
        public bool OBSStudioDisableAutomaticReplayBuffer { get; set; }

        [DataMember]
        public bool EnableStreamlabsOBSConnection { get; set; }

        [DataMember]
        public bool EnableXSplitConnection { get; set; }

        [DataMember]
        public bool EnableDeveloperAPI { get; set; }
        [DataMember]
        public bool EnableDeveloperAPIAdvancedMode { get; set; }

        [DataMember]
        public bool EnableMCPServer { get; set; }
        [DataMember]
        public bool EnableMCPServerAdvancedMode { get; set; }

        [DataMember]
        public string TiltifyCampaignV5 { get; set; }
        [DataMember]
        public bool TiltifyCampaignV5IsTeam { get; set; }

        [DataMember]
        public string DonorDriveCharityURL { get; set; }
        [DataMember]
        public string DonorDriveParticipantID { get; set; }
        [DataMember]
        public bool DonorDriveIncludeTeamDonations { get; set; }

        [DataMember]
        public string JustGivingPageShortName { get; set; }

        [DataMember]
        public string DiscordServer { get; set; }
        [DataMember]
        public string DiscordCustomClientID { get; set; }
        [DataMember]
        public string DiscordCustomClientSecret { get; set; }
        [DataMember]
        public string DiscordCustomBotToken { get; set; }

        [DataMember]
        public string PatreonTierSubscriberEquivalent { get; set; }

        [DataMember]
        public List<SerialDeviceModel> SerialDevices { get; set; } = new List<SerialDeviceModel>();

        [DataMember]
        public int VTubeStudioPortNumber { get; set; } = VTubeStudioService.DefaultPortNumber;

        [DataMember]
        public string VTubeStudioIPAddress { get; set; } = VTubeStudioService.DefaultIPAddress;

        [DataMember]
        public bool VeadotubeEnabled { get; set; }

        // Empty in the normal case. veadotube's address is read from its instance file, so there is
        // nothing to type unless the user is pointing at another machine, where no local file exists.
        [DataMember]
        public string VeadotubeManualAddress { get; set; }

        [DataMember]
        public bool VConnectEnabled { get; set; }

        [DataMember]
        public int VConnectPortNumber { get; set; } = VConnectService.DefaultPortNumber;

        [DataMember]
        public bool RahiTuberEnabled { get; set; }

        [DataMember]
        public int RahiTuberPortNumber { get; set; } = RahiTuberService.DefaultPortNumber;

        [DataMember]
        public int TITSPortNumber { get; set; } = TITSService.DefaultPortNumber;

        [DataMember]
        public int PolyPopPortNumber { get; set; }

        [DataMember]
        public string SAMMIAPIPassword { get; set; }

        [DataMember]
        public int PulsoidCommandTriggerDelay { get; set; } = 3;
        [DataMember]
        public List<Tuple<int, int>> PulsoidCommandHeartRateRangeTriggers { get; set; } = new List<Tuple<int, int>>();

        [DataMember]
        public bool VTSPogEnabled { get; set; }

        [DataMember]
        public bool MtionStudioEnabled { get; set; }

        [DataMember]
        public string AmazonPollyCustomRegionSystemName { get; set; }
        [DataMember]
        public string AmazonPollyCustomAccessKey { get; set; }
        [DataMember]
        public string AmazonPollyCustomSecretKey { get; set; }

        [DataMember]
        public string MicrosoftAzureSpeechCustomRegionName { get; set; }
        [DataMember]
        public string MicrosoftAzureSpeechCustomSubscriptionKey { get; set; }

        [DataMember]
        public string ElevenLabsAPIKey { get; set; }

        [DataMember]
        public string UberduckAPIKey { get; set; }
        [DataMember]
        public string UberduckAPISecret { get; set; }

        [DataMember]
        public string MeldStudioWebSocketAddress { get; set; }

        [DataMember]
        public bool VelumStudioEnabled { get; set; }
        [DataMember]
        public string VelumStudioWebSocketAddress { get; set; }
        [DataMember]
        public string VelumStudioToken { get; set; }
        [DataMember]
        public string VelumStudioCertificateFingerprint { get; set; }

        [DataMember]
        public string GoogleCloudTTSCustomKey { get; set; }

        [DataMember]
        public string ResponsiveVoiceCustomAPIKey { get; set; }

        #endregion Services

        #region Dashboard

        [DataMember]
        public DashboardLayoutTypeEnum DashboardLayout { get; set; }
        [DataMember]
        public List<DashboardItemTypeEnum> DashboardItems { get; set; } = new List<DashboardItemTypeEnum>();
        [DataMember]
        public List<Guid> DashboardQuickCommands { get; set; } = new List<Guid>();

        #endregion Dashboard

        #region Music Player

        [DataMember]
        public string MusicPlayerAudioOutput { get; set; }
        [DataMember]
        public int MusicPlayerVolume { get; set; } = 100;
        [DataMember]
        public List<string> MusicPlayerFolders { get; set; } = new List<string>();
        [DataMember]
        public Guid MusicPlayerOnSongChangedCommandID { get; set; }
        [DataMember]
        public List<string> MusicPlayerQueue { get; set; } = new List<string>();
        [DataMember]
        public int MusicPlayerQueueCurrentIndex { get; set; } = 0;
        [DataMember]
        public bool MusicPlayerShuffle { get; set; } = false;
        [DataMember]
        public bool MusicPlayerRepeat { get; set; } = false;
        [DataMember]
        public bool MusicPlayerRepeatOne { get; set; } = false;

        #endregion

        #region Advanced

        [DataMember]
        public bool ReRunWizard { get; set; }

        #endregion Advanced

        #region Currency

        [DataMember]
        public Dictionary<Guid, CurrencyModel> Currency { get; set; } = new Dictionary<Guid, CurrencyModel>();

        [DataMember]
        public Dictionary<Guid, InventoryModel> Inventory { get; set; } = new Dictionary<Guid, InventoryModel>();

        [DataMember]
        public Dictionary<Guid, StreamPassModel> StreamPass { get; set; } = new Dictionary<Guid, StreamPassModel>();

        [DataMember]
        public bool RedemptionStoreEnabled { get; set; }
        [DataMember]
        public Dictionary<Guid, RedemptionStoreProductModel> RedemptionStoreProducts { get; set; } = new Dictionary<Guid, RedemptionStoreProductModel>();
        [DataMember]
        public string RedemptionStoreChatPurchaseCommand { get; set; } = "!purchase";
        [DataMember]
        public string RedemptionStoreModRedeemCommand { get; set; } = "!redeem";
        [DataMember]
        public Guid RedemptionStoreManualRedeemNeededCommandID { get; set; }
        [DataMember]
        public Guid RedemptionStoreDefaultRedemptionCommandID { get; set; }
        [DataMember]
        public List<RedemptionStorePurchaseModel> RedemptionStorePurchases { get; set; } = new List<RedemptionStorePurchaseModel>();

        #endregion Currency

        #region Twitch

        [DataMember]
        public Dictionary<string, DateTimeOffset> TwitchVIPAutomaticRemovals { get; set; } = new Dictionary<string, DateTimeOffset>();
        [DataMember]
        public bool TwitchAllowSharedChatEvents { get; set; } = false;

        #endregion Twitch

        [DataMember]
        public List<string> RecentStreamTitles { get; set; } = new List<string>();
        [DataMember]
        public List<string> RecentStreamCategories { get; set; } = new List<string>();

        [DataMember]
        public Dictionary<string, object> LatestSpecialIdentifiersData { get; set; } = new Dictionary<string, object>();

        [DataMember]
        public Guid LastFollowerUserID { get; set; }
        [DataMember]
        public Guid LastSubscriberUserID { get; set; }

        [DataMember]
        public Dictionary<string, HotKeyConfiguration> HotKeys { get; set; } = new Dictionary<string, HotKeyConfiguration>();
        [DataMember]
        public Dictionary<string, CounterModel> Counters { get; set; } = new Dictionary<string, CounterModel>();

        #region Database Data

        [JsonIgnore]
        public DatabaseDictionary<Guid, CommandModelBase> Commands { get; set; } = new DatabaseDictionary<Guid, CommandModelBase>();

        [JsonIgnore]
        public DatabaseList<UserQuoteModel> Quotes { get; set; } = new DatabaseList<UserQuoteModel>();

        [JsonIgnore]
        public DatabaseDictionary<Guid, UserV2Model> Users { get; set; } = new DatabaseDictionary<Guid, UserV2Model>();
        [JsonIgnore]
        public DatabaseDictionary<Guid, UserImportModel> ImportedUsers { get; set; } = new DatabaseDictionary<Guid, UserImportModel>();

        #endregion Database Data

        [JsonIgnore]
        public string SettingsFileName { get { return string.Format("{0}.{1}", this.ID, SettingsV3Model.SettingsFileExtension); } }
        [JsonIgnore]
        public string SettingsFilePath { get { return Path.Combine(SettingsV3Model.SettingsDirectoryName, this.SettingsFileName); } }

        [JsonIgnore]
        public string DatabaseFileName { get { return string.Format("{0}.{1}", this.ID, SettingsV3Model.DatabaseFileExtension); } }
        [JsonIgnore]
        public string DatabaseFilePath { get { return Path.Combine(SettingsV3Model.SettingsDirectoryName, this.DatabaseFileName); } }

        [JsonIgnore]
        public string SettingsLocalBackupFileName { get { return string.Format("{0}.{1}.{2}", this.ID, SettingsV3Model.SettingsFileExtension, SettingsV3Model.SettingsLocalBackupFileExtension); } }
        [JsonIgnore]
        public string SettingsLocalBackupFilePath { get { return Path.Combine(SettingsV3Model.SettingsDirectoryName, this.SettingsLocalBackupFileName); } }

        public SettingsV3Model() { }

        public SettingsV3Model(string name)
            : this()
        {
            this.Name = name;

            this.InitializeMissingData();
        }

        public async Task Initialize(bool failOnCommandErrors = false)
        {
            if (!ServiceManager.Get<IFileService>().FileExists(this.DatabaseFilePath))
            {
                await ServiceManager.Get<IFileService>().CopyFile(Path.Combine(AppContext.BaseDirectory, SettingsV3Model.SettingsTemplateDatabaseFileName), this.DatabaseFilePath);
                if (!ServiceManager.Get<IFileService>().FileExists(this.DatabaseFilePath))
                {
                    Logger.Log(LogLevel.Error, $"Failed to create settings database from template: {this.DatabaseFilePath}");
                }
            }

            await ServiceManager.Get<IDatabaseService>().Read(this.DatabaseFilePath, "SELECT * FROM Quotes", (Dictionary<string, object> data) =>
            {
                DateTimeOffset.TryParse((string)data["DateTime"], out DateTimeOffset dateTime);
                this.Quotes.Add(new UserQuoteModel(Convert.ToInt32(data["ID"]), data["Quote"].ToString(), dateTime, data["GameName"].ToString()));
            });
            this.Quotes.ClearTracking();

            HashSet<Guid> forcedCommandResaves = new HashSet<Guid>();
            await ServiceManager.Get<IDatabaseService>().Read(this.DatabaseFilePath, "SELECT * FROM Commands", (Dictionary<string, object> data) =>
            {
                CommandModelBase command = null;
                CommandTypeEnum type = (CommandTypeEnum)Convert.ToInt32(data["TypeID"]);

                try
                {
                    string commandData = data["Data"].ToString();
                    if (type == CommandTypeEnum.Chat)
                    {
                        command = JSONSerializerHelper.DeserializeFromString<ChatCommandModel>(commandData);
                    }
                    else if (type == CommandTypeEnum.Event)
                    {
                        command = JSONSerializerHelper.DeserializeFromString<EventCommandModel>(commandData);
                    }
                    else if (type == CommandTypeEnum.Timer)
                    {
                        command = JSONSerializerHelper.DeserializeFromString<TimerCommandModel>(commandData);
                    }
                    else if (type == CommandTypeEnum.ActionGroup)
                    {
                        command = JSONSerializerHelper.DeserializeFromString<ActionGroupCommandModel>(commandData);
                    }
                    else if (type == CommandTypeEnum.Game)
                    {
                        try
                        {
                            command = JSONSerializerHelper.DeserializeFromString<GameCommandModelBase>(commandData);
                        }
                        catch (Exception ex)
                        {
                            if (ex.Message.Contains("MixItUp.Base.Model.User.UserRoleEnum"))
                            {
                                commandData = commandData.Replace("MixItUp.Base.Model.User.UserRoleEnum", "MixItUp.Base.Model.User.OldUserRoleEnum");
                                command = JSONSerializerHelper.DeserializeFromString<GameCommandModelBase>(commandData);
                                forcedCommandResaves.Add(command.ID);
                            }
                            else
                            {
                                Logger.Log(ex);
                                if (failOnCommandErrors) { throw; }
                            }
                        }
                    }
                    else if (type == CommandTypeEnum.TwitchChannelPoints)
                    {
                        command = JSONSerializerHelper.DeserializeFromString<TwitchChannelPointsCommandModel>(commandData);
                    }
                    else if (type == CommandTypeEnum.StreamlootsCard)
                    {
                        command = JSONSerializerHelper.DeserializeFromString<StreamlootsCardCommandModel>(commandData);
                    }
                    else if (type == CommandTypeEnum.Custom)
                    {
                        command = JSONSerializerHelper.DeserializeFromString<CustomCommandModel>(commandData);
                    }
                    else if (type == CommandTypeEnum.UserOnlyChat)
                    {
                        command = JSONSerializerHelper.DeserializeFromString<UserOnlyChatCommandModel>(commandData);
                    }
                    else if (type == CommandTypeEnum.Webhook)
                    {
                        command = JSONSerializerHelper.DeserializeFromString<WebhookCommandModel>(commandData);
                    }
                    else if (type == CommandTypeEnum.TwitchBits)
                    {
                        TwitchBitsCommandModel tbCommand = JSONSerializerHelper.DeserializeFromString<TwitchBitsCommandModel>(commandData);
                        if (string.IsNullOrWhiteSpace(tbCommand.Name))
                        {
                            tbCommand.Name = tbCommand.AmountDisplay;
                        }
                        command = tbCommand;
                    }
                    else if (type == CommandTypeEnum.CrowdControlEffect)
                    {
                        command = JSONSerializerHelper.DeserializeFromString<CrowdControlEffectCommandModel>(commandData);
                    }
                    else if (type == CommandTypeEnum.TwitchCustomPowerUp)
                    {
                        command = JSONSerializerHelper.DeserializeFromString<TwitchCustomPowerUpCommandModel>(commandData);
                    }
                    else if (type == CommandTypeEnum.KickChannelPoints)
                    {
                        command = JSONSerializerHelper.DeserializeFromString<KickChannelPointsCommandModel>(commandData);
                    }
                    else if (type == CommandTypeEnum.KickKicks)
                    {
                        KickKicksCommandModel kkCommand = JSONSerializerHelper.DeserializeFromString<KickKicksCommandModel>(commandData);
                        if (string.IsNullOrWhiteSpace(kkCommand.Name))
                        {
                            kkCommand.Name = kkCommand.AmountDisplay;
                        }
                        command = kkCommand;
                    }
                    else if (type == CommandTypeEnum.VeloraChannelPoints)
                    {
                        command = JSONSerializerHelper.DeserializeFromString<VeloraChannelPointsCommandModel>(commandData);
                    }
                    else if (type == CommandTypeEnum.VPZoneChannelPoints)
                    {
                        command = JSONSerializerHelper.DeserializeFromString<VPZoneChannelPointsCommandModel>(commandData);
                    }

                    if (command != null)
                    {
                        this.Commands[command.ID] = command;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                    if (failOnCommandErrors) { throw; }
                }
            });
            this.Commands.ClearTracking();

            foreach (Guid id in forcedCommandResaves)
            {
                this.Commands.ManualValueChanged(id);
            }

            foreach (CounterModel counter in this.Counters.Values.ToList())
            {
                // TODO: ToLower() all counters due to case-insensitive Special Identifier processing. Remove at some point in the future.
                this.Counters.Remove(counter.Name);
                counter.Name = counter.Name.ToLower();
                this.Counters[counter.Name] = counter;

                if (counter.ResetOnLoad)
                {
                    await counter.ResetAmount();
                }
            }

            if (string.IsNullOrEmpty(this.TelemetryUserID))
            {
                if (ChannelSession.IsDebug())
                {
                    this.TelemetryUserID = "MixItUpDebuggingUser";
                }
                else
                {
                    this.TelemetryUserID = Guid.NewGuid().ToString();
                }
            }

            // Clear out unused Cooldown Groups and Command Groups
            var unusedCooldownGroupNames = this.CooldownGroupAmounts.Select(c => c.Key).ToList();
            foreach (var command in this.Commands.Values.ToList())
            {
                foreach (CooldownRequirementModel cooldown in command.Requirements?.Cooldowns ?? Enumerable.Empty<CooldownRequirementModel>())
                {
                    if (!string.IsNullOrEmpty(cooldown.GroupName))
                    {
                        unusedCooldownGroupNames.Remove(cooldown.GroupName);
                    }
                }
            }

            foreach (var product in this.RedemptionStoreProducts)
            {
                foreach (CooldownRequirementModel cooldown in product.Value.Requirements?.Cooldowns ?? Enumerable.Empty<CooldownRequirementModel>())
                {
                    if (!string.IsNullOrEmpty(cooldown.GroupName))
                    {
                        unusedCooldownGroupNames.Remove(cooldown.GroupName);
                    }
                }
            }

            foreach (var unused in unusedCooldownGroupNames)
            {
                this.CooldownGroupAmounts.Remove(unused);
            }

            var allUsedCommandGroupNames = this.Commands.Values.ToList().Select(c => c.GroupName).Distinct();
            var allUnusedCommandGroupNames = this.CommandGroups.ToList().Where(c => !allUsedCommandGroupNames.Contains(c.Key, StringComparer.CurrentCulture));
            foreach (var unused in allUnusedCommandGroupNames)
            {
                this.CommandGroups.Remove(unused.Key);
            }

            this.InitializeMissingData();
        }

        public void CopyLatestValues()
        {
            Logger.Log(LogLevel.Debug, "Copying over latest values into Settings object");

            this.Version = SettingsV3Model.LatestVersion;

            foreach (StreamingPlatformSessionBase session in StreamingPlatforms.GetPlatformSessions())
            {
                session.SaveAuthenticationSettings();
            }

            if (ServiceManager.Get<StreamlabsService>().IsConnected)
            {
                this.StreamlabsOAuthToken = ServiceManager.Get<StreamlabsService>().GetOAuthTokenCopy();
            }
            if (ServiceManager.Get<StreamElementsService>().IsConnected)
            {
                this.StreamElementsOAuthToken = ServiceManager.Get<StreamElementsService>().GetOAuthTokenCopy();
            }
            if (ServiceManager.Get<RainmakerService>().IsConnected)
            {
                this.RainMakerOAuthToken = ServiceManager.Get<RainmakerService>().GetOAuthTokenCopy();
            }
            if (ServiceManager.Get<TipeeeStreamService>().IsConnected)
            {
                this.TipeeeStreamOAuthToken = ServiceManager.Get<TipeeeStreamService>().GetOAuthTokenCopy();
            }
            if (ServiceManager.Get<TreatStreamService>().IsConnected)
            {
                this.TreatStreamOAuthToken = ServiceManager.Get<TreatStreamService>().GetOAuthTokenCopy();
            }
            if (ServiceManager.Get<StreamlootsService>().IsConnected)
            {
                this.StreamlootsOAuthToken = ServiceManager.Get<StreamlootsService>().GetOAuthTokenCopy();
            }
            if (ServiceManager.Get<TiltifyService>().IsConnected)
            {
                this.TiltifyOAuthToken = ServiceManager.Get<TiltifyService>().GetOAuthTokenCopy();
            }
            if (ServiceManager.Get<PatreonService>().IsConnected)
            {
                this.PatreonOAuthToken = ServiceManager.Get<PatreonService>().GetOAuthTokenCopy();
            }
            if (ServiceManager.Get<IFTTTService>().IsConnected)
            {
                this.IFTTTOAuthToken = ServiceManager.Get<IFTTTService>().GetOAuthTokenCopy();
            }
            if (ServiceManager.Get<DiscordService>().IsConnected)
            {
                this.DiscordOAuthToken = ServiceManager.Get<DiscordService>().GetOAuthTokenCopy();
            }
            if (ServiceManager.Get<PixelChatService>().IsConnected)
            {
                this.PixelChatOAuthToken = ServiceManager.Get<PixelChatService>().GetOAuthTokenCopy();
            }
            if (ServiceManager.Get<VTubeStudioService>().IsConnected)
            {
                this.VTubeStudioOAuthToken = ServiceManager.Get<VTubeStudioService>().GetOAuthTokenCopy();
            }
            if (ServiceManager.Get<TITSService>().IsConnected)
            {
                this.TITSOAuthToken = ServiceManager.Get<TITSService>().GetOAuthTokenCopy();
            }
            if (ServiceManager.Get<PulsoidService>().IsConnected)
            {
                this.PulsoidOAuthToken = ServiceManager.Get<PulsoidService>().GetOAuthTokenCopy();
            }
            if (ServiceManager.Get<PallyService>().IsConnected)
            {
                this.PallyOAuthToken = ServiceManager.Get<PallyService>().GetOAuthTokenCopy();
            }
            if (ServiceManager.Get<ITTSMonsterService>().IsConnected)
            {
                this.TTSMonsterOAuthToken = ServiceManager.Get<ITTSMonsterService>().GetOAuthTokenCopy();
            }
            if (ServiceManager.Get<ITTSMonsterAPIService>().IsConnected)
            {
                this.TTSMonsterAPIOAuthToken = ServiceManager.Get<ITTSMonsterAPIService>().GetOAuthTokenCopy();
            }
        }

        // to do: move somewhere else (Viewmodel or QuoteService?)
        public async Task SaveRenumberedQuotes()
        {
            List<UserQuoteModel> allQuotes = this.Quotes.ToList();
            if (allQuotes.Count == 0)
            {
                return;
            }

            await ServiceManager.Get<IDatabaseService>().BulkWrite(this.DatabaseFilePath,
                "REPLACE INTO Quotes(ID, Quote, GameName, DateTime) VALUES($ID, $Quote, $GameName, $DateTime)",
                allQuotes.Select(q => new Dictionary<string, object>()
                {
                    { "$ID", q.ID.ToString() },
                    { "$Quote", q.Quote },
                    { "$GameName", q.GameName },
                    { "$DateTime", q.DateTime.ToString() }
                }));

            await ServiceManager.Get<IDatabaseService>().Write(this.DatabaseFilePath,
                $"DELETE FROM Quotes WHERE ID > {allQuotes.Count}");

            this.Quotes.ClearTracking();
        }

        // to do: move somewhere else (Viewmodel or QuoteService?)
        public async Task DeleteAllQuotes()
        {
            this.Quotes.Clear();
            await ServiceManager.Get<IDatabaseService>().Write(this.DatabaseFilePath, "DELETE FROM Quotes");
            this.Quotes.ClearTracking();
        }

        public async Task SaveDatabaseData()
        {
            IEnumerable<Guid> removedUsers = this.Users.GetRemovedValues();
            await ServiceManager.Get<IDatabaseService>().BulkWrite(this.DatabaseFilePath, "DELETE FROM Users WHERE ID = $ID", removedUsers.Select(u => new Dictionary<string, object>() { { "$ID", u.ToString() } }));

            IEnumerable<UserV2Model> changedUsers = this.Users.GetAddedChangedValues();
            await ServiceManager.Get<IDatabaseService>().BulkWrite(this.DatabaseFilePath,
                "REPLACE INTO Users(ID, TwitchID, TwitchUsername, YouTubeID, YouTubeUsername, FacebookID, FacebookUsername, TrovoID, TrovoUsername, Data, KickID, KickUsername, VeloraID, VeloraUsername, VPZoneID, VPZoneUsername) " +
                "VALUES($ID, $TwitchID, $TwitchUsername, $YouTubeID, $YouTubeUsername, $FacebookID, $FacebookUsername, $TrovoID, $TrovoUsername, $Data, $KickID, $KickUsername, $VeloraID, $VeloraUsername, $VPZoneID, $VPZoneUsername)",
                changedUsers.Select(u => new Dictionary<string, object>()
                {
                    { "$ID", u.ID.ToString() },
                    { "$TwitchID", u.GetPlatformID(StreamingPlatformTypeEnum.Twitch) }, { "$TwitchUsername", u.GetPlatformUsername(StreamingPlatformTypeEnum.Twitch) },
                    { "$YouTubeID", u.GetPlatformID(StreamingPlatformTypeEnum.YouTube) }, { "$YouTubeUsername", u.GetPlatformUsername(StreamingPlatformTypeEnum.YouTube) },
#pragma warning disable CS0612 // Type or member is obsolete
                    { "$FacebookID", u.GetPlatformID(StreamingPlatformTypeEnum.Facebook) }, { "$FacebookUsername", u.GetPlatformUsername(StreamingPlatformTypeEnum.Facebook) },
                    { "$TrovoID", u.GetPlatformID(StreamingPlatformTypeEnum.Trovo) }, { "$TrovoUsername", u.GetPlatformUsername(StreamingPlatformTypeEnum.Trovo) },
#pragma warning restore CS0612 // Type or member is obsolete                    
                    { "$Data", JSONSerializerHelper.SerializeToString(u) },
                    { "$KickID", u.GetPlatformID(StreamingPlatformTypeEnum.Kick) }, { "$KickUsername", u.GetPlatformUsername(StreamingPlatformTypeEnum.Kick) },
                    { "$VeloraID", u.GetPlatformID(StreamingPlatformTypeEnum.Velora) }, { "$VeloraUsername", u.GetPlatformUsername(StreamingPlatformTypeEnum.Velora) },
                    { "$VPZoneID", u.GetPlatformID(StreamingPlatformTypeEnum.VPZone) }, { "$VPZoneUsername", u.GetPlatformUsername(StreamingPlatformTypeEnum.VPZone) }
                }));

            List<Guid> removedCommands = new List<Guid>();
            await ServiceManager.Get<IDatabaseService>().BulkWrite(this.DatabaseFilePath, "DELETE FROM Commands WHERE ID = $ID",
                this.Commands.GetRemovedValues().Select(id => new Dictionary<string, object>() { { "$ID", id.ToString() } }));

            await ServiceManager.Get<IDatabaseService>().BulkWrite(this.DatabaseFilePath, "REPLACE INTO Commands(ID, TypeID, Data) VALUES($ID, $TypeID, $Data)",
                this.Commands.GetAddedChangedValues().Select(c => new Dictionary<string, object>() { { "$ID", c.ID.ToString() }, { "$TypeID", (int)c.Type }, { "$Data", JSONSerializerHelper.SerializeToString(c) } }));

            await ServiceManager.Get<IDatabaseService>().BulkWrite(this.DatabaseFilePath, "DELETE FROM Quotes WHERE ID = $ID",
                this.Quotes.GetRemovedValues().Select(q => new Dictionary<string, object>() { { "$ID", q.ID.ToString() } }));

            await ServiceManager.Get<IDatabaseService>().BulkWrite(this.DatabaseFilePath, "REPLACE INTO Quotes(ID, Quote, GameName, DateTime) VALUES($ID, $Quote, $GameName, $DateTime)",
                this.Quotes.GetAddedChangedValues().Select(q => new Dictionary<string, object>() { { "$ID", q.ID.ToString() }, { "$Quote", q.Quote }, { "$GameName", q.GameName }, { "$DateTime", q.DateTime.ToString() } }));

            try
            {
                await ServiceManager.Get<IDatabaseService>().BulkWrite(this.DatabaseFilePath, "DELETE FROM ImportedUsers WHERE ID = $ID",
                    this.ImportedUsers.GetRemovedValues().Select(u => new Dictionary<string, object>() { { "$ID", u.ToString() } }));

                await ServiceManager.Get<IDatabaseService>().BulkWrite(this.DatabaseFilePath, "REPLACE INTO ImportedUsers(ID, Platform, PlatformID, PlatformUsername, Data) VALUES($ID, $Platform, $PlatformID, $PlatformUsername, $Data)",
                    this.ImportedUsers.GetAddedChangedValues().Select(u => new Dictionary<string, object>()
                    {
                    { "$ID", u.ID.ToString() },
                    { "$Platform", (int)u.Platform },
                    { "$PlatformID", u.PlatformID },
                    { "$PlatformUsername", u.PlatformUsername },
                    { "$Data", JSONSerializerHelper.SerializeToString(u) }
                    }));
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                await CreateUserImportTable();
            }

            //try
            //{
            //    await ServiceManager.Get<IDatabaseService>().BulkWrite(this.DatabaseFilePath, "REPLACE INTO Statistics(ID, DateTime, TypeID, PlatformID, Data) VALUES($ID, $DateTime, $TypeID, $PlatformID, $Data)",
            //        ServiceManager.Get<StatisticsService>().GetStatisticsToSave().Select(s => new Dictionary<string, object>() { { "$ID", s.ID.ToString() }, { "$DateTime", s.DateTime }, { "$TypeID", (int)s.Type },
            //            { "$PlatformID", (int)s.Platform }, { "$Data", JSONSerializerHelper.SerializeToString(s.Data) } }));
            //}
            //catch (Exception ex)
            //{
            //    Logger.Log(ex);
            //    await CreateStatisticsTable();
            //}

            await ServiceManager.Get<IDatabaseService>().CompressDb(this.DatabaseFilePath);
        }

        public async Task BackupSettingsToDatabase()
        {
            try
            {
                await ServiceManager.Get<IDatabaseService>().Write(this.DatabaseFilePath, "CREATE TABLE IF NOT EXISTS \"SettingsBackupHistory\" (\"ID\" INTEGER PRIMARY KEY, \"BackupDateTime\" datetime not null default CURRENT_TIMESTAMP, \"SettingsJSON\" text not null)");

                string settingsJSON = JSONSerializerHelper.SerializeToString(this);

                if (string.IsNullOrEmpty(settingsJSON) || settingsJSON.Contains("\0"))
                {
                    Logger.Log(LogLevel.Error, "Invalid settings data detected, skipping database backup");
                    return;
                }

                await ServiceManager.Get<IDatabaseService>().BulkWrite(
                    this.DatabaseFilePath,
                    "INSERT INTO SettingsBackupHistory (SettingsJSON) VALUES ($JSON)",
                    new List<Dictionary<string, object>> { new Dictionary<string, object> { { "$JSON", settingsJSON } } });

                await ServiceManager.Get<IDatabaseService>().Write(
                    this.DatabaseFilePath,
                    @"DELETE FROM SettingsBackupHistory 
              WHERE ID NOT IN (
                  SELECT ID FROM SettingsBackupHistory 
                  ORDER BY BackupDateTime DESC 
                  LIMIT 20
              )");

                Logger.Log(LogLevel.Debug, "Settings backed up to database");
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "Failed to backup settings to database");
                Logger.Log(ex);
            }
        }

        public async Task SaveMissingFilesCheckCommand(CommandModelBase command)
        {
            if (command == null) return;
            this.Commands.ManualValueChanged(command.ID);
            await ServiceManager.Get<IDatabaseService>().BulkWrite(
                this.DatabaseFilePath,
                "REPLACE INTO Commands(ID, TypeID, Data) VALUES($ID, $TypeID, $Data)",
                new List<Dictionary<string, object>>() { new Dictionary<string, object>() { { "$ID", command.ID.ToString() }, { "$TypeID", (int)command.Type }, { "$Data", JSONSerializerHelper.SerializeToString(command) } } });
        }

        public async Task<IEnumerable<UserV2Model>> LoadUserV2Data(string query, Dictionary<string, object> parameters)
        {
            List<UserV2Model> results = new List<UserV2Model>();

            await ServiceManager.Get<IDatabaseService>().Read(this.DatabaseFilePath, query, parameters,
                (Dictionary<string, object> data) =>
                {
                    results.Add(JSONSerializerHelper.DeserializeFromString<UserV2Model>(data["Data"].ToString()));
                });

            foreach (UserV2Model user in results)
            {
                if (!this.Users.ContainsKey(user.ID))
                {
                    this.Users[user.ID] = user;
                    this.Users.ClearTracking(user.ID);
                }
            }

            return results;
        }

        public async Task<UserImportModel> LoadUserImportData(StreamingPlatformTypeEnum platform, string platformID, string platformUsername)
        {
            UserImportModel userImport = null;

            try
            {
                await ServiceManager.Get<IDatabaseService>().Read(this.DatabaseFilePath,
                    $"SELECT * FROM ImportedUsers WHERE Platform = @Platform AND (PlatformID = @PlatformID OR PlatformUsername = @PlatformUsername)",
                    new Dictionary<string, object>()
                    {
                        { "Platform", (int)platform },
                        { "PlatformID", platformID },
                        { "PlatformUsername", platformUsername }
                    },
                    (Dictionary<string, object> data) =>
                    {
                        userImport = JSONSerializerHelper.DeserializeFromString<UserImportModel>(data["Data"].ToString());
                        this.ImportedUsers[userImport.ID] = userImport;
                        this.ImportedUsers.ClearTracking(userImport.ID);
                    });
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                await CreateUserImportTable();
            }

            return userImport;
        }

        public async Task CreateUserImportTable()
        {
            bool tableExists = false;
            await ServiceManager.Get<IDatabaseService>().Read(this.DatabaseFilePath, "SELECT name FROM sqlite_master WHERE type='table' AND name='ImportedUsers'", (row) =>
            {
                tableExists = true;
            });

            if (!tableExists)
            {
                await ServiceManager.Get<IDatabaseService>().Write(this.DatabaseFilePath, "CREATE TABLE \"ImportedUsers\" (\"ID\" TEXT NOT NULL, \"Platform\" INTEGER NOT NULL, \"PlatformID\" TEXT, \"PlatformUsername\" TEXT, \"Data\" TEXT NOT NULL, UNIQUE(\"Platform\",\"PlatformID\",\"PlatformUsername\"), PRIMARY KEY(\"ID\"))");
            }
        }

        public async Task AddMissingUsersTableKickColumns()
        {
            bool hasKickID = false;
            bool hasKickUsername = false;

            await ServiceManager.Get<IDatabaseService>().Read(this.DatabaseFilePath, "PRAGMA table_info(Users)", (row) =>
            {
                string columnName = row["name"]?.ToString();
                if (string.Equals(columnName, "KickID", StringComparison.OrdinalIgnoreCase))
                {
                    hasKickID = true;
                }
                else if (string.Equals(columnName, "KickUsername", StringComparison.OrdinalIgnoreCase))
                {
                    hasKickUsername = true;
                }
            });

            if (!hasKickID)
            {
                await ServiceManager.Get<IDatabaseService>().Write(this.DatabaseFilePath, "ALTER TABLE Users ADD COLUMN KickID TEXT");
            }

            if (!hasKickUsername)
            {
                await ServiceManager.Get<IDatabaseService>().Write(this.DatabaseFilePath, "ALTER TABLE Users ADD COLUMN KickUsername TEXT");
            }
        }

        public async Task AddMissingUsersTableVeloraColumns()
        {
            bool hasVeloraID = false;
            bool hasVeloraUsername = false;

            await ServiceManager.Get<IDatabaseService>().Read(this.DatabaseFilePath, "PRAGMA table_info(Users)", (row) =>
            {
                string columnName = row["name"]?.ToString();
                if (string.Equals(columnName, "VeloraID", StringComparison.OrdinalIgnoreCase))
                {
                    hasVeloraID = true;
                }
                else if (string.Equals(columnName, "VeloraUsername", StringComparison.OrdinalIgnoreCase))
                {
                    hasVeloraUsername = true;
                }
            });

            if (!hasVeloraID)
            {
                await ServiceManager.Get<IDatabaseService>().Write(this.DatabaseFilePath, "ALTER TABLE Users ADD COLUMN VeloraID TEXT");
            }

            if (!hasVeloraUsername)
            {
                await ServiceManager.Get<IDatabaseService>().Write(this.DatabaseFilePath, "ALTER TABLE Users ADD COLUMN VeloraUsername TEXT");
            }
        }

        public async Task AddMissingUsersTableVPZoneColumns()
        {
            bool hasVPZoneID = false;
            bool hasVPZoneUsername = false;

            await ServiceManager.Get<IDatabaseService>().Read(this.DatabaseFilePath, "PRAGMA table_info(Users)", (row) =>
            {
                string columnName = row["name"]?.ToString();
                if (string.Equals(columnName, "VPZoneID", StringComparison.OrdinalIgnoreCase))
                {
                    hasVPZoneID = true;
                }
                else if (string.Equals(columnName, "VPZoneUsername", StringComparison.OrdinalIgnoreCase))
                {
                    hasVPZoneUsername = true;
                }
            });

            if (!hasVPZoneID)
            {
                await ServiceManager.Get<IDatabaseService>().Write(this.DatabaseFilePath, "ALTER TABLE Users ADD COLUMN VPZoneID TEXT");
            }

            if (!hasVPZoneUsername)
            {
                await ServiceManager.Get<IDatabaseService>().Write(this.DatabaseFilePath, "ALTER TABLE Users ADD COLUMN VPZoneUsername TEXT");
            }
        }

        public async Task<IEnumerable<StatisticModel>> LoadSpecificStatisticType(StatisticItemTypeEnum type)
        {
            List<StatisticModel> statistics = new List<StatisticModel>();

            try
            {
                await ServiceManager.Get<IDatabaseService>().Read(this.DatabaseFilePath,
                    $"SELECT * FROM Statistics WHERE TypeID = @TypeID",
                    new Dictionary<string, object>()
                    {
                        { "TypeID", (int)type }
                    },
                    (Dictionary<string, object> data) =>
                    {
                        statistics.Add(new StatisticModel(data));
                    });
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                await this.CreateStatisticsTable();
            }

            return statistics;
        }

        public async Task<IEnumerable<StatisticModel>> LoadStatisticBetweenRange(DateTime start, DateTime end)
        {
            List<StatisticModel> statistics = new List<StatisticModel>();

            try
            {
                await ServiceManager.Get<IDatabaseService>().Read(this.DatabaseFilePath,
                    $"SELECT * FROM Statistics WHERE DateTime >= @Start AND DateTime <= @End",
                    new Dictionary<string, object>()
                    {
                        { "Start", start },
                        { "End", end }
                    },
                    (Dictionary<string, object> data) =>
                    {
                        statistics.Add(new StatisticModel(data));
                    });
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                await this.CreateStatisticsTable();
            }

            return statistics;
        }

        public async Task CreateStatisticsTable()
        {
            bool tableExists = false;
            bool tableValid = false;
            await ServiceManager.Get<IDatabaseService>().Read(this.DatabaseFilePath, "SELECT name, sql FROM sqlite_master WHERE type='table' AND name='Statistics'", (row) =>
            {
                tableExists = true;
                if (row.TryGetValue("sql", out object sql) && sql.ToString().Contains("Description"))
                {
                    tableValid = true;
                }
            });

            if (tableExists && !tableValid)
            {
                await ServiceManager.Get<IDatabaseService>().Write(this.DatabaseFilePath, "DROP TABLE 'Statistics'");
                tableExists = false;
                tableValid = false;
            }

            if (!tableExists)
            {
                await ServiceManager.Get<IDatabaseService>().Write(this.DatabaseFilePath, "CREATE TABLE \"Statistics\" (\"ID\" TEXT not null, \"DateTime\" datetime not null default CURRENT_TIMESTAMP, \"TypeID\" INT not null, \"PlatformID\" INT not null, \"Amount\" DECIMAL not null default 0, \"Description\" text null, \"Data\" text null, primary key (\"ID\"))");
            }
        }

        public CommandModelBase GetCommand(Guid id) { return this.Commands.ContainsKey(id) ? this.Commands[id] : null; }



        public T GetCommand<T>(Guid id) where T : CommandModelBase { return (T)this.GetCommand(id); }

        public void SetCommand(CommandModelBase command) { if (command != null) { this.Commands[command.ID] = command; } }

        public void RemoveCommand(CommandModelBase command) { if (command != null) { this.Commands.Remove(command.ID); } }

        public void RemoveCommand(Guid id) { this.Commands.Remove(id); }

        private async void InitializeMissingData()
        {
            await this.CreateUserImportTable();
            await this.AddMissingUsersTableKickColumns();
            await this.AddMissingUsersTableVeloraColumns();
            await this.AddMissingUsersTableVPZoneColumns();
            //await this.CreateStatisticsTable();

            StreamingPlatforms.ForEachPlatform(p =>
            {
                if (StreamingPlatforms.SupportedPlatforms.Contains(p) && !this.StreamingPlatformAuthentications.ContainsKey(p))
                {
                    this.StreamingPlatformAuthentications[p] = new StreamingPlatformAuthenticationSettingsModel(p);
                }
            });

            if (this.OverlayEndpointsV3.Count == 0)
            {
                this.OverlayEndpointsV3.Add(new OverlayEndpointV3Model(OverlayEndpointV3Model.DefaultOverlayName)
                {
                    ID = Guid.Empty
                });

                await ServiceManager.Get<OverlayV3Service>().Enable();

                this.EnableOverlay = true;
            }

            if (this.DefaultStreamingPlatform == StreamingPlatformTypeEnum.None)
            {
                var auth = this.StreamingPlatformAuthentications.FirstOrDefault(p => p.Value != null && p.Value.IsEnabled);
                if (auth.Value != null && StreamingPlatforms.SupportedPlatforms.Contains(auth.Value.Type))
                {
                    this.DefaultStreamingPlatform = auth.Value.Type;
                }
            }

            if (this.DashboardItems.Count < 4)
            {
                this.DashboardItems = new List<DashboardItemTypeEnum>() { DashboardItemTypeEnum.None, DashboardItemTypeEnum.None, DashboardItemTypeEnum.None, DashboardItemTypeEnum.None };
            }
            if (this.DashboardQuickCommands.Count < 5)
            {
                this.DashboardQuickCommands = new List<Guid>() { Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty };
            }

            if (this.GetCommand(this.GameQueueUserJoinedCommandID) == null)
            {
                this.GameQueueUserJoinedCommandID = this.CreateBasicChatCommand(MixItUp.Base.Resources.GameQueueUserJoinedCommandName, MixItUp.Base.Resources.GameQueueUserJoinedExample);
            }
            if (this.GetCommand(this.GameQueueUserSelectedCommandID) == null)
            {
                this.GameQueueUserSelectedCommandID = this.CreateBasicChatCommand(MixItUp.Base.Resources.GameQueueUserSelectedCommandName, MixItUp.Base.Resources.GameQueueUserSelectedExample);
            }

            if (this.GetCommand(this.GiveawayStartedReminderCommandID) == null)
            {
                this.GiveawayStartedReminderCommandID = this.CreateBasicChatCommand(MixItUp.Base.Resources.GiveawayStartedReminderCommandName, MixItUp.Base.Resources.GiveawayStartedReminderExample);
            }
            if (this.GetCommand(this.GiveawayUserJoinedCommandID) == null)
            {
                this.GiveawayUserJoinedCommandID = this.CreateBasicCommand(MixItUp.Base.Resources.GiveawayUserJoinedCommandName);
            }
            if (this.GetCommand(this.GiveawayWinnerSelectedCommandID) == null)
            {
                this.GiveawayWinnerSelectedCommandID = this.CreateBasicChatCommand(MixItUp.Base.Resources.GiveawayWinnerSelectedCommandName, MixItUp.Base.Resources.GiveawayWinnerSelectedExample);
            }

            if (this.GetCommand(this.ModerationStrike1CommandID) == null)
            {
                this.ModerationStrike1CommandID = this.CreateBasicChatCommand(MixItUp.Base.Resources.ModerationStrike1CommandName, MixItUp.Base.Resources.ModerationStrikeExample);
            }
            if (this.GetCommand(this.ModerationStrike2CommandID) == null)
            {
                this.ModerationStrike2CommandID = this.CreateBasicChatCommand(MixItUp.Base.Resources.ModerationStrike2CommandName, MixItUp.Base.Resources.ModerationStrikeExample);
            }
            if (this.GetCommand(this.ModerationStrike3CommandID) == null)
            {
                this.ModerationStrike3CommandID = this.CreateBasicChatCommand(MixItUp.Base.Resources.ModerationStrike3CommandName, MixItUp.Base.Resources.ModerationStrikeExample);
            }

            if (this.GetCommand(this.RedemptionStoreManualRedeemNeededCommandID) == null)
            {
                this.RedemptionStoreManualRedeemNeededCommandID = this.CreateBasicChatCommand(MixItUp.Base.Resources.RedemptionStoreManualRedeemNeededCommandName, MixItUp.Base.Resources.RedemptionStoreManualRedeemNeededExample);
            }
            if (this.GetCommand(this.RedemptionStoreDefaultRedemptionCommandID) == null)
            {
                this.RedemptionStoreDefaultRedemptionCommandID = this.CreateBasicChatCommand(MixItUp.Base.Resources.RedemptionStoreDefaultRedemptionCommandName, MixItUp.Base.Resources.RedemptionStoreDefaultRedemptionExample);
            }

            if (this.GetCommand(this.MusicPlayerOnSongChangedCommandID) == null)
            {
                this.MusicPlayerOnSongChangedCommandID = this.CreateBasicCommand(MixItUp.Base.Resources.MusicPlayerOnSongChanged);
            }

            if (this.GetCommand(this.QuoteAddedCommandID) == null)
            {
                this.QuoteAddedCommandID = this.CreateBasicCommand(MixItUp.Base.Resources.QuoteAdded);
            }
        }

        public Version GetLatestVersion() { return Assembly.GetEntryAssembly().GetName().Version; }

        private Guid CreateBasicCommand(string name)
        {
            CustomCommandModel command = new CustomCommandModel(name);
            this.SetCommand(command);
            return command.ID;
        }

        private Guid CreateBasicChatCommand(string name, string chatText)
        {
            CustomCommandModel command = new CustomCommandModel(name);
            command.Actions.Add(new ChatActionModel(chatText));
            this.SetCommand(command);
            return command.ID;
        }
    }
}

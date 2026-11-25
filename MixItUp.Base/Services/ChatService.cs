using MixItUp.Base.Model;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.Currency;
using MixItUp.Base.Model.Requirements;
using MixItUp.Base.Model.User;
using MixItUp.Base.Services.Mock.New;
using MixItUp.Base.Services.Kick.New;
using MixItUp.Base.Services.Twitch.New;
using MixItUp.Base.Services.Velora.New;
using MixItUp.Base.Services.VPZone.New;
using MixItUp.Base.Services.Joystick;
using MixItUp.Base.Services.AltStream;
using MixItUp.Base.Services.Razo;
using MixItUp.Base.Services.YouTube.New;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Chat;
using MixItUp.Base.ViewModel.Chat.Twitch;
using MixItUp.Base.ViewModel.Chat.YouTube;
using MixItUp.Base.ViewModel.User;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Base.Services
{
    public class ChatService
    {
        private const string ChatEventLogDirectoryName = "ChatEventLogs";
        private const string ChatEventLogFileNameFormat = "ChatEventLog-{0}.txt";

        public static event EventHandler OnChatVisualSettingsChanged = delegate { };
        public static void ChatVisualSettingsChanged() { OnChatVisualSettingsChanged(null, new EventArgs()); }

        public static event EventHandler<ChatMessageViewModel> OnChatMessageReceived = delegate { };
        public static void ChatMessageReceived(ChatMessageViewModel message) { OnChatMessageReceived(null, message); }

        public static event EventHandler<string> OnChatMessageDeleted = delegate { };
        public static void ChatMessageDeleted(string messageID) { OnChatMessageDeleted(null, messageID); }

        public static event EventHandler<string> OnChannelChatCleared = delegate { };

        public async Task ClearChannelMessages(StreamingPlatformTypeEnum platform, string prefix)
        {
            foreach (var row in this.Messages.ToList().Where(m => m.Platform == platform && m.ID.StartsWith(prefix, StringComparison.Ordinal)))
                await this.DeleteMessage(row.ID);
            OnChannelChatCleared(null, prefix);
        }

        public static event EventHandler<UserV2ViewModel> OnChatUserTimedOut = delegate { };
        public static void ChatUserTimedOut(UserV2ViewModel user) { OnChatUserTimedOut(null, user); }

        public static event EventHandler<UserV2ViewModel> OnChatUserBanned = delegate { };
        public static void ChatUserBanned(UserV2ViewModel user) { OnChatUserBanned(null, user); }

        public static event EventHandler OnChatCleared = delegate { };
        public static void ChatCleared() { OnChatCleared(null, new EventArgs()); }

        public static string SplitLargeMessage(string message, int maxLength, out string subMessage)
        {
            subMessage = null;
            if (message.Length >= maxLength)
            {
                string tempMessage = message.Substring(0, maxLength - 1);
                int splitIndex = tempMessage.LastIndexOf(' ');
                if (splitIndex <= 0)
                {
                    splitIndex = maxLength;
                }

                if (splitIndex + 1 < message.Length)
                {
                    subMessage = message.Substring(splitIndex + 1);
                    message = message.Substring(0, splitIndex);
                }
            }
            return message;
        }

        public bool DisableChat { get; set; }

        public ThreadSafeObservableCollection<ChatMessageViewModel> Messages { get; private set; } = new ThreadSafeObservableCollection<ChatMessageViewModel>();
        private LockedDictionary<string, ChatMessageViewModel> messagesLookup = new LockedDictionary<string, ChatMessageViewModel>();

        public event EventHandler ChatCommandsReprocessed = delegate { };
        public IEnumerable<CommandModelBase> ChatMenuCommands { get { return this.chatMenuCommands.ToList(); } }
        private List<CommandModelBase> chatMenuCommands = new List<CommandModelBase>();

        public event EventHandler<Dictionary<string, uint>> OnPollEndOccurred = delegate { };

        private Dictionary<string, CommandModelBase> triggersToCommands = new Dictionary<string, CommandModelBase>();
        private int longestTrigger = 0;
        private List<CommandModelBase> wildcardCommands = new List<CommandModelBase>();

        private HashSet<Guid> userEntranceCommands = new HashSet<Guid>();

        private SemaphoreSlim whisperNumberLock = new SemaphoreSlim(1);
        private Dictionary<Guid, int> whisperMap = new Dictionary<Guid, int>();

        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        private string currentChatEventLogFilePath;

        public ChatService() { }

        public async Task Initialize()
        {
            this.RebuildCommandTriggers();

            await ServiceManager.Get<IFileService>().CreateDirectory(ChatEventLogDirectoryName);
            this.currentChatEventLogFilePath = Path.Combine(ChatEventLogDirectoryName, string.Format(ChatEventLogFileNameFormat, DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss", CultureInfo.InvariantCulture)));

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            AsyncRunner.RunAsyncBackground(this.MinuteBackgroundThread, this.cancellationTokenSource.Token, 60000);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }

        public int GetViewerCount()
        {
            return StreamingPlatforms.GetConnectedPlatformSessions()
                .Where(session => session.IsLive)
                .Sum(session => session.StreamViewerCount);
        }

        public async Task SendMessage(string message, bool sendAsStreamer = false, string replyMessageID = null)
        {
            await StreamingPlatforms.ForEachPlatform(async (p) =>
            {
                await this.SendMessage(message, p, sendAsStreamer, replyMessageID);
            });
        }

        public async Task SendMessage(string message, CommandParametersModel parameters, bool sendAsStreamer = false)
        {
            await this.SendMessage(message, parameters.Platform, sendAsStreamer, parameters.TriggeringChatMessageID);
        }

        public async Task SendMessage(string message, StreamingPlatformTypeEnum platform, bool sendAsStreamer = false, string replyMessageID = null)
        {
            if (!string.IsNullOrEmpty(message))
            {
                if (platform == StreamingPlatformTypeEnum.All)
                {
                    await this.SendMessage(message, sendAsStreamer, replyMessageID);
                }
                else if (!string.IsNullOrEmpty(message))
                {
                    if (platform == StreamingPlatformTypeEnum.Twitch && ServiceManager.Get<TwitchSession>().IsConnected)
                    {
                        await ServiceManager.Get<TwitchSession>().SendMessage(message, sendAsStreamer, replyMessageID);
                    }
                    else if (platform == StreamingPlatformTypeEnum.YouTube && ServiceManager.Get<YouTubeSession>().IsConnected)
                    {
                        if (!string.IsNullOrEmpty(replyMessageID) &&
                            this.messagesLookup.TryGetValue(replyMessageID, out ChatMessageViewModel replyMessage) &&
                            replyMessage is YouTubeChatMessageViewModel youTubeReplyMessage &&
                            !string.IsNullOrEmpty(youTubeReplyMessage.BroadcastID))
                        {
                            await ServiceManager.Get<YouTubeSession>().SendMessage(message, youTubeReplyMessage.BroadcastID, sendAsStreamer);
                        }
                        else
                        {
                            await ServiceManager.Get<YouTubeSession>().SendMessage(message, sendAsStreamer);
                        }
                    }
                    else if (platform == StreamingPlatformTypeEnum.Kick && ServiceManager.Get<KickSession>().IsConnected)
                    {
                        await ServiceManager.Get<KickSession>().SendMessage(message, sendAsStreamer);
                    }
                    else if (platform == StreamingPlatformTypeEnum.Velora && ServiceManager.Get<VeloraSession>().IsConnected)
                    {
                        await ServiceManager.Get<VeloraSession>().SendMessage(message, sendAsStreamer);
                    }
                    else if (platform == StreamingPlatformTypeEnum.VPZone && ServiceManager.Get<VPZoneSession>().IsConnected)
                    {
                        await ServiceManager.Get<VPZoneSession>().SendMessage(message, sendAsStreamer);
                    }
                    else if (platform == StreamingPlatformTypeEnum.Joystick && ServiceManager.Get<JoystickSession>().IsConnected)
                    {
                        await AsyncRunner.RunAsync(ServiceManager.Get<JoystickSession>().SendMessage(message, sendAsStreamer));
                    }
                    else if (platform == StreamingPlatformTypeEnum.AltStream && ServiceManager.Get<AltStreamSession>().IsConnected)
                    {
                        await AsyncRunner.RunAsync(ServiceManager.Get<AltStreamSession>().SendChatMessage(message, replyMessageID));
                    }
                    else if (platform == StreamingPlatformTypeEnum.Razo && ServiceManager.Get<RazoSession>().IsConnected)
                    {
                        await AsyncRunner.RunAsync(ServiceManager.Get<RazoSession>().SendMessage(message, sendAsStreamer));
                    }
                    else if (platform == StreamingPlatformTypeEnum.Mock)
                    {
                        await ServiceManager.Get<MockSession>().SendMessage(message, sendAsStreamer);
                    }
                }
            }
        }

        public async Task Whisper(UserV2ViewModel user, string message, bool sendAsStreamer = false)
        {
            if (user != null && !string.IsNullOrEmpty(message))
            {
                if (user.Platform == StreamingPlatformTypeEnum.Twitch && ServiceManager.Get<TwitchSession>().IsConnected)
                {
                    await ServiceManager.Get<TwitchSession>().SendWhisper(user, message, sendAsStreamer);
                }
                else if (user.Platform == StreamingPlatformTypeEnum.Joystick && ServiceManager.Get<JoystickSession>().IsConnected)
                {
                    await AsyncRunner.RunAsync(ServiceManager.Get<JoystickSession>().Whisper(user.Username, message));
                }
            }
        }

        public async Task Whisper(string username, StreamingPlatformTypeEnum platform, string message, bool sendAsStreamer = false)
        {
            UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByPlatform(platform, platformUsername: username, performPlatformSearch: true);
            if (user != null)
            {
                await this.Whisper(user, message, sendAsStreamer);
            }
        }

        public async Task DeleteMessage(string messageID)
        {
            if (string.IsNullOrEmpty(messageID))
            {
                return;
            }

            if (this.messagesLookup.TryGetValue(messageID, out ChatMessageViewModel message) && message != null)
            {
                await this.DeleteMessage(message, externalDeletion: true);
            }
            else { ChatMessageDeleted(messageID); }
        }

        public async Task DeleteMessage(ChatMessageViewModel message, bool externalDeletion = false)
        {
            if (message == null)
            {
                return;
            }
            if (externalDeletion && this.messagesLookup.TryGetValue(message.ID, out ChatMessageViewModel existingMessage) && existingMessage != null)
            {
                message = existingMessage;
            }

            if (!externalDeletion && !string.IsNullOrEmpty(message.ID))
            {
                if (message.Platform == StreamingPlatformTypeEnum.Twitch && ServiceManager.Get<TwitchSession>().IsConnected)
                {
                    await ServiceManager.Get<TwitchSession>().DeleteMessage(message);
                }
                else if (message.Platform == StreamingPlatformTypeEnum.YouTube && ServiceManager.Get<YouTubeSession>().IsConnected)
                {
                    await ServiceManager.Get<YouTubeSession>().DeleteMessage(message);
                }
                else if (message.Platform == StreamingPlatformTypeEnum.Kick && ServiceManager.Get<KickSession>().IsConnected)
                {
                    await ServiceManager.Get<KickSession>().DeleteMessage(message);
                }
                else if (message.Platform == StreamingPlatformTypeEnum.Velora && ServiceManager.Get<VeloraSession>().IsConnected)
                {
                    await ServiceManager.Get<VeloraSession>().DeleteMessage(message);
                }
                else if (message.Platform == StreamingPlatformTypeEnum.VPZone && ServiceManager.Get<VPZoneSession>().IsConnected)
                {
                    await ServiceManager.Get<VPZoneSession>().DeleteMessage(message);
                }
                else if (message.Platform == StreamingPlatformTypeEnum.Joystick && ServiceManager.Get<JoystickSession>().IsConnected)
                {
                    await AsyncRunner.RunAsync(ServiceManager.Get<JoystickSession>().DeleteMessage(message));
                }
                else if (message.Platform == StreamingPlatformTypeEnum.Razo && ServiceManager.Get<RazoSession>().CanDelete)
                {
                    await ServiceManager.Get<RazoSession>().DeleteMessage(message);
                }
                else if (message.Platform == StreamingPlatformTypeEnum.Mock)
                {
                    await ServiceManager.Get<MockSession>().DeleteMessage(message);
                }
            }

            if (!message.IsDeleted)
            {
                await message.Delete();

                if (ChannelSession.Settings.HideDeletedMessages)
                {
                    await this.RemoveMessage(message);
                }

                ChatService.ChatMessageDeleted(message.ID);
            }
        }

        // Returns the number of messages newly marked; already-deleted messages are skipped so a purge that
        // fires both locally and from the platform's ban/timeout echo does not re-process the same messages.
        public async Task<int> MarkUserMessagesAsDeleted(UserV2ViewModel user, UserV2ViewModel moderator = null, string reason = null)
        {
            int deleted = 0;
            foreach (ChatMessageViewModel message in this.Messages.ToList())
            {
                if (message.User != null && message.User.ID == user.ID && !message.IsDeleted &&
                    (!StreamingPlatforms.IsValidPlatform(user.Platform) || message.Platform == user.Platform))
                {
                    await message.Delete(moderator, reason, triggerEventCommand: false);

                    if (ChannelSession.Settings.HideDeletedMessages)
                    {
                        await this.RemoveMessage(message);
                    }

                    ChatService.ChatMessageDeleted(message.ID);
                    deleted++;
                }
            }
            return deleted;
        }

        public async Task ClearMessages(StreamingPlatformTypeEnum platform)
        {
            // Every platform gets its turn before the local window is emptied. VPZone has no bulk clear
            // and works out what to delete by reading this.Messages, so clearing the window per platform
            // left it with nothing to do whenever it was not the first platform in the loop.
            if (platform == StreamingPlatformTypeEnum.All)
            {
                await StreamingPlatforms.ForEachPlatform(async (p) =>
                {
                    await this.ClearPlatformMessages(p);
                });
            }
            else
            {
                await this.ClearPlatformMessages(platform);
            }

            this.messagesLookup.Clear();
            this.Messages.Clear();

            ChatService.ChatCleared();
        }

        private async Task ClearPlatformMessages(StreamingPlatformTypeEnum platform)
        {
            if (platform == StreamingPlatformTypeEnum.Twitch && ServiceManager.Get<TwitchSession>().IsConnected)
            {
                await ServiceManager.Get<TwitchSession>().ClearMessages();
            }
            if (platform == StreamingPlatformTypeEnum.Velora && ServiceManager.Get<VeloraSession>().IsConnected)
            {
                await ServiceManager.Get<VeloraSession>().ClearMessages();
            }

            if (platform == StreamingPlatformTypeEnum.VPZone && ServiceManager.Get<VPZoneSession>().IsConnected)
            {
                await ServiceManager.Get<VPZoneSession>().ClearMessages();
            }
        }

        public async Task PurgeUser(UserV2ViewModel user)
        {
            // On Velora a timeout has no effect on existing messages (the platform never clears them) and
            // announces itself in the channel's chat, so a purge is purely deleting the user's messages.
            if (user.Platform == StreamingPlatformTypeEnum.Velora && ServiceManager.Get<VeloraSession>().IsConnected)
            {
                await ServiceManager.Get<VeloraSession>().PurgeUser(user);
                return;
            }

            if (user.Platform == StreamingPlatformTypeEnum.VPZone && ServiceManager.Get<VPZoneSession>().IsConnected)
            {
                await ServiceManager.Get<VPZoneSession>().PurgeUser(user);
                return;
            }

            // Everywhere else a purge is a one-second timeout, which the platform answers by clearing the
            // user's messages; every platform session implements it.
            await this.TimeoutUser(user, 1);
        }

        public async Task TimeoutUser(UserV2ViewModel user, int durationInSeconds, string reason = null)
        {
            if (user.Platform is StreamingPlatformTypeEnum.Joystick or StreamingPlatformTypeEnum.Razo or StreamingPlatformTypeEnum.AltStream) { return; }

            if (user.Platform == StreamingPlatformTypeEnum.Twitch && ServiceManager.Get<TwitchSession>().IsConnected)
            {
                await ServiceManager.Get<TwitchSession>().TimeoutUser(user, durationInSeconds, reason);
            }

            if (user.Platform == StreamingPlatformTypeEnum.YouTube && ServiceManager.Get<YouTubeSession>().IsConnected)
            {
                await ServiceManager.Get<YouTubeSession>().TimeoutUser(user, durationInSeconds);
            }
            if (user.Platform == StreamingPlatformTypeEnum.Kick && ServiceManager.Get<KickSession>().IsConnected)
            {
                await ServiceManager.Get<KickSession>().TimeoutUser(user, durationInSeconds, reason);
            }
            if (user.Platform == StreamingPlatformTypeEnum.Velora && ServiceManager.Get<VeloraSession>().IsConnected)
            {
                await ServiceManager.Get<VeloraSession>().TimeoutUser(user, durationInSeconds, reason);
            }

            if (user.Platform == StreamingPlatformTypeEnum.VPZone && ServiceManager.Get<VPZoneSession>().IsConnected)
            {
                await ServiceManager.Get<VPZoneSession>().TimeoutUser(user, durationInSeconds, reason);
            }

            ChatService.ChatUserTimedOut(user);
        }

        public async Task ModUser(UserV2ViewModel user)
        {
            if (user.Platform == StreamingPlatformTypeEnum.Twitch && ServiceManager.Get<TwitchSession>().IsConnected)
            {
                await ServiceManager.Get<TwitchSession>().ModUser(user);
            }

            if (user.Platform == StreamingPlatformTypeEnum.YouTube && ServiceManager.Get<YouTubeSession>().IsConnected)
            {
                await ServiceManager.Get<YouTubeSession>().ModUser(user);
            }
            if (user.Platform == StreamingPlatformTypeEnum.Kick && ServiceManager.Get<KickSession>().IsConnected)
            {
                await ServiceManager.Get<KickSession>().ModUser(user);
            }
            if (user.Platform == StreamingPlatformTypeEnum.Velora && ServiceManager.Get<VeloraSession>().IsConnected)
            {
                await ServiceManager.Get<VeloraSession>().ModUser(user);
            }

            if (user.Platform == StreamingPlatformTypeEnum.VPZone && ServiceManager.Get<VPZoneSession>().IsConnected)
            {
                await ServiceManager.Get<VPZoneSession>().ModUser(user);
            }

        }

        public async Task UnmodUser(UserV2ViewModel user)
        {
            if (user.Platform == StreamingPlatformTypeEnum.Twitch && ServiceManager.Get<TwitchSession>().IsConnected)
            {
                await ServiceManager.Get<TwitchSession>().UnmodUser(user);
            }

            if (user.Platform == StreamingPlatformTypeEnum.YouTube && ServiceManager.Get<YouTubeSession>().IsConnected)
            {
                await ServiceManager.Get<YouTubeSession>().UnmodUser(user);
            }
            if (user.Platform == StreamingPlatformTypeEnum.Kick && ServiceManager.Get<KickSession>().IsConnected)
            {
                await ServiceManager.Get<KickSession>().UnmodUser(user);
            }
            if (user.Platform == StreamingPlatformTypeEnum.Velora && ServiceManager.Get<VeloraSession>().IsConnected)
            {
                await ServiceManager.Get<VeloraSession>().UnmodUser(user);
            }

            if (user.Platform == StreamingPlatformTypeEnum.VPZone && ServiceManager.Get<VPZoneSession>().IsConnected)
            {
                await ServiceManager.Get<VPZoneSession>().UnmodUser(user);
            }

        }

        public async Task BanUser(UserV2ViewModel user, string reason = null, string triggeringMessageID = null)
        {
            if (user.Platform == StreamingPlatformTypeEnum.Joystick)
            {
                if (!string.IsNullOrEmpty(triggeringMessageID) && this.messagesLookup.TryGetValue(triggeringMessageID, out var message) &&
                    message is JoystickChatMessage row && row.User?.ID == user.ID)
                    await ServiceManager.Get<JoystickSession>().ModerateMessageAuthor(row, true);
                return;
            }
            if (user.Platform is StreamingPlatformTypeEnum.Razo or StreamingPlatformTypeEnum.AltStream) { return; }

            if (user.Platform == StreamingPlatformTypeEnum.Twitch && ServiceManager.Get<TwitchSession>().IsConnected)
            {
                await ServiceManager.Get<TwitchSession>().BanUser(user, reason);
            }

            if (user.Platform == StreamingPlatformTypeEnum.YouTube && ServiceManager.Get<YouTubeSession>().IsConnected)
            {
                await ServiceManager.Get<YouTubeSession>().BanUser(user, reason);
            }
            if (user.Platform == StreamingPlatformTypeEnum.Kick && ServiceManager.Get<KickSession>().IsConnected)
            {
                await ServiceManager.Get<KickSession>().BanUser(user, reason);
            }
            if (user.Platform == StreamingPlatformTypeEnum.Velora && ServiceManager.Get<VeloraSession>().IsConnected)
            {
                await ServiceManager.Get<VeloraSession>().BanUser(user, reason);
            }

            if (user.Platform == StreamingPlatformTypeEnum.VPZone && ServiceManager.Get<VPZoneSession>().IsConnected)
            {
                await ServiceManager.Get<VPZoneSession>().BanUser(user, reason);
            }

            ChatService.ChatUserBanned(user);
        }

        public async Task UnbanUser(UserV2ViewModel user)
        {
            if (user.Platform == StreamingPlatformTypeEnum.Twitch && ServiceManager.Get<TwitchSession>().IsConnected)
            {
                await ServiceManager.Get<TwitchSession>().UnbanUser(user);
            }

            if (user.Platform == StreamingPlatformTypeEnum.YouTube && ServiceManager.Get<YouTubeSession>().IsConnected)
            {
                await ServiceManager.Get<YouTubeSession>().UnbanUser(user);
            }
            if (user.Platform == StreamingPlatformTypeEnum.Kick && ServiceManager.Get<KickSession>().IsConnected)
            {
                await ServiceManager.Get<KickSession>().UnbanUser(user);
            }
            if (user.Platform == StreamingPlatformTypeEnum.Velora && ServiceManager.Get<VeloraSession>().IsConnected)
            {
                await ServiceManager.Get<VeloraSession>().UnbanUser(user);
            }

            if (user.Platform == StreamingPlatformTypeEnum.VPZone && ServiceManager.Get<VPZoneSession>().IsConnected)
            {
                await ServiceManager.Get<VPZoneSession>().UnbanUser(user);
            }
            if (user.Platform == StreamingPlatformTypeEnum.Joystick && ServiceManager.Get<JoystickSession>().IsConnected)
            {
                await AsyncRunner.RunAsync(ServiceManager.Get<JoystickSession>().UnbanUser(user));
            }

        }

        public void RebuildCommandTriggers()
        {
            try
            {
                this.triggersToCommands.Clear();
                this.longestTrigger = 0;
                this.wildcardCommands.Clear();
                this.chatMenuCommands.Clear();
                foreach (ChatCommandModel command in ServiceManager.Get<CommandService>().AllEnabledChatAccessibleCommands)
                {
                    if (command.Wildcards)
                    {
                        this.wildcardCommands.Add(command);
                    }
                    else
                    {
                        foreach (string trigger in command.GetFullTriggers())
                        {
                            string t = trigger.ToLower();
                            this.triggersToCommands[t] = command;
                            this.longestTrigger = Math.Max(this.longestTrigger, t.Length);
                        }
                    }

                    SettingsRequirementModel settings = command.Requirements.Settings;
                    if (settings != null && settings.ShowOnChatContextMenu)
                    {
                        this.chatMenuCommands.Add(command);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            this.ChatCommandsReprocessed(this, new EventArgs());
        }

        public void DisplayMessage(ChatMessageViewModel message, bool showMessage = true)
        {
            if (!(message is AlertChatMessageViewModel) || !ChannelSession.Settings.OnlyShowAlertsInDashboard)
            {
                this.messagesLookup[message.ID] = message;
                if (showMessage)
                {
                    if (ChannelSession.Settings.LatestChatAtTop)
                    {
                        this.Messages.Insert(0, message);
                    }
                    else
                    {
                        this.Messages.Add(message);
                    }
                }

                if (this.Messages.Count > ChannelSession.Settings.MaxMessagesInChat)
                {
                    ChatMessageViewModel removedMessage = (ChannelSession.Settings.LatestChatAtTop) ? this.Messages.Last() : this.Messages.First();
                    this.messagesLookup.Remove(removedMessage.ID);
                    this.Messages.Remove(removedMessage);
                }
            }
        }

        public async Task AddMessage(ChatMessageViewModel message)
        {
            try
            {
                message.ProcessingStartTime = DateTimeOffset.Now;
                Logger.Log(LogLevel.Debug, $"Message Received - {message.Platform} - {message.ID} - {message.ProcessingStartTime} - {message.LogDescription}");

                // Pre message processing

                if (message is UserChatMessageViewModel)
                {
                    if (message.User != null)
                    {
                        message.User.UpdateLastActivity();
                        if (message.IsWhisper && ChannelSession.Settings.TrackWhispererNumber && !message.IsStreamerOrBot && message.User.WhispererNumber == 0)
                        {
                            try
                            {
                                await this.whisperNumberLock.WaitAsync();

                                if (!whisperMap.ContainsKey(message.User.ID))
                                {
                                    whisperMap[message.User.ID] = whisperMap.Count + 1;
                                }
                                message.User.WhispererNumber = whisperMap[message.User.ID];
                            }
                            catch (Exception ex)
                            {
                                Logger.Log(ex);
                            }
                            finally
                            {
                                this.whisperNumberLock.Release();
                            }
                        }
                    }
                }

                if (message.User != null)
                {
                    await ServiceManager.Get<UserService>().AddOrUpdateActiveUser(message.User);
                }

                if (message.ProcessingTime > 1000)
                {
                    Logger.Log(LogLevel.Error, string.Format("Long processing time detected for the following message (AFTER USER ADD/UPDATE): {0} - {1} ms - {2}", message.ID.ToString(), message.ProcessingTime, message.LogDescription));
                }

                // Add message to chat list
                bool showMessage = true;
                if (message.User != null && message.Platform != StreamingPlatformTypeEnum.None)
                {
                    if (ChannelSession.Settings.HideBotMessages && StreamingPlatforms.GetPlatformSession(message.Platform).IsBotUser(message.User))
                    {
                        showMessage = false;
                    }
                    else if (ChannelSession.Settings.HideWhisperMessages && message.IsWhisper)
                    {
                        showMessage = false;
                    }
                    else if (ChannelSession.Settings.HideSpecificUserMessages.Contains(message.User?.Username.ToLower()))
                    {
                        showMessage = false;
                    }
                }

                this.DisplayMessage(message, showMessage);

                if (message.ProcessingTime > 1000)
                {
                    Logger.Log(LogLevel.Error, string.Format("Long processing time detected for the following message (AFTER ALERTS): {0} - {1} ms - {2}", message.ID.ToString(), message.ProcessingTime, message.LogDescription));
                }

                // Post message processing

                if (message is UserChatMessageViewModel && message.User != null)
                {
                    await message.User.Refresh();

                    if (message.ProcessingTime > 1000)
                    {
                        Logger.Log(LogLevel.Error, string.Format("Long processing time detected for the following message (AFTER USER REFRESH): {0} - {1} ms - {2}", message.ID.ToString(), message.ProcessingTime, message.LogDescription));
                    }

                    if (message.IsWhisper)
                    {
                        if (!message.IsStreamerOrBot)
                        {
                            if (!string.IsNullOrEmpty(ChannelSession.Settings.NotificationChatWhisperSoundFilePath))
                            {
                                await ServiceManager.Get<IAudioService>().PlayNotification(ChannelSession.Settings.NotificationChatWhisperSoundFilePath, ChannelSession.Settings.NotificationChatWhisperSoundVolume);
                            }

                            if (!string.IsNullOrEmpty(message.PlainTextMessage))
                            {
                                await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.ChatWhisperReceived, new CommandParametersModel(message));
                            }

                            // Don't send this if it's in response to another "You are whisperer #" message
                            if (ChannelSession.Settings.TrackWhispererNumber && message.User.WhispererNumber > 0 && !message.PlainTextMessage.StartsWith("You are whisperer #", StringComparison.InvariantCultureIgnoreCase))
                            {
                                await ServiceManager.Get<ChatService>().Whisper(message.User, string.Format(MixItUp.Base.Resources.ChatWhisperNumberResponse, message.User.WhispererNumber), sendAsStreamer: false);
                            }
                        }
                    }
                    else
                    {
                        message.User.TotalChatMessageSent++;

                        if (this.DisableChat)
                        {
                            Logger.Log(LogLevel.Debug, string.Format("Deleting Message As Chat Disabled - {0} - {1}", message.ID, message.LogDescription));
                            await this.DeleteMessage(message);
                            return;
                        }

                        if (await message.CheckForModeration())
                        {
                            await this.DeleteMessage(message);
                            return;
                        }

                        if (!string.IsNullOrEmpty(ChannelSession.Settings.NotificationChatTaggedSoundFilePath) && message.IsStreamerTagged)
                        {
                            await ServiceManager.Get<IAudioService>().PlayNotification(ChannelSession.Settings.NotificationChatTaggedSoundFilePath, ChannelSession.Settings.NotificationChatTaggedSoundVolume);
                        }
                        else if (!string.IsNullOrEmpty(ChannelSession.Settings.NotificationChatMessageSoundFilePath) && !message.User.IsSpecialtyExcluded)
                        {
                            await ServiceManager.Get<IAudioService>().PlayNotification(ChannelSession.Settings.NotificationChatMessageSoundFilePath, ChannelSession.Settings.NotificationChatMessageSoundVolume);
                        }

                        if (message.ProcessingTime > 1000)
                        {
                            Logger.Log(LogLevel.Error, string.Format("Long processing time detected for the following message (AFTER MODERATION/NOTIFICATIONS): {0} - {1} ms - {2}", message.ID.ToString(), message.ProcessingTime, message.LogDescription));
                        }

                        if (message.User.TotalChatMessageSent == 1)
                        {
                            await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.ChatUserFirstMessage, new CommandParametersModel(message));

                            await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(message.User, string.Format(MixItUp.Base.Resources.AlertUserFirstMessage, message.User.FullDisplayName), ChannelSession.Settings.AlertUserFirstMessageColor));
                        }

                        if (!this.userEntranceCommands.Contains(message.User.ID))
                        {
                            if (!ChannelSession.Settings.UserEntranceCommandsOnlyWhenLive || StreamingPlatforms.GetPlatformSession(message.User.Platform).IsLive)
                            {
                                this.userEntranceCommands.Add(message.User.ID);

                                CommandModelBase customEntranceCommand = ChannelSession.Settings.GetCommand(message.User.EntranceCommandID);
                                if (customEntranceCommand != null && customEntranceCommand.IsEnabled)
                                {
                                    await ServiceManager.Get<CommandService>().Queue(message.User.EntranceCommandID, new CommandParametersModel(message));
                                }
                                else if (!message.User.IsSpecialtyExcluded)
                                {
                                    await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.ChatUserEntranceCommand, new CommandParametersModel(message));
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(message.PlainTextMessage))
                        {
                            await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.ChatMessageReceived, new CommandParametersModel(message));
                        }

                        string primaryTaggedUsername = message.PrimaryTaggedUsername;
                        if (!string.IsNullOrEmpty(primaryTaggedUsername))
                        {
                            UserV2ViewModel primaryTaggedUser = ServiceManager.Get<UserService>().GetActiveUserByPlatform(message.Platform, platformUsername: primaryTaggedUsername);
                            if (primaryTaggedUser != null)
                            {
                                primaryTaggedUser.TotalTimesTagged++;
                            }
                        }

                        if (message.ProcessingTime > 1000)
                        {
                            Logger.Log(LogLevel.Error, string.Format("Long processing time detected for the following message (AFTER EVENT COMMANDS): {0} - {1} ms - {2}", message.ID.ToString(), message.ProcessingTime, message.LogDescription));
                        }
                    }

                    ChatService.ChatMessageReceived(message);

                    await this.WriteToChatEventLog(message);

                    if (message.ProcessingTime > 1000)
                    {
                        Logger.Log(LogLevel.Error, string.Format("Long processing time detected for the following message (AFTER CHAT MESSAGE GLOBAL EVENT): {0} - {1} ms - {2}", message.ID.ToString(), message.ProcessingTime, message.LogDescription));
                    }

                    IEnumerable<string> arguments = null;
#pragma warning disable CS0612 // Type or member is obsolete
                    if (!string.IsNullOrEmpty(message.PlainTextMessage) && !message.User.HasRole(UserRoleEnum.Banned))
#pragma warning restore CS0612 // Type or member is obsolete
                    {
                        if (!ChannelSession.Settings.AllowCommandWhispering && message.IsWhisper)
                        {
                            return;
                        }

                        if (ChannelSession.Settings.TwitchIgnoreRewardInputCommands && message is TwitchChatMessageViewModel { IsCustomRewardInput: true })
                        {
                            return;
                        }

                        if (ChannelSession.Settings.IgnoreBotAccountCommands && message.Platform != StreamingPlatformTypeEnum.None)
                        {
                            if (StreamingPlatforms.GetPlatformSession(message.Platform).IsBotUser(message.User))
                            {
                                return;
                            }
                        }

                        Logger.Log(LogLevel.Debug, string.Format("Checking Message For Command - {0} - {1}", message.ID, message.LogDescription));

                        bool commandTriggered = false;
                        if (message.User.CustomCommandIDs.Count > 0)
                        {
                            Dictionary<string, CommandModelBase> userOnlyTriggersToCommands = new Dictionary<string, CommandModelBase>();
                            List<ChatCommandModel> userOnlyWildcardCommands = new List<ChatCommandModel>();
                            foreach (Guid commandID in message.User.CustomCommandIDs)
                            {
                                ChatCommandModel command = (ChatCommandModel)ChannelSession.Settings.GetCommand(commandID);
                                if (command != null && command.IsEnabled)
                                {
                                    if (command.Wildcards)
                                    {
                                        userOnlyWildcardCommands.Add(command);
                                    }
                                    else
                                    {
                                        foreach (string trigger in command.GetFullTriggers())
                                        {
                                            userOnlyTriggersToCommands[trigger.ToLower()] = command;
                                        }
                                    }
                                }
                            }

                            if (!commandTriggered && userOnlyTriggersToCommands.Count > 0)
                            {
                                commandTriggered = await this.CheckForChatCommandAndRun(message, userOnlyTriggersToCommands, ignoreTriggerLengthCheck: true);
                            }

                            if (!commandTriggered && userOnlyWildcardCommands.Count > 0)
                            {
                                foreach (ChatCommandModel command in userOnlyWildcardCommands)
                                {
                                    if (command.DoesMessageMatchWildcardTriggers(message, out arguments))
                                    {
                                        await this.RunChatCommand(message, command, arguments);
                                        commandTriggered = true;
                                        break;
                                    }
                                }
                            }
                        }

                        if (!commandTriggered)
                        {
                            commandTriggered = await this.CheckForChatCommandAndRun(message, this.triggersToCommands);
                        }

                        if (!commandTriggered)
                        {
                            foreach (ChatCommandModel command in this.wildcardCommands)
                            {
                                if (command.DoesMessageMatchWildcardTriggers(message, out arguments))
                                {
                                    await this.RunChatCommand(message, command, arguments);
                                    commandTriggered = true;
                                    break;
                                }
                            }
                        }

                        if (message.ProcessingTime > 1000)
                        {
                            Logger.Log(LogLevel.Error, string.Format("Long processing time detected for the following message (AFTER CHAT COMMAND PROCESSING): {0} - {1} ms - {2}", message.ID.ToString(), message.ProcessingTime, message.LogDescription));
                        }
                    }

                    foreach (InventoryModel inventory in ChannelSession.Settings.Inventory.Values.ToList())
                    {
                        if (inventory.ShopEnabled && ChatCommandModel.DoesMessageMatchTriggers(message, new List<string>() { inventory.ShopCommand }, out arguments))
                        {
                            await inventory.PerformShopCommand(message.User, arguments);
                        }
                        else if (inventory.TradeEnabled && ChatCommandModel.DoesMessageMatchTriggers(message, new List<string>() { inventory.TradeCommand }, out arguments))
                        {
                            await inventory.PerformTradeCommand(message.User, arguments);
                        }
                    }

                    if (ChannelSession.Settings.RedemptionStoreEnabled)
                    {
                        if (ChatCommandModel.DoesMessageMatchTriggers(message, new List<string>() { ChannelSession.Settings.RedemptionStoreChatPurchaseCommand }, out arguments))
                        {
                            await RedemptionStorePurchaseModel.Purchase(message.User, arguments);
                        }
                        else if (ChatCommandModel.DoesMessageMatchTriggers(message, new List<string>() { ChannelSession.Settings.RedemptionStoreModRedeemCommand }, out arguments))
                        {
                            await RedemptionStorePurchaseModel.Redeem(message.User, arguments);
                        }
                    }
                }

                Logger.Log(LogLevel.Debug, string.Format("Message Processing Complete: {0} - {1} ms", message.ID, message.ProcessingTime));
                if (message.ProcessingTime > 1000)
                {
                    Logger.Log(LogLevel.Error, string.Format("Long processing time detected for the following message: {0} - {1} ms - {2}", message.ID.ToString(), message.ProcessingTime, message.LogDescription));
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        public async Task RemoveMessage(string messageID)
        {
            if (!string.IsNullOrEmpty(messageID) && this.messagesLookup.ContainsKey(messageID))
            {
                await this.RemoveMessage(this.messagesLookup[messageID]);
            }
        }

        public Task RemoveMessage(ChatMessageViewModel message)
        {
            this.messagesLookup.Remove(message.ID);
            this.Messages.Remove(message);
            return Task.CompletedTask;
        }

        public void HandleDeletion(ChatMessageViewModel message)
        {
            if (message.IsDeleted)
            {
                int index = this.Messages.IndexOf(message);
                if (index >= 0)
                {
                    this.Messages.Remove(message);
                    this.Messages.Insert(index, message);
                }
            }
        }

        public async Task WriteToChatEventLog(ChatMessageViewModel message, string prepend = null)
        {
            if (ChannelSession.Settings.SaveChatEventLogs)
            {
                try
                {
                    string timestamp = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture);
                    await ServiceManager.Get<IFileService>().AppendFile(this.currentChatEventLogFilePath, $"{prepend}{message.LogDescription} ({timestamp}){Environment.NewLine}");
                }
                catch (Exception) { }
            }
        }

        private async Task<bool> CheckForChatCommandAndRun(ChatMessageViewModel message, Dictionary<string, CommandModelBase> commands, bool ignoreTriggerLengthCheck = false)
        {
            string[] messageParts = message.PlainTextMessage.Split(new char[] { ' ' });
            for (int i = 0; i < messageParts.Length; i++)
            {
                string commandCheck = string.Join(" ", messageParts.Take(i + 1)).ToLower();
                if (!ignoreTriggerLengthCheck && commandCheck.Length > this.longestTrigger)
                {
                    return false;
                }

                if (commands.ContainsKey(commandCheck))
                {
                    await this.RunChatCommand(message, commands[commandCheck], messageParts.Skip(i + 1));
                    return true;
                }
            }
            return false;
        }

        private async Task RunChatCommand(ChatMessageViewModel message, CommandModelBase command, IEnumerable<string> arguments)
        {
            Logger.Log(LogLevel.Debug, string.Format("Command Found For Message - {0} - {1} - {2}", message.ID, message.LogDescription, command));

            CommandParametersModel parameters = new CommandParametersModel(message, arguments); // Overwrite arguments to account for variable argument length for commands
            await ServiceManager.Get<CommandService>().Queue(command, parameters);

            SettingsRequirementModel settings = command.Requirements.Settings;
            if (settings != null)
            {
                if (settings != null && settings.ShouldChatMessageBeDeletedWhenRun)
                {
                    await this.DeleteMessage(message);
                }
            }
        }

        private Task MinuteBackgroundThread(CancellationToken cancellationToken)
        {
            Dictionary<StreamingPlatformTypeEnum, bool> liveStreams = new Dictionary<StreamingPlatformTypeEnum, bool>();
            Dictionary<StreamingPlatformTypeEnum, int> chatterCount = new Dictionary<StreamingPlatformTypeEnum, int>();

            StreamingPlatforms.ForEachPlatform(p =>
            {
                liveStreams[p] = StreamingPlatforms.GetPlatformSession(p).IsConnected && StreamingPlatforms.GetPlatformSession(p).IsLive;
                chatterCount[p] = 0;

                Logger.Log(LogLevel.Debug, $"{p} Stream Status: {liveStreams[p]}");
            });

            if (liveStreams.Any(s => s.Value))
            {
                Logger.Log(LogLevel.Debug, $"A valid live stream has been detected, starting minute background processing");

                foreach (UserV2ViewModel user in ServiceManager.Get<UserService>().GetActiveUsers())
                {
                    if (liveStreams.TryGetValue(user.Platform, out bool active) && active)
                    {
                        user.UpdateViewingMinutes(liveStreams);
                        chatterCount[user.Platform]++;
                    }
                }

                foreach (CurrencyModel currency in ChannelSession.Settings.Currency.Values)
                {
                    currency.UpdateUserData(liveStreams);
                }

                foreach (StreamPassModel streamPass in ChannelSession.Settings.StreamPass.Values)
                {
                    streamPass.UpdateUserData(liveStreams);
                }

                StreamingPlatforms.ForEachPlatform(p =>
                {
                    if (liveStreams[p])
                    {
                        ServiceManager.Get<StatisticsService>().LogStatistic(StatisticItemTypeEnum.Viewers, platform: p, amount: StreamingPlatforms.GetPlatformSession(p).StreamViewerCount);
                        ServiceManager.Get<StatisticsService>().LogStatistic(StatisticItemTypeEnum.Chatters, platform: p, amount: chatterCount[p]);
                    }
                });
            }

            return Task.CompletedTask;
        }
    }
}

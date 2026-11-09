using MixItUp.Base.Model;
using MixItUp.Base.Model.Settings;
using MixItUp.Base.Model.Twitch.ChannelPoints;
using MixItUp.Base.Model.Web;
using MixItUp.Base.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Base.Services.Twitch.New
{
    public static class TwitchAuthenticationMigrationService
    {
        public static Task<Result> Migrate(SettingsV3Model settings)
        {
            return SettingsUpdateService.WithStrictDatabaseErrors(() => MigrateInternal(settings));
        }

        private static async Task<Result> MigrateInternal(SettingsV3Model settings)
        {
            if (!settings.StreamingPlatformAuthentications.TryGetValue(StreamingPlatformTypeEnum.Twitch, out var authentication) ||
                authentication.UserOAuthToken == null)
            {
                return new Result();
            }

            TwitchService oldService = new TwitchService(TwitchSession.StreamerScopes, useLegacyApplication: true);
            TwitchService newService = new TwitchService(TwitchSession.StreamerScopes);
            TwitchService botService = new TwitchService(TwitchSession.BotScopes, isBotService: true);
            string phase = "authorization";
            TwitchRewardMigrationModel activeReward = null;
            try
            {
                if (string.IsNullOrWhiteSpace(newService.ClientID) || string.IsNullOrWhiteSpace(newService.ClientSecret))
                {
                    throw new InvalidOperationException("Updated Twitch application credentials are missing");
                }
                await TwitchRewardMigrationRecovery.Restore(settings, authentication.UserID);
                var state = settings.TwitchAuthenticationMigration;
                bool migrateStreamer = IsLegacy(authentication.UserOAuthToken);
                bool migrateBot = authentication.BotOAuthToken != null && IsLegacy(authentication.BotOAuthToken);
                bool rewardsAlreadyMigrated = state?.Completed == true;
                if (rewardsAlreadyMigrated && !migrateStreamer && !migrateBot)
                {
                    if (authentication.UserOAuthToken.clientID != newService.ClientID)
                    {
                        throw new InvalidOperationException(Resources.TwitchMigrationAccountMismatch);
                    }
                    phase = "command mapping";
                    await RemapCommands(settings, state.Rewards.ToDictionary(r => r.Original.id, r => r.NewRewardID));
                    phase = "credential cleanup";
                    await ClearStoredCredentials(settings);
                    return new Result();
                }
                if (state == null && !migrateStreamer && !migrateBot)
                {
                    if (authentication.UserOAuthToken.clientID != newService.ClientID)
                    {
                        throw new InvalidOperationException(Resources.TwitchMigrationAccountMismatch);
                    }
                    return new Result();
                }
                if (state == null)
                {
                    state = new TwitchAuthenticationMigrationModel { UserID = authentication.UserID };
                    settings.TwitchAuthenticationMigration = state;
                    await SaveProgress(settings);
                }
                if (string.IsNullOrEmpty(state.UserID) || state.UserID != authentication.UserID)
                {
                    throw new InvalidOperationException(Resources.TwitchMigrationAccountMismatch);
                }
                if (rewardsAlreadyMigrated && migrateBot) { state.AuthenticationConfirmed = false; }

                string streamerName = null;
                if (migrateStreamer && !rewardsAlreadyMigrated)
                {
                    JObject validation = await Authorize(oldService, authentication.UserOAuthToken, TwitchSession.StreamerScopes, state.UserID);
                    streamerName = (string)validation["login"];
                    authentication.UserOAuthToken = oldService.GetOAuthTokenCopy();
                    await SaveProgress(settings);
                }
                JObject userValidation = await newService.ValidateMigrationToken(state.NewUserToken ?? (migrateStreamer ? null : authentication.UserOAuthToken),
                    TwitchSession.StreamerScopes, state.UserID);
                JObject botValidation = null;
                bool hasBot = authentication.BotOAuthToken != null;
                if (hasBot)
                {
                    if (string.IsNullOrEmpty(authentication.BotID)) { throw new InvalidOperationException(Resources.TwitchMigrationAccountMismatch); }
                    botValidation = await botService.ValidateMigrationToken(state.NewBotToken ?? (migrateBot ? null : authentication.BotOAuthToken),
                        TwitchSession.BotScopes, authentication.BotID);
                }

                string botName = (string)botValidation?["login"];
                if (hasBot && botName == null && ((migrateStreamer && !rewardsAlreadyMigrated) || userValidation != null))
                {
                    botName = (await (migrateStreamer && !rewardsAlreadyMigrated ? oldService : newService)
                        .GetNewAPIUserByID(authentication.BotID))?.login;
                }
                var prompt = new TwitchAuthenticationMigrationDialogModel
                {
                    StreamerName = streamerName ?? (string)userValidation?["login"] ?? Resources.StreamerAccount,
                    HasBot = hasBot,
                    BotName = hasBot ? botName ?? Resources.BotAccount : null,
                    StreamerAuthenticated = userValidation != null,
                    BotAuthenticated = botValidation != null,
                };
                if (userValidation != null) { state.NewUserToken = newService.GetOAuthTokenCopy(); }
                if (botValidation != null) { state.NewBotToken = botService.GetOAuthTokenCopy(); }
                prompt.LoginStreamer = token => LoginFromDialog(newService, state.UserID, TwitchSession.StreamerScopes, token, async () =>
                {
                    state.NewUserToken = newService.GetOAuthTokenCopy();
                    await SaveProgress(settings);
                });
                prompt.LoginBot = token => LoginFromDialog(botService, authentication.BotID, TwitchSession.BotScopes, token, async () =>
                {
                    state.NewBotToken = botService.GetOAuthTokenCopy();
                    await SaveProgress(settings);
                });
                if (!state.AuthenticationConfirmed || !prompt.StreamerAuthenticated || (state.NewBotToken != null && !prompt.BotAuthenticated))
                {
                    if (!await DialogHelper.ShowTwitchAuthenticationMigration(prompt) || !prompt.CanContinue)
                    {
                        return new Result(Resources.TwitchMigrationFailed);
                    }
                }
                if (!prompt.BotAuthenticated) { state.NewBotToken = null; }
                state.AuthenticationConfirmed = true;
                state.CredentialsCleared = false;
                await SaveProgress(settings);

                if (!rewardsAlreadyMigrated)
                {
                    // Read all command JSON before any remote deletion. Unknown command fields are preserved.
                    phase = "inventory";
                    await ReadCommands(settings);
                    if (!state.InventorySaved)
                    {
                        List<CustomChannelPointRewardModel> originals = migrateStreamer
                            ? await oldService.GetMigrationRewards(state.UserID) : new List<CustomChannelPointRewardModel>();
                        List<CustomChannelPointRewardModel> replacements = await newService.GetMigrationRewards(state.UserID);
                        if (originals.Any(o => replacements.Any(n => string.Equals(n.title, o.title, StringComparison.OrdinalIgnoreCase))))
                        {
                            throw new InvalidOperationException("A replacement reward title is already in use");
                        }
                        state.ExistingNewRewardIDs = replacements.Select(r => r.id).ToList();
                        state.Rewards = originals.Select(r => new TwitchRewardMigrationModel { Original = r }).ToList();
                        state.InventorySaved = true;
                        await SaveProgress(settings);
                    }

                    foreach (TwitchRewardMigrationModel reward in state.Rewards)
                    {
                        activeReward = reward;
                        phase = "reward recreation";
                        List<CustomChannelPointRewardModel> oldRewards = migrateStreamer
                            ? await oldService.GetMigrationRewards(state.UserID) : new List<CustomChannelPointRewardModel>();
                        CustomChannelPointRewardModel original = oldRewards.FirstOrDefault(r => r.id == reward.Original.id);
                        if (original != null)
                        {
                            if (reward.NewRewardID != Guid.Empty || !SameDefinition(original, reward.Original) ||
                                original.is_enabled != reward.Original.is_enabled || original.is_paused != reward.Original.is_paused)
                            {
                                throw new InvalidOperationException("The original reward changed during migration");
                            }
                            reward.DeleteStarted = true;
                            await SaveProgress(settings);
                            await oldService.DeleteMigrationReward(state.UserID, original.id);
                        }
                        else if (!reward.DeleteStarted)
                        {
                            throw new InvalidOperationException("The original reward is missing");
                        }

                        List<CustomChannelPointRewardModel> newRewards = await newService.GetMigrationRewards(state.UserID);
                        if (reward.NewRewardID == Guid.Empty)
                        {
                            CustomChannelPointRewardModel replacement = newRewards.SingleOrDefault(r => string.Equals(r.title, reward.Original.title, StringComparison.OrdinalIgnoreCase));
                            if (replacement != null)
                            {
                                // A timed-out create can have succeeded on Twitch. Adopt only a matching result from this run.
                                if (!reward.CreateStarted || state.ExistingNewRewardIDs.Contains(replacement.id) || !SameDefinition(replacement, TwitchRewardMigrationRecovery.ReplacementDefinition(reward)))
                                {
                                    throw new InvalidOperationException("An unrelated replacement reward already exists");
                                }
                            }
                            else
                            {
                                reward.CreateStarted = true;
                                await SaveProgress(settings);
                                replacement = await TwitchRewardMigrationRecovery.CreateReplacement(reward,
                                    fields => newService.CreateMigrationReward(state.UserID, fields),
                                    prompt => DialogHelper.ShowTextEntry(Resources.Description, prompt,
                                        string.Format(Resources.TwitchMigrationRewardPromptRejected, reward.Original.title)),
                                    () => SaveProgress(settings));
                            }
                            reward.NewRewardID = replacement.id;
                            await SaveProgress(settings);
                        }
                        else if (!newRewards.Any(r => r.id == reward.NewRewardID && SameDefinition(r, TwitchRewardMigrationRecovery.ReplacementDefinition(reward))))
                        {
                            throw new InvalidOperationException("The replacement reward changed during migration");
                        }
                        phase = "command mapping";
                        await RemapCommands(settings, new Dictionary<Guid, Guid> { [reward.Original.id] = reward.NewRewardID });
                    }
                    activeReward = null;

                    Dictionary<Guid, Guid> mapping = state.Rewards.ToDictionary(r => r.Original.id, r => r.NewRewardID);
                    phase = "command mapping";
                    await RemapCommands(settings, mapping);
                    phase = "reward verification";
                    foreach (TwitchRewardMigrationModel reward in state.Rewards)
                    {
                        await newService.RestoreMigrationReward(state.UserID, reward.NewRewardID, reward.Original.is_enabled, reward.Original.is_paused);
                    }
                    if (state.Rewards.Count > 0)
                    {
                        List<CustomChannelPointRewardModel> verified = await newService.GetMigrationRewards(state.UserID);
                        if (state.Rewards.Any(r => !verified.Any(n => n.id == r.NewRewardID && SameDefinition(n, TwitchRewardMigrationRecovery.ReplacementDefinition(r)) &&
                            n.is_enabled == r.Original.is_enabled && n.is_paused == r.Original.is_paused)))
                        {
                            throw new InvalidOperationException("Replacement reward verification failed");
                        }
                    }
                }

                // Completed remote migration can outlive a restored command database or legacy credentials.
                if (rewardsAlreadyMigrated)
                {
                    phase = "command mapping";
                    await RemapCommands(settings, state.Rewards.ToDictionary(r => r.Original.id, r => r.NewRewardID));
                }

                authentication.UserOAuthToken = state.NewUserToken;
                authentication.BotOAuthToken = state.NewBotToken;
                if (authentication.BotOAuthToken == null) { authentication.BotID = null; }
                state.NewUserToken = null;
                state.NewBotToken = null;
                state.Completed = true;
                phase = "credential cleanup";
                await SaveProgress(settings);
                await ClearStoredCredentials(settings);
                Logger.Log(LogLevel.Information, $"Twitch application migration completed for {state.Rewards.Count} rewards.");
                return new Result();
            }
            catch (Exception ex)
            {
                // Keep credentials, request bodies and token responses out of migration diagnostics.
                Logger.Log(LogLevel.Error, "Twitch application migration stopped during " + phase + ": " + ex.GetType().Name);
                if (activeReward != null)
                {
                    return new Result(string.Format(ex is TwitchRewardMigrationPausedException
                        ? Resources.TwitchMigrationRewardPaused : Resources.TwitchMigrationRewardFailed, activeReward.Original.title));
                }
                return new Result(ex.Message == Resources.TwitchMigrationAccountMismatch ? ex.Message : Resources.TwitchMigrationFailed);
            }
            finally
            {
                await oldService.Disable();
                await newService.Disable();
                await botService.Disable();
            }
        }

        private static bool IsLegacy(OAuthTokenModel token)
        {
            return string.IsNullOrEmpty(token.clientID) || token.clientID == TwitchService.LegacyClientID;
        }

        private static async Task<JObject> Authorize(TwitchService service, OAuthTokenModel token, IEnumerable<string> scopes, string userID, CancellationToken cancellationToken = default)
        {
            JObject validation = await service.ValidateMigrationToken(token, scopes, userID);
            if (validation == null)
            {
                Result result = await service.ManualConnect(cancellationToken);
                if (!result.Success) { throw new InvalidOperationException(Resources.TwitchMigrationFailed); }
                validation = await service.ValidateMigrationToken(service.GetOAuthTokenCopy(), scopes, userID);
                if (validation == null) { throw new InvalidOperationException(Resources.TwitchMigrationAccountMismatch); }
            }
            cancellationToken.ThrowIfCancellationRequested();
            return validation;
        }

        private static async Task<Result> LoginFromDialog(TwitchService service, string userID, IEnumerable<string> scopes,
            CancellationToken cancellationToken, Func<Task> save)
        {
            try
            {
                await Authorize(service, null, scopes, userID, cancellationToken);
                await save();
                return new Result();
            }
            catch (Exception ex)
            {
                return new Result(ex.Message == Resources.TwitchMigrationAccountMismatch ? ex.Message : Resources.TwitchMigrationFailed);
            }
        }

        internal static JObject RewardFields(CustomChannelPointRewardModel reward)
        {
            JObject result = new JObject
            {
                ["title"] = reward.title,
                ["cost"] = reward.cost,
                ["prompt"] = reward.prompt ?? string.Empty,
                ["background_color"] = reward.background_color,
                ["is_user_input_required"] = reward.is_user_input_required,
                ["should_redemptions_skip_request_queue"] = reward.should_redemptions_skip_request_queue,
                ["is_max_per_stream_enabled"] = reward.max_per_stream_setting?.is_enabled ?? false,
                ["is_max_per_user_per_stream_enabled"] = reward.max_per_user_per_stream_setting?.is_enabled ?? false,
                ["is_global_cooldown_enabled"] = reward.global_cooldown_setting?.is_enabled ?? false,
            };
            if (reward.max_per_stream_setting?.max_per_stream > 0) { result["max_per_stream"] = reward.max_per_stream_setting.max_per_stream; }
            if (reward.max_per_user_per_stream_setting?.max_per_user_per_stream > 0) { result["max_per_user_per_stream"] = reward.max_per_user_per_stream_setting.max_per_user_per_stream; }
            if (reward.global_cooldown_setting?.global_cooldown_seconds > 0) { result["global_cooldown_seconds"] = reward.global_cooldown_setting.global_cooldown_seconds; }
            return result;
        }

        private static bool SameDefinition(CustomChannelPointRewardModel first, CustomChannelPointRewardModel second)
        {
            JObject Normalize(CustomChannelPointRewardModel reward)
            {
                JObject fields = RewardFields(reward);
                if (!(bool)fields["is_max_per_stream_enabled"]) { fields.Remove("max_per_stream"); }
                if (!(bool)fields["is_max_per_user_per_stream_enabled"]) { fields.Remove("max_per_user_per_stream"); }
                if (!(bool)fields["is_global_cooldown_enabled"]) { fields.Remove("global_cooldown_seconds"); }
                fields["background_color"] = reward.background_color?.ToUpperInvariant();
                return fields;
            }
            return JToken.DeepEquals(Normalize(first), Normalize(second));
        }

        private static Task SaveProgress(SettingsV3Model settings)
        {
            return SettingsService.WithFileSnapshot(async () =>
            {
                // Sessions are not initialized yet, so CopyLatestValues would erase their saved tokens.
                await FileSerializerHelper.SerializeSettingsToFile(settings.SettingsFilePath, settings);
                await TwitchRewardMigrationRecovery.Save(settings);
                File.Copy(settings.SettingsFilePath, settings.SettingsLocalBackupFilePath, overwrite: true);
            });
        }

        private static async Task<Dictionary<string, string>> ReadCommands(SettingsV3Model settings)
        {
            var commands = new Dictionary<string, string>();
            await ServiceManager.Get<IDatabaseService>().Read(settings.DatabaseFilePath, "SELECT ID, Data FROM Commands", row =>
            {
                string json = (string)row["Data"];
                JObject.Parse(json);
                commands.Add(row["ID"].ToString(), json);
            });
            return commands;
        }

        internal static string RemapCommand(string json, IReadOnlyDictionary<Guid, Guid> mapping)
        {
            JObject command = JObject.Parse(json);
            bool changed = false;
            foreach (JProperty property in command.Descendants().OfType<JProperty>().Where(p => p.Name == "ChannelPointRewardID"))
            {
                string type = (string)property.Parent["$type"];
                if (type != null && (type.StartsWith("MixItUp.Base.Model.Commands.TwitchChannelPointsCommandModel,", StringComparison.Ordinal) ||
                    type.StartsWith("MixItUp.Base.Model.Actions.TwitchActionModel,", StringComparison.Ordinal)) &&
                    Guid.TryParse((string)property.Value, out Guid oldID) && mapping.TryGetValue(oldID, out Guid newID))
                {
                    property.Value = newID.ToString();
                    changed = true;
                }
            }
            return changed ? command.ToString(Formatting.None) : json;
        }

        private static async Task RemapCommands(SettingsV3Model settings, IReadOnlyDictionary<Guid, Guid> mapping)
        {
            if (mapping.Count == 0) { return; }
            Dictionary<string, string> commands = await ReadCommands(settings);
            var updates = commands.Select(c => new { c.Key, Before = c.Value, After = RemapCommand(c.Value, mapping) })
                .Where(c => c.Before != c.After).ToList();
            await ServiceManager.Get<IDatabaseService>().BulkWrite(settings.DatabaseFilePath, "UPDATE Commands SET Data = $Data WHERE ID = $ID",
                updates.Select(c => new Dictionary<string, object> { ["$ID"] = c.Key, ["$Data"] = c.After }));
            Dictionary<string, string> verified = await ReadCommands(settings);
            if (updates.Any(c => !verified.TryGetValue(c.Key, out string value) || value != c.After) ||
                verified.Values.Any(c => RemapCommand(c, mapping) != c))
            {
                throw new IOException("Twitch command mapping was not saved");
            }
        }

        private static async Task ClearStoredCredentials(SettingsV3Model settings)
        {
            var state = settings.TwitchAuthenticationMigration;
            if (state.CredentialsCleared && state.CredentialCleanupVersion >= 1) { return; }
            await ClearHistoryCredentials(settings.DatabaseFilePath);

            // Pre-upgrade snapshots are owned by this profile. Exported backups remain user-owned.
            string backupRoot = Path.Combine(Path.GetDirectoryName(settings.SettingsFilePath), "MigrationBackups");
            if (Directory.Exists(backupRoot) && (File.GetAttributes(backupRoot) & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("Linked migration backup directory");
            }
            string root = Path.Combine(backupRoot, settings.ID.ToString());
            if (Directory.Exists(root))
            {
                var directories = new Stack<DirectoryInfo>();
                directories.Push(new DirectoryInfo(root));
                while (directories.Count > 0)
                {
                    DirectoryInfo directory = directories.Pop();
                    if ((directory.Attributes & FileAttributes.ReparsePoint) != 0) { throw new IOException("Linked migration backup directory"); }
                    foreach (FileSystemInfo entry in directory.EnumerateFileSystemInfos())
                    {
                        if ((entry.Attributes & FileAttributes.ReparsePoint) != 0) { throw new IOException("Linked migration backup entry"); }
                        if (entry is DirectoryInfo child) { directories.Push(child); }
                        else if (entry.Name.EndsWith(".db3", StringComparison.OrdinalIgnoreCase))
                        {
                            await ClearHistoryCredentials(entry.FullName);
                        }
                        else if (entry.Name.EndsWith(".miu3", StringComparison.OrdinalIgnoreCase) ||
                            entry.Name.EndsWith(".miu3.backup", StringComparison.OrdinalIgnoreCase))
                        {
                            JObject snapshot = JObject.Parse(await File.ReadAllTextAsync(entry.FullName));
                            if (!Guid.TryParse((string)snapshot["ID"], out Guid id) || id != settings.ID)
                            {
                                throw new InvalidDataException("Migration backup belongs to another profile");
                            }
                            if (snapshot["StreamingPlatformAuthentications"]?["Twitch"] is JObject twitch)
                            {
                                twitch.Remove("UserOAuthToken");
                                twitch.Remove("BotOAuthToken");
                            }
                            snapshot.Remove("TwitchAuthenticationMigration");
                            string temporary = entry.FullName + ".cleanup";
                            await File.WriteAllTextAsync(temporary, snapshot.ToString(Formatting.None));
                            File.Move(temporary, entry.FullName, overwrite: true);
                        }
                    }
                }
            }
            int previousVersion = state.CredentialCleanupVersion;
            bool previouslyCleared = state.CredentialsCleared;
            try
            {
                state.CredentialCleanupVersion = 1;
                state.CredentialsCleared = true;
                await SaveProgress(settings);
            }
            catch
            {
                state.CredentialCleanupVersion = previousVersion;
                state.CredentialsCleared = previouslyCleared;
                throw;
            }
        }

        private static async Task ClearHistoryCredentials(string databasePath)
        {
            bool historyExists = false;
            await ServiceManager.Get<IDatabaseService>().Read(databasePath,
                "SELECT name FROM sqlite_master WHERE type = 'table' AND name = 'SettingsBackupHistory'", row => historyExists = true);
            if (historyExists)
            {
                bool invalidHistory = false;
                await ServiceManager.Get<IDatabaseService>().Read(databasePath,
                    "SELECT ID FROM SettingsBackupHistory WHERE NOT json_valid(SettingsJSON) LIMIT 1", row => invalidHistory = true);
                if (invalidHistory) { throw new InvalidDataException("Unreadable settings backup history"); }
                // Historical snapshots cannot restore obsolete Twitch credentials or migration progress.
                await ServiceManager.Get<IDatabaseService>().Write(databasePath,
                    "UPDATE SettingsBackupHistory SET SettingsJSON = json_remove(SettingsJSON, '$.StreamingPlatformAuthentications.Twitch.UserOAuthToken', '$.StreamingPlatformAuthentications.Twitch.BotOAuthToken', '$.TwitchAuthenticationMigration') WHERE json_valid(SettingsJSON)");
            }
        }
    }
}

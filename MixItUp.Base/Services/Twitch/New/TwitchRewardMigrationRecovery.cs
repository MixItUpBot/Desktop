using MixItUp.Base.Model;
using MixItUp.Base.Model.Settings;
using MixItUp.Base.Model.Twitch.ChannelPoints;
using MixItUp.Base.Model.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace MixItUp.Base.Services.Twitch.New
{
    // Kept outside settings backups: restoring local commands cannot undo Twitch changes.
    internal static class TwitchRewardMigrationRecovery
    {
        private sealed class RecoveryRecord
        {
            public int Version { get; set; } = 1;
            public Guid ProfileID { get; set; }
            public string UserID { get; set; }
            public bool InventorySaved { get; set; }
            public List<Guid> ExistingNewRewardIDs { get; set; }
            public List<TwitchRewardMigrationModel> Rewards { get; set; }
            public bool Completed { get; set; }
        }

        internal static string GetPath(SettingsV3Model settings) => Path.Combine(
            Path.GetDirectoryName(settings.SettingsFilePath), "TwitchMigrationRecovery", settings.ID + ".json");

        internal static async Task Save(SettingsV3Model settings, bool onlyIfMissing = false)
        {
            var state = settings.TwitchAuthenticationMigration;
            if (state?.InventorySaved != true) { return; }
            string path = GetPath(settings);
            if (onlyIfMissing && File.Exists(path)) { return; }
            var record = new RecoveryRecord
            {
                ProfileID = settings.ID,
                UserID = state.UserID,
                InventorySaved = state.InventorySaved,
                ExistingNewRewardIDs = state.ExistingNewRewardIDs,
                Rewards = state.Rewards,
                Completed = state.Completed,
            };
            // The recovery record deliberately contains no OAuth tokens or authentication flags.
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await File.WriteAllTextAsync(temporary, JsonConvert.SerializeObject(record));
                File.Move(temporary, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporary)) { File.Delete(temporary); }
            }
        }

        internal static async Task Restore(SettingsV3Model settings, string userID)
        {
            string path = GetPath(settings);
            if (!File.Exists(path)) { return; }
            var record = JsonConvert.DeserializeObject<RecoveryRecord>(await File.ReadAllTextAsync(path));
            if (record == null || record.Version != 1 || record.ProfileID != settings.ID ||
                string.IsNullOrEmpty(record.UserID) || record.UserID != userID ||
                record.Rewards == null || record.ExistingNewRewardIDs == null || !record.InventorySaved ||
                record.Rewards.Any(r => r?.Original == null || r.Original.id == Guid.Empty) ||
                record.Rewards.Select(r => r.Original.id).Distinct().Count() != record.Rewards.Count ||
                (record.Completed && record.Rewards.Any(r => r.NewRewardID == Guid.Empty)))
            {
                throw new InvalidDataException("Invalid Twitch reward recovery record");
            }
            var state = settings.TwitchAuthenticationMigration;
            if (state != null && state.UserID != userID)
            {
                throw new InvalidDataException("Twitch reward recovery account mismatch");
            }
            state ??= new TwitchAuthenticationMigrationModel { UserID = userID };
            state.InventorySaved = record.InventorySaved;
            state.ExistingNewRewardIDs = record.ExistingNewRewardIDs;
            state.Rewards = record.Rewards;
            state.Completed = record.Completed;
            settings.TwitchAuthenticationMigration = state;
        }

        internal static CustomChannelPointRewardModel ReplacementDefinition(TwitchRewardMigrationModel reward)
        {
            var definition = JObject.FromObject(reward.Original).ToObject<CustomChannelPointRewardModel>();
            if (reward.ReplacementPrompt != null) { definition.prompt = reward.ReplacementPrompt; }
            return definition;
        }

        internal static async Task<CustomChannelPointRewardModel> CreateReplacement(
            TwitchRewardMigrationModel reward, Func<JObject, Task<CustomChannelPointRewardModel>> create,
            Func<string, Task<string>> editPrompt, Func<Task> save)
        {
            while (true)
            {
                JObject fields = TwitchAuthenticationMigrationService.RewardFields(ReplacementDefinition(reward));
                fields["is_enabled"] = false;
                try { return await create(fields); }
                catch (HttpRestRequestException ex) when (ex.Response?.StatusCode == HttpStatusCode.BadRequest)
                {
                    JObject error = null;
                    try { error = JObject.Parse(await ex.Response.Content.ReadAsStringAsync()); }
                    catch (JsonException) { }
                    if ((string)error?["message"] != "CREATE_CUSTOM_REWARD_PROMPT_AUTOMOD_FAILED") { throw; }
                    string prompt;
                    do
                    {
                        prompt = await editPrompt(reward.ReplacementPrompt ?? reward.Original.prompt ?? string.Empty);
                        if (prompt == null) { throw new TwitchRewardMigrationPausedException(); }
                    }
                    while (prompt.Length > 200 || prompt == (reward.ReplacementPrompt ?? reward.Original.prompt ?? string.Empty));
                    reward.ReplacementPrompt = prompt;
                    // Persist the correction before trying Twitch again so restart/adoption use the same definition.
                    await save();
                }
            }
        }
    }

    internal sealed class TwitchRewardMigrationPausedException : Exception { }
}

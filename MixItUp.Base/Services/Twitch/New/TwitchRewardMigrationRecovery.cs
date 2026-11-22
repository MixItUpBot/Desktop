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
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public bool VerifyRewardLinks { get; set; }
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
                VerifyRewardLinks = state.VerifyRewardLinks,
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

        internal static async Task<bool> Restore(SettingsV3Model settings, string userID, Func<Task<string>> verifyOwner = null)
        {
            string path = GetPath(settings);
            var record = File.Exists(path) ? await ReadRecord(settings) : null;
            var state = settings.TwitchAuthenticationMigration;
            if (record == null && state == null) { return false; }
            if (string.IsNullOrEmpty(userID)) { throw InvalidRecord("missing saved identity"); }
            bool foreignRecord = record != null && record.UserID != userID;
            bool foreignState = state != null && state.UserID != userID;
            if (foreignRecord || foreignState)
            {
                // An empty, completed migration can belong to a different login in a restored profile.
                // A pending reward or an existing mapping must always stay with its original owner.
                if (verifyOwner == null ||
                    (foreignRecord && !IsCompletedEmptyRecord(record)) ||
                    (foreignState && state is not { Completed: true, InventorySaved: true, Rewards.Count: 0,
                        ExistingNewRewardIDs.Count: 0, NewUserToken: null, NewBotToken: null }) ||
                    (foreignRecord && foreignState && record.UserID != state.UserID))
                {
                    throw InvalidRecord("owner mismatch");
                }
                string otherOwner = foreignRecord ? record.UserID : state.UserID;
                if (string.IsNullOrEmpty(otherOwner) || otherOwner.Any(c => c < '0' || c > '9')) { throw InvalidRecord("invalid record owner"); }
                string archive = Path.ChangeExtension(path, otherOwner + ".completed.json");
                if (!foreignRecord)
                {
                    if (!File.Exists(archive)) { throw InvalidRecord("missing recovery record"); }
                    var archived = await ReadRecord(settings, archive);
                    if (archived.UserID != otherOwner || !IsCompletedEmptyRecord(archived)) { throw InvalidRecord("conflicting recovery archive"); }
                }
                if (await verifyOwner() != userID) { throw new InvalidOperationException(Resources.TwitchMigrationAccountMismatch); }
                if (foreignRecord)
                {
                    // Leave the original in place until Save writes the verified account's inventory.
                    // A restart on either side of that write can safely repeat the recovery.
                    await ArchiveCompletedRecord(settings, path, archive, otherOwner);
                    record = null;
                }
                if (foreignState || state == null) { state = new TwitchAuthenticationMigrationModel { UserID = userID }; }
                state.VerifyRewardLinks = true;
                settings.TwitchAuthenticationMigration = state;
            }
            if (record == null) { return foreignRecord || foreignState; }
            state ??= new TwitchAuthenticationMigrationModel { UserID = userID };
            state.VerifyRewardLinks |= record.VerifyRewardLinks;
            state.InventorySaved = record.InventorySaved;
            state.ExistingNewRewardIDs = record.ExistingNewRewardIDs;
            state.Rewards = record.Rewards;
            state.Completed = record.Completed;
            settings.TwitchAuthenticationMigration = state;
            return foreignRecord || foreignState;
        }

        private static bool IsCompletedEmptyRecord(RecoveryRecord record) =>
            record.Completed && record.Rewards.Count == 0 && record.ExistingNewRewardIDs.Count == 0;

        private static async Task ArchiveCompletedRecord(SettingsV3Model settings, string path, string archive, string owner)
        {
            string temporary = archive + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.Copy(path, temporary);
                var snapshot = await ReadRecord(settings, temporary);
                if (snapshot.UserID != owner || !IsCompletedEmptyRecord(snapshot)) { throw InvalidRecord("recovery record changed"); }
                if (!File.Exists(archive)) { File.Move(temporary, archive); }
                if (!(await File.ReadAllBytesAsync(archive)).SequenceEqual(await File.ReadAllBytesAsync(path)))
                {
                    throw InvalidRecord("conflicting recovery archive");
                }
            }
            finally { if (File.Exists(temporary)) { File.Delete(temporary); } }
        }

        internal static InvalidDataException InvalidRecord(string reason)
        {
            var error = new InvalidDataException("Invalid Twitch reward recovery record");
            error.Data["RecoveryReason"] = reason;
            return error;
        }

        internal static async Task<string> GetRecordOwner(SettingsV3Model settings)
        {
            return File.Exists(GetPath(settings)) ? (await ReadRecord(settings)).UserID : null;
        }

        private static async Task<RecoveryRecord> ReadRecord(SettingsV3Model settings, string path = null)
        {
            string json = await File.ReadAllTextAsync(path ?? GetPath(settings));
            RecoveryRecord record;
            try { record = JsonConvert.DeserializeObject<RecoveryRecord>(json); }
            catch (JsonException) { throw InvalidRecord("invalid reward inventory"); }
            if (record == null || record.Version != 1 || record.ProfileID != settings.ID) { throw InvalidRecord("profile or version mismatch"); }
            if (string.IsNullOrEmpty(record.UserID)) { throw InvalidRecord("missing record owner"); }
            if (!record.InventorySaved || record.Rewards == null || record.ExistingNewRewardIDs == null ||
                record.Rewards.Any(r => r?.Original == null || r.Original.id == Guid.Empty) ||
                record.Rewards.Select(r => r.Original.id).Distinct().Count() != record.Rewards.Count)
            {
                throw InvalidRecord("invalid reward inventory");
            }
            if (record.Completed && record.Rewards.Any(r => r.NewRewardID == Guid.Empty)) { throw InvalidRecord("missing replacement ID"); }
            return record;
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

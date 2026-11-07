using MixItUp.Base.Model.Settings;
using MixItUp.Base.Util;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Base.Services
{
    public sealed class UnsupportedSettingsVersionException : InvalidOperationException
    {
        public UnsupportedSettingsVersionException() : base(Resources.SettingsBackupTooNew) { }
    }

    public static class SettingsUpdateService
    {
        private static readonly SemaphoreSlim migrationLock = new SemaphoreSlim(1, 1);
        private static readonly AsyncLocal<bool> migrationContext = new AsyncLocal<bool>();
        public static bool IsMigrating => migrationContext.Value;

        internal static async Task<T> WithStrictDatabaseErrors<T>(Func<Task<T>> operation)
        {
            bool previous = migrationContext.Value;
            migrationContext.Value = true;
            try { return await operation(); }
            finally { migrationContext.Value = previous; }
        }

        public static async Task<SettingsV3Model> Load(string filePath)
        {
            await migrationLock.WaitAsync();
            try
            {
                int version = await SettingsV3Upgrader.GetSettingsVersion(filePath);
                if (version < 0) { return null; }
                if (version > SettingsV3Model.LatestVersion) { throw new UnsupportedSettingsVersionException(); }
                if (version == SettingsV3Model.LatestVersion)
                {
                    return await FileSerializerHelper.DeserializeFromFile<SettingsV3Model>(filePath, ignoreErrors: true);
                }

                JObject source = JObject.Parse(await File.ReadAllTextAsync(filePath));
                if (!Guid.TryParse((string)source["ID"], out Guid id) || id == Guid.Empty)
                {
                    throw new InvalidDataException("Settings ID is invalid");
                }
                string settingsPath = Path.GetFullPath(Path.Combine(SettingsV3Model.SettingsDirectoryName, id + "." + SettingsV3Model.SettingsFileExtension));
                string databasePath = Path.ChangeExtension(settingsPath, SettingsV3Model.DatabaseFileExtension);
                string backupDirectory = Path.Combine(SettingsV3Model.SettingsDirectoryName, "MigrationBackups", id.ToString(),
                    "v" + version + "-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmss") + "-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(backupDirectory);
                ServiceManager.Get<IDatabaseService>()?.ClearAllPools();
                var backups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string path in new[] { Path.GetFullPath(filePath), settingsPath, settingsPath + ".backup", databasePath,
                    databasePath + "-wal", databasePath + "-shm", databasePath + "-journal" }.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    string backup = null;
                    if (File.Exists(path))
                    {
                        backup = Path.Combine(backupDirectory, backups.Count + "-" + Path.GetFileName(path));
                        File.Copy(path, backup, overwrite: false);
                    }
                    backups.Add(path, backup);
                }

                SettingsV3Model previousSettings = ChannelSession.Settings;
                bool previousOverlayUpgrade = SettingsV3Upgrader.OverlayV3UpgradeOccurred;
                bool previousMigration = migrationContext.Value;
                migrationContext.Value = true;
                try
                {
                    await SettingsV3Upgrader.Version7Upgrade(version, filePath);
                    await SettingsV3Upgrader.Version8Upgrade(version, version < 7 ? settingsPath : filePath);
                    SettingsV3Model result = await FileSerializerHelper.DeserializeFromFile<SettingsV3Model>(settingsPath);
                    if (result == null || result.ID != id || result.Version != SettingsV3Model.LatestVersion)
                    {
                        throw new InvalidDataException("Settings migration did not complete");
                    }
                    return result;
                }
                catch
                {
                    ServiceManager.Get<IDatabaseService>()?.ClearAllPools();
                    foreach (var backup in backups)
                    {
                        if (backup.Value != null) { File.Copy(backup.Value, backup.Key, overwrite: true); }
                        else if (File.Exists(backup.Key)) { File.Delete(backup.Key); }
                    }
                    SettingsV3Upgrader.OverlayV3UpgradeOccurred = previousOverlayUpgrade;
                    throw;
                }
                finally
                {
                    migrationContext.Value = previousMigration;
                    ChannelSession.SetChannelSessionSettings(previousSettings);
                }
            }
            finally { migrationLock.Release(); }
        }
    }
}

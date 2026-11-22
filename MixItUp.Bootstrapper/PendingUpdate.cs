using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace MixItUp.Updates
{
    [DataContract]
    public sealed class PendingUpdate
    {
        public const string CacheName = ".mixitup-updates";
        public const string HelperName = "MixItUp.Bootstrapper.exe";
        public const string StateName = "pending.json";
        public const string EulaReceipt = "eula-accepted";

        [DataMember] public string Version { get; set; }
        [DataMember] public string Channel { get; set; }
        [DataMember] public string Sha256 { get; set; }
        [DataMember] public string EulaVersion { get; set; }

        public string ArchiveName => Channel + "-" + Version + ".zip";

        public static PendingUpdate Read(string cache)
        {
            string path = Path.Combine(cache, StateName);
            if (!File.Exists(path)) { return null; }
            using (FileStream stream = File.OpenRead(path))
            {
                var update = (PendingUpdate)new DataContractJsonSerializer(typeof(PendingUpdate)).ReadObject(stream);
                if (!update.IsValid()) { throw new InvalidDataException("Invalid pending update."); }
                return update;
            }
        }

        public void Write(Stream stream)
        {
            if (!IsValid()) { throw new InvalidDataException("Invalid pending update."); }
            new DataContractJsonSerializer(typeof(PendingUpdate)).WriteObject(stream, this);
        }

        public bool IsValid()
        {
            return (Channel == "internal" || Channel == "public" || Channel == "preview" || Channel == "dev")
                && System.Version.TryParse(Version, out var parsed) && parsed.ToString() == Version
                && Sha256 != null && Sha256.Length == 64 && Array.TrueForAll(Sha256.ToCharArray(), Uri.IsHexDigit);
        }

        public bool LicenseAccepted(string root)
        {
            if (string.IsNullOrEmpty(EulaVersion)) { return true; }
            string path = Path.Combine(root, EulaReceipt);
            if (!File.Exists(path)) { return false; }
            using (var reader = File.OpenText(path))
            {
                return string.Equals(reader.ReadLine()?.Trim(), EulaVersion, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}

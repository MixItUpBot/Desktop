using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace MixItUp.API.V1.Models
{
    [DataContract]
    public class User
    {
        [DataMember]
        public Guid ID { get; set; }

        [DataMember]
        public string TwitchID { get; set; }
        [DataMember]
        public string YouTubeID { get; set; }
        [DataMember]
        public string KickID { get; set; }
        [DataMember]
        public string VeloraID { get; set; }
        [DataMember]
        public string VPZoneID { get; set; }
        public string JoystickID { get; set; }
        public string RazoID { get; set; }
        public string AltStreamID { get; set; }

        [DataMember]
        public string Username { get; set; }

        [DataMember]
        public int? ViewingMinutes { get; set; }

        [DataMember]
        public List<CurrencyAmount> CurrencyAmounts { get; set; } = new List<CurrencyAmount>();

        [DataMember]
        public List<InventoryAmount> InventoryAmounts { get; set; } = new List<InventoryAmount>();
    }
}
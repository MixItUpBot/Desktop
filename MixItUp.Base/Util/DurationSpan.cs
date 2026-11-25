using System;
using System.Runtime.Serialization;

namespace MixItUp.Base.Util
{
    public enum DurationSpanTypeEnum
    {
        Seconds,
        Minutes,
        Hours,
        Days,
        Months,
        Years,
    }

    [DataContract]
    public class DurationSpan
    {
        [DataMember]
        public DurationSpanTypeEnum Type { get; set; }

        [DataMember]
        public int Amount { get; set; }

        public DurationSpan() { }

        public DurationSpan(DurationSpanTypeEnum type, int amount)
        {
            this.Type = type;
            this.Amount = amount;
        }

        public DateTimeOffset GetDateTimeOffsetFromNow() => this.GetDateTimeOffsetFrom(DateTimeOffset.Now);

        public DateTimeOffset GetDateTimeOffsetFrom(DateTimeOffset start)
        {
            switch (this.Type)
            {
                case DurationSpanTypeEnum.Years: return start.AddMonths(this.Amount * 12);
                case DurationSpanTypeEnum.Months: return start.AddMonths(this.Amount);
                case DurationSpanTypeEnum.Days: return start.AddDays(this.Amount);
                case DurationSpanTypeEnum.Hours: return start.AddHours(this.Amount);
                case DurationSpanTypeEnum.Minutes: return start.AddMinutes(this.Amount);
                case DurationSpanTypeEnum.Seconds: return start.AddSeconds(this.Amount);
            }
            return start;
        }
    }
}

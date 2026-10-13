using MixItUp.Base.Util;
using System;
using System.Threading.Tasks;

namespace MixItUp.WPF.Util
{
    public static class BuildExpirationHelper
    {
        private static readonly bool ENABLED = false;  // Set to true to enable build expiration checking

        private static readonly DateTime EXPIRATION_DATE = new DateTime(2026, 10, 12); // (year, month, day) - Set expiration date here

        public static bool IsEnabled()
        {
            return ENABLED;
        }

        public static DateTime GetExpirationDate()
        {
            return EXPIRATION_DATE;
        }

        public static async Task<bool> CheckBuildExpiration()
        {
            if (!ENABLED)
            {
                return false;
            }

            if (DateTime.Now > EXPIRATION_DATE)
            {
                Logger.ForceLog(LogLevel.Warning, $"Build expired on {EXPIRATION_DATE.ToShortDateString()}");
                return true;
            }

            return false;
        }
    }
}

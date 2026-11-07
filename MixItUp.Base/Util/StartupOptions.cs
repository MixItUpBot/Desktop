using System;

namespace MixItUp.Base.Util
{
    public static class StartupOptions
    {
        public static Guid GetAutoLoginProfileID(string[] arguments, Guid defaultProfileID)
        {
            for (int i = 0; i < arguments.Length; i++)
            {
                string argument = arguments[i];
                string value = null;
                if (argument.Equals("--profile", StringComparison.OrdinalIgnoreCase)
                    || argument.Equals("--profile-id", StringComparison.OrdinalIgnoreCase))
                {
                    value = i + 1 < arguments.Length ? arguments[i + 1] : null;
                }
                else if (argument.StartsWith("--profile=", StringComparison.OrdinalIgnoreCase)
                    || argument.StartsWith("--profile-id=", StringComparison.OrdinalIgnoreCase))
                {
                    value = argument.Substring(argument.IndexOf('=') + 1);
                }
                else
                {
                    continue;
                }

                // An invalid explicit profile must never fall back to a different account.
                return Guid.TryParse(value, out Guid profileID) ? profileID : Guid.Empty;
            }
            return defaultProfileID;
        }
    }
}

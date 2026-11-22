using System;
using System.Globalization;
using System.IO;

namespace MixItUp.WPF.Services
{
    internal static class UpdateCheckSchedule
    {
        internal static readonly TimeSpan Interval = TimeSpan.FromHours(1);

        // The caller holds the install's apply.lock while reserving the next poll.
        internal static TimeSpan Reserve(string cache, DateTimeOffset now)
        {
            string path = Path.Combine(cache, "last-check");
            long ticks = now.UtcTicks;
            long previous = 0;
            if (File.Exists(path) && long.TryParse(File.ReadAllText(path), out previous) && previous > 0 && previous <= ticks)
            {
                TimeSpan remaining = Interval - TimeSpan.FromTicks(ticks - previous);
                if (remaining > TimeSpan.Zero) return remaining;
            }

            File.WriteAllText(path + ".tmp", ticks.ToString(CultureInfo.InvariantCulture));
            File.Move(path + ".tmp", path, true);
            return previous > ticks ? Interval : TimeSpan.Zero;
        }
    }
}

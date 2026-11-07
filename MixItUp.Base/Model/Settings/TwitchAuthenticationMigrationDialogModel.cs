using MixItUp.Base.Util;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Base.Model.Settings
{
    public class TwitchAuthenticationMigrationDialogModel
    {
        public string StreamerName { get; set; }
        public string BotName { get; set; }
        public bool HasBot { get; set; }
        public bool StreamerAuthenticated { get; set; }
        public bool BotAuthenticated { get; set; }
        public Func<CancellationToken, Task<Result>> LoginStreamer { get; set; }
        public Func<CancellationToken, Task<Result>> LoginBot { get; set; }
        public bool CanContinue => this.StreamerAuthenticated;
    }
}

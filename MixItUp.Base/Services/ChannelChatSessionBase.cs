using MixItUp.Base.Model;
using MixItUp.Base.Model.Settings;
using MixItUp.Base.Model.Web;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Chat;
using MixItUp.Base.ViewModel.User;
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Base.Services
{
    public abstract class ChannelChatSessionBase : StreamingPlatformSessionBase
    {
        private readonly SemaphoreSlim connectionLock = new SemaphoreSlim(1);
        private CancellationTokenSource cancellation;
        private Task receiver = Task.CompletedTask;
        protected string Token { get; private set; }
        public static event EventHandler AccountChanged;
        protected void NotifyAccountChanged() => AccountChanged?.Invoke(this, EventArgs.Empty);

        public virtual bool RequiresChannelName => true;
        public virtual bool RequiresToken => false;
        protected virtual TimeSpan VerificationTimeout => TimeSpan.FromSeconds(12);
        public override OAuthServiceBase StreamerOAuthService => null;
        public override OAuthServiceBase BotOAuthService => null;
        public override void SaveAuthenticationSettings() { }

        private CancellationTokenSource restore;

        public async Task RestoreChannel(StreamingPlatformAuthenticationSettingsModel saved)
        {
            using var lifetime = new CancellationTokenSource();
            this.restore?.Cancel();
            this.restore = lifetime;
            try
            {
                while (!lifetime.IsCancellationRequested && ReferenceEquals(saved, this.GetAuthenticationSettings()))
                {
                    var result = await this.ConnectChannel(saved.ChannelID, saved.BotOAuthToken?.accessToken, false, lifetime.Token);
                    if (result.Success || !ReferenceEquals(saved, this.GetAuthenticationSettings())) return;
                    await Task.Delay(TimeSpan.FromMinutes(1), lifetime.Token);
                }
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
            finally { if (this.restore == lifetime) this.restore = null; }
        }

        public Task<Result> ConnectChannel(string channel, string token, bool save = true) => this.ConnectChannel(channel, token, save, CancellationToken.None);

        private async Task<Result> ConnectChannel(string channel, string token, bool save, CancellationToken cancel)
        {
            await this.connectionLock.WaitAsync(cancel);
            try
            {
                if (save) this.restore?.Cancel();
                cancel.ThrowIfCancellationRequested();
                channel = channel?.Trim();
                token = token?.Trim();
                if ((this.RequiresChannelName && string.IsNullOrWhiteSpace(channel)) || (this.RequiresToken && string.IsNullOrWhiteSpace(token))) return new Result(Resources.AuthenticationFailedGeneric);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
                timeout.CancelAfter(this.VerificationTimeout);
                try
                {
                    channel = await this.VerifyChannel(channel, token, timeout.Token);
                }
                catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
                {
                    if (this.Token == token) await this.StopReceiver();
                    await this.ClearRejectedAccount(token);
                    return new Result(Resources.AuthenticationFailedGeneric);
                }
                if (string.IsNullOrWhiteSpace(channel)) return new Result(Resources.AuthenticationFailedGeneric);
                await this.StopReceiver();
                this.ChannelID = channel;
                this.Token = token;
                this.IsConnected = true;
                this.IsBotConnected = !string.IsNullOrEmpty(token);
                this.cancellation = new CancellationTokenSource();
                if (save)
                {
                    ChannelSession.Settings.StreamingPlatformAuthentications[this.Platform] = new StreamingPlatformAuthenticationSettingsModel(this.Platform)
                    {
                        ChannelID = channel,
                        BotOAuthToken = !string.IsNullOrWhiteSpace(token) ? new OAuthTokenModel { accessToken = token } : null
                    };
                    await ChannelSession.SaveSettings();
                }
                this.receiver = this.RunReceiver(channel, token, this.cancellation.Token);
                this.NotifyAccountChanged();
                return new Result();
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Warning, $"{this.Platform} channel connection failed ({ex.GetType().Name})");
                return new Result(Resources.AuthenticationFailedGeneric);
            }
            finally { this.connectionLock.Release(); }
        }

        private async Task RunReceiver(string channel, string token, CancellationToken cancel)
        {
            try { await this.Receive(channel, token, cancel); }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
            {
                if (!cancel.IsCancellationRequested)
                {
                    await this.RejectActiveToken(token);
                }
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
            {
                Logger.Log(LogLevel.Warning, $"{this.Platform} denied access to the event feed.");
            }
            finally { this.NotifyAccountChanged(); }
        }

        protected async Task RejectActiveToken(string token)
        {
            if (token == null || this.Token != token) return;
            this.IsConnected = this.IsBotConnected = false;
            this.Token = this.ChannelID = this.BotUsername = null;
            this.cancellation?.Cancel();
            await this.ClearRejectedAccount(token);
            this.NotifyAccountChanged();
        }

        private async Task ClearRejectedAccount(string token)
        {
            var account = this.GetAuthenticationSettings();
            if (account != null && account.BotOAuthToken?.accessToken == token)
            {
                ChannelSession.Settings.StreamingPlatformAuthentications.Remove(this.Platform);
                await ChannelSession.SaveSettings();
                this.NotifyAccountChanged();
            }
        }

        public async Task ForgetChannel()
        {
            this.restore?.Cancel();
            await this.connectionLock.WaitAsync();
            try
            {
                await this.StopReceiver();
                ChannelSession.Settings.StreamingPlatformAuthentications.Remove(this.Platform);
                await ChannelSession.SaveSettings();
                this.NotifyAccountChanged();
            }
            finally { this.connectionLock.Release(); }
        }

        protected abstract Task<string> VerifyChannel(string channel, string token, CancellationToken cancellation);
        protected abstract Task Receive(string channel, string token, CancellationToken cancellation);

        private async Task StopReceiver()
        {
            this.cancellation?.Cancel();
            try { await this.receiver; }
            catch (OperationCanceledException) { }
            finally
            {
                this.cancellation?.Dispose();
                this.cancellation = null;
                this.receiver = Task.CompletedTask;
                this.ChannelID = this.Token = this.BotUsername = null;
                this.IsConnected = this.IsBotConnected = false;
            }
        }

        protected override async Task DisconnectStreamerInternal()
        {
            this.restore?.Cancel();
            await this.connectionLock.WaitAsync();
            try { await this.StopReceiver(); }
            finally { this.connectionLock.Release(); }
        }
        protected override Task<Result> InitializeStreamerInternal() => Task.FromResult(new Result(false));
        protected override Task<Result> InitializeBotInternal() => Task.FromResult(new Result(false));
        protected override Task DisconnectBotInternal() => Task.CompletedTask;
        public override Task RefreshOAuthTokenIfCloseToExpiring() => Task.CompletedTask;
        public override Task<Result> SetStreamTitle(string title) => Task.FromResult(new Result(false));
        public override Task<Result> SetStreamCategory(string category) => Task.FromResult(new Result(false));
        public override Task ClearMessages() => Task.CompletedTask;
        public override Task DeleteMessage(ChatMessageViewModel message) => Task.CompletedTask;
        public override Task TimeoutUser(UserV2ViewModel user, int durationInSeconds, string reason = null) => Task.CompletedTask;
        public override Task ModUser(UserV2ViewModel user) => Task.CompletedTask;
        public override Task UnmodUser(UserV2ViewModel user) => Task.CompletedTask;
        public override Task BanUser(UserV2ViewModel user, string reason = null) => Task.CompletedTask;
        public override Task UnbanUser(UserV2ViewModel user) => Task.CompletedTask;
    }
}

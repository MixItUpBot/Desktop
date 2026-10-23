using MixItUp.Base.Model;
using MixItUp.Base.Model.Actions;
using MixItUp.Base.Model.API;
using MixItUp.Base.Model.API.Files.V2;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.Store;
using MixItUp.Base.Model.Web;
using MixItUp.Base.Model.Webhooks;
using MixItUp.Base.Model.Kick.Webhooks;
using MixItUp.Base.Services.Twitch;
using MixItUp.Base.Services.Twitch.New;
using MixItUp.Base.Services.YouTube;
using MixItUp.Base.Services.YouTube.New;
using MixItUp.Base.Services.Kick.New;
using MixItUp.Base.Services.Velora.New;
using MixItUp.Base.Services.VPZone.New;
using MixItUp.Base.Util;
using MixItUp.Base.Web;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace MixItUp.Base.Services
{
    public interface IMixItUpService : IDisposable
    {
        bool IsWebhookHubConnected { get; }
        bool IsWebhookHubAllowed { get; }
        event EventHandler<bool> OnWebhooksHubAllowed;
        void BackgroundConnect();
        Task<Result> Connect();
        Task Disconnect();

        Task Authenticate(CommunityCommandLoginModel login);

        Task<GetWebhooksResponseModel> GetWebhooks();
        Task<Webhook> CreateWebhook(string service = null);
        Task DeleteWebhook(Guid id);

        event EventHandler<bool> NotificationStatusChanged;
        Task StartNotificationPolling();
        void StopNotificationPolling();
        Task<List<NotificationModel>> GetNotifications();
        bool HasUnreadNotifications { get; }
        void MarkNotificationsAsRead();
        Task<OutageModel> CheckOutageStatus();
        Task<PatreonMemberShoutoutModel> GetRandomPatreonMemberShoutout();
        Task<List<PatreonMemberV2Model>> GetAllPatreonMembersV2();
    }

    public interface IWebhookService
    {
        bool IsWebhookHubConnected { get; }
        bool IsWebhookHubAllowed { get; }
        void BackgroundConnect();
        Task<Result> Connect();
        Task Disconnect();

        Task Authenticate(CommunityCommandLoginModel login);

        Task<GetWebhooksResponseModel> GetWebhooks();
        Task<Webhook> CreateWebhook(string service = null);
        Task DeleteWebhook(Guid id);
    }

    public interface ICommunityCommandsService
    {
        Task<IEnumerable<CommunityCommandCategoryModel>> GetHomeCategories();
        Task<CommunityCommandsSearchResult> SearchCommands(string query, int skip, int top);
        Task<CommunityCommandDetailsModel> GetCommandDetails(Guid id);
        Task<CommunityCommandDetailsModel> AddOrUpdateCommand(CommunityCommandUploadModel command);
        Task DeleteCommand(Guid id);
        Task ReportCommand(CommunityCommandReportModel report);
        Task<CommunityCommandsSearchResult> GetCommandsByUser(Guid userID, int skip, int top);
        Task<CommunityCommandsSearchResult> GetMyCommands(int skip, int top);
        Task<CommunityCommandReviewModel> AddReview(CommunityCommandReviewModel review);
        Task DownloadCommand(Guid id);
    }

    public class CommunityCommandsSearchResult
    {
        public const string PageNumberHeader = "Page-Number";
        public const string PageSizeHeader = "Page-Size";
        public const string TotalElementsHeader = "Total-Elements";
        public const string TotalPagesHeader = "Total-Pages";

        public static async Task<CommunityCommandsSearchResult> Create(HttpResponseMessage response)
        {
            CommunityCommandsSearchResult result = new CommunityCommandsSearchResult();
            result.Results.AddRange(await response.ProcessResponse<IEnumerable<CommunityCommandModel>>());

            if (int.TryParse(response.GetHeaderValue(PageNumberHeader), out int pageNumber))
            {
                result.PageNumber = pageNumber;
            }
            if (int.TryParse(response.GetHeaderValue(PageSizeHeader), out int pageSize))
            {
                result.PageSize = pageSize;
            }
            if (int.TryParse(response.GetHeaderValue(TotalElementsHeader), out int totalElements))
            {
                result.TotalElements = totalElements;
            }
            if (int.TryParse(response.GetHeaderValue(TotalPagesHeader), out int totalPages))
            {
                result.TotalPages = totalPages;
            }
            return result;
        }

        public List<CommunityCommandModel> Results { get; set; } = new List<CommunityCommandModel>();

        public int PageNumber { get; set; }
        public int PageSize { get; set; }

        public int TotalElements { get; set; }
        public int TotalPages { get; set; }

        public CommunityCommandsSearchResult() { }

        public bool HasPreviousResults { get { return this.PageNumber > 1; } }

        public bool HasNextResults { get { return this.PageNumber < this.TotalPages; } }
    }

    public class CommunityCommandsUnavailableException : Exception
    {
        public CommunityCommandsUnavailableException(string message)
            : base(message)
        {
        }
    }

    public class MixItUpService : OAuthRestServiceBase, ICommunityCommandsService, IMixItUpService, IWebhookService, IDisposable
    {
        public const string MixItUpAPIEndpoint = "https://desktop.api.mixitup.bot/api/";
        public const string MixItUpWebhookHubEndpoint = "wss://desktop.api.mixitup.bot/webhookhub";

        public const string DevMixItUpAPIEndpoint = "http://localhost:3000/api/";                // Dev Endpoint
        public const string DevMixItUpWebhookHubEndpoint = "ws://localhost:3000/webhookhub";      // Dev Endpoint


        private const string FileServiceBaseUrl = BuildChannelHelper.API_FILES_UPDATE_ROOT; // "https://files.mixitup.bot/apps/mixitup-desktop/windows-x64";
        private static readonly TimeSpan[] FileServiceRetryDelays = new[]
        {
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(6),
            TimeSpan.FromSeconds(9),
        };

        private string accessToken = null;
        private bool isUpdateRequired = false;
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        private CancellationTokenSource notificationCancellationTokenSource;
        private int? cachedLatestNotificationId = null;
        private List<NotificationModel> cachedNotifications = null;
        private DateTime? lastNotificationFetch = null;
        private readonly TimeSpan notificationCacheExpiry = TimeSpan.FromMinutes(5);
        private readonly object patreonShoutoutFetchLock = new object();
        private Task<PatreonMemberShoutoutModel> patreonShoutoutFetchTask = null;
        private Task<List<PatreonMemberV2Model>> patreonMembersFetchTask = null;

        public event EventHandler<bool> NotificationStatusChanged;
        public bool HasUnreadNotifications { get; private set; }

        public static ClientOptionsModel Options { get; private set; } = new ClientOptionsModel();
        public async Task<(UpdateVersionCheckModel, UpdateVersionManifestModel)?> GetLatestUpdate()
        {
            try
            {
                string channel = BuildChannelHelper.GetUpdateChannel(ChannelSession.AppSettings.UpdateChannel, ChannelSession.AppSettings.PreviewProgram, ChannelSession.AppSettings.TestBuild);
                ChannelSession.AppSettings.TestBuild = false;

                UpdateVersionCheckModel check = await this.FetchVersionCheckAsync(channel);
                if (check == null || check.updatePaused)
                {
                    return null;
                }

                Version currentVersion = VersionHelper.GetCurrentVersion();
                Version latestVersion = check.GetNormalizedLatestVersion();
                Version minimumVersion = check.GetNormalizedMinimumVersion();

                if (currentVersion >= latestVersion && currentVersion >= minimumVersion)
                {
                    return null;
                }

                string targetVersion = currentVersion < minimumVersion ? check.minimumVersion : check.latestVersion;
                UpdateVersionManifestModel manifest = await this.FetchVersionManifestAsync(channel, targetVersion);
                if (manifest == null)
                {
                    return null;
                }

                return (check, manifest);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            return null;
        }

        private async Task<UpdateVersionCheckModel> FetchVersionCheckAsync(string channel)
        {
            string url = $"{FileServiceBaseUrl}/{channel}/latest";
            Exception lastError = null;

            for (int attempt = 0; attempt <= FileServiceRetryDelays.Length; attempt++)
            {
                try
                {
                    using (AdvancedHttpClient client = new AdvancedHttpClient())
                    {
                        client.Timeout = TimeSpan.FromSeconds(5);
                        UpdateVersionCheckModel check = await client.GetAsync<UpdateVersionCheckModel>(url);
                        if (check != null)
                        {
                            return check;
                        }
                    }
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    Logger.Log(LogLevel.Warning, $"Attempt {attempt + 1} to fetch version check from {url} failed: {ex.Message}");
                }

                if (attempt < FileServiceRetryDelays.Length)
                {
                    try
                    {
                        await Task.Delay(FileServiceRetryDelays[attempt], CancellationToken.None).ConfigureAwait(false);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                }
            }

            if (lastError != null)
            {
                Logger.Log(lastError);
            }

            Logger.Log(LogLevel.Warning, $"Unable to retrieve version check from {url} after retries.");
            return null;
        }

        private async Task<UpdateVersionManifestModel> FetchVersionManifestAsync(string channel, string version)
        {
            string url = $"{FileServiceBaseUrl}/{channel}/{version}";
            Exception lastError = null;

            for (int attempt = 0; attempt <= FileServiceRetryDelays.Length; attempt++)
            {
                try
                {
                    using (AdvancedHttpClient client = new AdvancedHttpClient())
                    {
                        client.Timeout = TimeSpan.FromSeconds(5);
                        UpdateVersionManifestModel manifest = await client.GetAsync<UpdateVersionManifestModel>(url);
                        if (manifest != null)
                        {
                            return manifest;
                        }
                    }
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    Logger.Log(LogLevel.Warning, $"Attempt {attempt + 1} to fetch version manifest from {url} failed: {ex.Message}");
                }

                if (attempt < FileServiceRetryDelays.Length)
                {
                    try
                    {
                        await Task.Delay(FileServiceRetryDelays[attempt], CancellationToken.None).ConfigureAwait(false);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                }
            }

            if (lastError != null)
            {
                Logger.Log(lastError);
            }

            Logger.Log(LogLevel.Warning, $"Unable to retrieve version manifest from {url} after retries.");
            return null;
        }

        public async Task SendIssueReport(IssueReportModel report)
        {
            string content = JSONSerializerHelper.SerializeToString(report);
            var response = await this.PostAsync("issuereport", new StringContent(content, Encoding.UTF8, "application/json"));
            if (!response.IsSuccessStatusCode)
            {
                string resultContent = await response.Content.ReadAsStringAsync();
                Logger.Log(resultContent);
            }
        }

        // ICommunityCommandsService
        public async Task<IEnumerable<CommunityCommandCategoryModel>> GetHomeCategories()
        {
            return await this.CommunityCommandsRequest(async () =>
            {
                await EnsureLogin();
                return await GetAsync<IEnumerable<CommunityCommandCategoryModel>>("v2/community/commands/categories");
            });
        }

        public async Task<CommunityCommandsSearchResult> SearchCommands(string query, int skip, int top)
        {
            return await this.CommunityCommandsRequest(async () =>
            {
                await EnsureLogin();
                return await CommunityCommandsSearchResult.Create(await this.GetAsync($"v2/community/commands/command/search?query={HttpUtility.UrlEncode(query)}&skip={skip}&top={top}"));
            });
        }

        public async Task<CommunityCommandDetailsModel> GetCommandDetails(Guid id)
        {
            return await this.CommunityCommandsRequest(async () =>
            {
                try
                {
                    await EnsureLogin();
                    return await GetAsync<CommunityCommandDetailsModel>($"v2/community/commands/command/{id}");
                }
                catch (HttpRestRequestException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return null;
                }
            });
        }

        public async Task<CommunityCommandDetailsModel> AddOrUpdateCommand(CommunityCommandUploadModel command)
        {
            return await this.CommunityCommandsRequest(async () =>
            {
                return await this.AuthorizedDesktopApiRequest(async () =>
                {
                    await EnsureLogin();
                    return await PostAsync<CommunityCommandDetailsModel>("v2/community/commands/command", AdvancedHttpClient.CreateContentFromObject(command));
                });
            });
        }

        public async Task DeleteCommand(Guid id)
        {
            await this.CommunityCommandsRequest(async () =>
            {
                await this.AuthorizedDesktopApiRequest(async () =>
                {
                    await EnsureLogin();
                    await DeleteAsync<CommunityCommandDetailsModel>($"v2/community/commands/command/{id}/delete");
                });
            });
        }

        public async Task ReportCommand(CommunityCommandReportModel report)
        {
            await this.CommunityCommandsRequest(async () =>
            {
                await this.AuthorizedDesktopApiRequest(async () =>
                {
                    await EnsureLogin();
                    await PostAsync($"v2/community/commands/command/{report.CommandID}/report", AdvancedHttpClient.CreateContentFromObject(report));
                });
            });
        }

        public async Task<CommunityCommandsSearchResult> GetCommandsByUser(Guid userID, int skip, int top)
        {
            return await this.CommunityCommandsRequest(async () =>
            {
                await EnsureLogin();
                return await CommunityCommandsSearchResult.Create(await GetAsync($"v2/community/commands/command/user/{userID}?skip={skip}&top={top}"));
            });
        }

        public async Task<CommunityCommandsSearchResult> GetMyCommands(int skip, int top)
        {
            return await this.CommunityCommandsRequest(async () =>
            {
                return await this.AuthorizedDesktopApiRequest(async () =>
                {
                    await EnsureLogin();
                    return await CommunityCommandsSearchResult.Create(await GetAsync($"v2/community/commands/command/mine?skip={skip}&top={top}"));
                });
            });
        }

        public async Task<CommunityCommandReviewModel> AddReview(CommunityCommandReviewModel review)
        {
            return await this.CommunityCommandsRequest(async () =>
            {
                return await this.AuthorizedDesktopApiRequest(async () =>
                {
                    await EnsureLogin();
                    return await PostAsync<CommunityCommandReviewModel>($"v2/community/commands/command/{review.CommandID}/review", AdvancedHttpClient.CreateContentFromObject(review));
                });
            });
        }

        public async Task DownloadCommand(Guid id)
        {
            try
            {
                await this.CommunityCommandsRequest(async () =>
                {
                    await EnsureLogin();
                    await GetAsync<IEnumerable<CommunityCommandDetailsModel>>($"v2/community/commands/command/{id}/download");
                });
            }
            catch (CommunityCommandsUnavailableException) { throw; }
            catch { }
        }

        private async Task<T> CommunityCommandsRequest<T>(Func<Task<T>> action)
        {
            try
            {
                return await action();
            }
            catch (HttpRestRequestException ex) when (ex.Response?.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                throw new CommunityCommandsUnavailableException(await this.GetCommunityCommandsUnavailableMessage(ex));
            }
        }

        private async Task CommunityCommandsRequest(Func<Task> action)
        {
            await this.CommunityCommandsRequest(async () =>
            {
                await action();
                return true;
            });
        }

        private async Task<T> AuthorizedDesktopApiRequest<T>(Func<Task<T>> action, bool hasRetried = false)
        {
            try
            {
                return await action();
            }
            catch (HttpRestRequestException ex) when (!hasRetried && ex.Response?.StatusCode == HttpStatusCode.Unauthorized)
            {
                this.accessToken = null;
                await this.EnsureLogin();
                return await this.AuthorizedDesktopApiRequest(action, hasRetried: true);
            }
        }

        private async Task AuthorizedDesktopApiRequest(Func<Task> action, bool hasRetried = false)
        {
            try
            {
                await action();
            }
            catch (HttpRestRequestException ex) when (!hasRetried && ex.Response?.StatusCode == HttpStatusCode.Unauthorized)
            {
                this.accessToken = null;
                await this.EnsureLogin();
                await this.AuthorizedDesktopApiRequest(action, hasRetried: true);
            }
        }

        private async Task<string> GetCommunityCommandsUnavailableMessage(HttpRestRequestException ex)
        {
            const string fallback = "Community Commands is temporarily unavailable.";
            try
            {
                string content = await ex.Response.Content.ReadAsStringAsync();
                string message = JObject.Parse(content)?["message"]?.ToString();
                return string.IsNullOrWhiteSpace(message) ? fallback : message;
            }
            catch
            {
                return fallback;
            }
        }

        protected override Task<OAuthTokenModel> GetOAuthToken(bool autoRefreshToken = true)
        {
            return Task.FromResult(new OAuthTokenModel { accessToken = this.accessToken });
        }

        protected override string GetBaseAddress()
        {
            //if (ChannelSession.IsDebug())
            //{
            //    return MixItUpService.DevMixItUpAPIEndpoint;
            //}
            return MixItUpService.MixItUpAPIEndpoint;
        }

        protected string GetWebhookHubAddress()
        {
            //if (ChannelSession.IsDebug())
            //{
            //    return MixItUpService.DevMixItUpWebhookHubEndpoint;
            //}
            return MixItUpService.MixItUpWebhookHubEndpoint;
        }

        private async Task EnsureLogin()
        {
            if (accessToken == null)
            {
                try
                {
                    var token = this.GetLoginToken();
                    var loginResponse = await PostAsync<CommunityCommandLoginResponseModel>("v2/user/login", AdvancedHttpClient.CreateContentFromObject(token));
                    this.accessToken = loginResponse.AccessToken;
                }
                catch (HttpRestRequestException ex) when (ex.Response?.StatusCode == HttpStatusCode.UpgradeRequired)
                {
                    isUpdateRequired = true;
                    Logger.Log(LogLevel.Error, "A Desktop update is required to use Mix It Up API services.");
                    ChannelSession.DisconnectionOccurred(MixItUp.Base.Resources.MixItUpServices);
                    throw;
                }
            }
        }

        // IWebhookService
        public const string AuthenticateMethodName = "AuthenticateMany";
        private static readonly ConcurrentDictionary<string, Func<string, Task>> webhookServiceHandlers = new ConcurrentDictionary<string, Func<string, Task>>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<Guid, string> webhookServiceById = new ConcurrentDictionary<Guid, string>();
        private WebhookHubConnection webhookHubConnection = null;
        private TaskCompletionSource<bool> webhookAuthenticationCompletionSource = null;
        private readonly SemaphoreSlim webhookConnectLock = new SemaphoreSlim(1, 1);
        private CancellationTokenSource webhookReconnectCancellationTokenSource = null;
        private int webhookReconnectInProgress = 0;
        private const int WebhookReconnectBaseDelayMs = 5000;
        private const int WebhookReconnectMaxDelayMs = 30000;
        public bool IsWebhookHubConnected { get { return this.webhookHubConnection?.IsConnected() ?? false; } }
        public bool IsWebhookHubAllowed { get; private set; } = false;
        public event EventHandler<bool> OnWebhooksHubAllowed = delegate { };

        public void BackgroundConnect()
        {
            AsyncRunner.RunAsyncBackground(async (cancellationToken) =>
            {
                Result result = await this.Connect();
                if (!result.Success && !isUpdateRequired)
                {
                    WebhookHubConnection_Disconnected(this, new Exception());
                }
            }, new CancellationToken());
        }

        public async Task<Result> Connect()
        {
            if (isUpdateRequired) return new Result("Update Required");

            await this.webhookConnectLock.WaitAsync();
            try
            {
                if (!this.IsWebhookHubConnected)
                {
                    if (this.webhookHubConnection == null)
                    {
                        this.webhookHubConnection = new WebhookHubConnection(this.GetWebhookHubAddress());

                        this.webhookHubConnection.Listen("TriggerWebhook", (Guid id, string payload) =>
                        {
                            Logger.Log($"Webhook Event - Generic Webhook - {id} - {payload}");
                            var _ = this.TriggerGenericWebhook(id, payload);
                        });

                        this.webhookHubConnection.Listen("AuthenticationCompleteEvent", (bool approved) =>
                        {
                            Logger.Log($"Webhook Authentication - {approved}");

                            this.IsWebhookHubAllowed = approved;
                            this.webhookAuthenticationCompletionSource?.TrySetResult(approved);
                            this.OnWebhooksHubAllowed(this, approved);
                            if (!this.IsWebhookHubAllowed)
                            {
                                Logger.Log(LogLevel.Error, $"Webhook Authentication Failed");

                                // Force disconnect so it doesn't retry
                                var _ = this.Disconnect();
                            }
                        });

                        this.webhookHubConnection.Listen<string, JObject, JObject>("KickWebhookEvent", (eventType, payload, metadataObject) =>
                        {
                            try
                            {
                                Logger.Log(LogLevel.Debug, $"Kick Webhook Event Received - EventType: {eventType} - Metadata: {metadataObject?.ToString(Newtonsoft.Json.Formatting.None)} - Payload: {payload?.ToString(Newtonsoft.Json.Formatting.None)}");

                                WebhookEventModel metadata = metadataObject?.ToObject<WebhookEventModel>();
                                var _ = ServiceManager.Get<KickSession>().Client.HandleWebhookEvent(eventType, payload, metadata);
                            }
                            catch (Exception ex)
                            {
                                Logger.Log(ex);
                            }
                        });

                        // Velora inbound events are handled client-direct over the Chat + Events WebSockets
                        // (VeloraChatSocketClient / VeloraEventSocketClient), so there is no "VeloraWebhookEvent"
                        // relay listener here. The DesktopAPI relay route still exists but is no longer consumed.

                        // VPZone's chat gateway carries the whole event catalog client-direct, so this
                        // listener stays dormant. The only event the relay would add is a subscription
                        // cancellation, which Mix It Up does not surface. The DesktopAPI relay route and
                        // the registration client on VPZoneSession are both still in place for whenever
                        // VPZone's event catalog grows past what the gateway carries.
                        //this.webhookHubConnection.Listen<string, JObject, JObject>("VPZoneWebhookEvent", (eventType, payload, metadataObject) =>
                        //{
                        //    try
                        //    {
                        //        Logger.Log(LogLevel.Debug, $"VPZone Webhook Event Received - EventType: {eventType} - Payload: {payload?.ToString(Newtonsoft.Json.Formatting.None)}");
                        //
                        //        var _ = ServiceManager.Get<VPZoneSession>().Client.HandleWebhookEvent(eventType, payload);
                        //    }
                        //    catch (Exception ex)
                        //    {
                        //        Logger.Log(ex);
                        //    }
                        //});
                    }

                    this.webhookHubConnection.Connected -= WebhookHubConnection_Connected;
                    this.webhookHubConnection.Disconnected -= WebhookHubConnection_Disconnected;

                    this.webhookHubConnection.Connected += WebhookHubConnection_Connected;
                    this.webhookHubConnection.Disconnected += WebhookHubConnection_Disconnected;

                    if (!await this.webhookHubConnection.Connect())
                    {
                        return new Result(MixItUp.Base.Resources.WebhooksServiceFailedConnection);
                    }
                }
                else
                {
                    return new Result(MixItUp.Base.Resources.WebhookServiceAlreadyConnected);
                }
            }
            finally
            {
                this.webhookConnectLock.Release();
            }

            this.IsWebhookHubAllowed = false;
            this.webhookAuthenticationCompletionSource = new TaskCompletionSource<bool>();
            TaskCompletionSource<bool> tcs = this.webhookAuthenticationCompletionSource;

            Task completedTask = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(15)));
            bool authenticated = completedTask == tcs.Task && tcs.Task.Result;
            this.webhookAuthenticationCompletionSource = null;

            if (!authenticated)
            {
                await this.Disconnect();
            }

            return new Result(authenticated);
        }

        public async Task Disconnect()
        {
            webhookReconnectCancellationTokenSource?.Cancel();
            webhookReconnectCancellationTokenSource = null;

            await DisconnectHubConnection();
        }

        private async Task DisconnectHubConnection()
        {
            if (this.webhookHubConnection != null)
            {
                this.webhookHubConnection.Connected -= WebhookHubConnection_Connected;
                this.webhookHubConnection.Disconnected -= WebhookHubConnection_Disconnected;

                await this.webhookHubConnection.Disconnect();

                this.webhookHubConnection = null;
            }

            this.IsWebhookHubAllowed = false;
            this.webhookAuthenticationCompletionSource?.TrySetResult(false);
            this.webhookAuthenticationCompletionSource = null;
        }

        private async void WebhookHubConnection_Connected(object sender, EventArgs e)
        {
            ChannelSession.ReconnectionOccurred(MixItUp.Base.Resources.MixItUpServices);

            await this.Authenticate(this.GetLoginToken());
        }

        private async void WebhookHubConnection_Disconnected(object sender, Exception e)
        {
            if (e?.Message?.Contains("4426") == true)
            {
                isUpdateRequired = true;
                Logger.Log(LogLevel.Error, "A Desktop update is required to use Mix It Up WebhookHub services.");
                return;
            }

            if (Interlocked.CompareExchange(ref webhookReconnectInProgress, 1, 0) != 0)
            {
                return;
            }

            ChannelSession.DisconnectionOccurred(MixItUp.Base.Resources.MixItUpServices);

            var reconnectCts = new CancellationTokenSource();
            webhookReconnectCancellationTokenSource = reconnectCts;
            CancellationToken reconnectToken = reconnectCts.Token;
            try
            {
                Result result;
                int attempt = 0;
                do
                {
                    await DisconnectHubConnection();

                    int baseDelay = (int)Math.Min((long)WebhookReconnectBaseDelayMs << attempt, WebhookReconnectMaxDelayMs);
                    int jitter = RandomHelper.GenerateRandomNumber(-(baseDelay / 2), baseDelay / 2);
                    await Task.Delay(Math.Max(baseDelay + jitter, 0), reconnectToken);

                    result = await this.Connect();
                    attempt++;
                }
                while (!result.Success && !isUpdateRequired && !reconnectToken.IsCancellationRequested && attempt < 100);

                if (!result.Success && attempt >= 100)
                {
                    Logger.Log(LogLevel.Error, "Webhook reconnection paused after 100 attempts.");
                }

                if (result.Success)
                {
                    ChannelSession.ReconnectionOccurred(MixItUp.Base.Resources.MixItUpServices);
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                Interlocked.Exchange(ref webhookReconnectInProgress, 0);
            }
        }

        public async Task Authenticate(CommunityCommandLoginModel login)
        {
            Logger.Log($"Webhook - Sending Auth - {JSONSerializerHelper.SerializeToString(login)}");

            try
            {
                if (this.webhookHubConnection == null) { return; }
                await this.AsyncWrapper(this.webhookHubConnection.Send(AuthenticateMethodName, login));
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        public static void RegisterWebhookServiceHandler(string service, Func<string, Task> handler)
        {
            if (!string.IsNullOrEmpty(service) && handler != null)
            {
                webhookServiceHandlers[service] = handler;
            }
        }

        public async Task<GetWebhooksResponseModel> GetWebhooks()
        {
            GetWebhooksResponseModel response = await this.AuthorizedDesktopApiRequest(async () =>
            {
                await EnsureLogin();
                return await GetAsync<GetWebhooksResponseModel>($"webhook");
            });

            if (response?.Webhooks != null)
            {
                this.webhookServiceById.Clear();
                foreach (Webhook webhook in response.Webhooks)
                {
                    this.webhookServiceById[webhook.Id] = webhook.Service;
                }
            }

            return response;
        }

        public async Task<Webhook> CreateWebhook(string service = null)
        {
            Webhook webhook = await this.AuthorizedDesktopApiRequest(async () =>
            {
                await EnsureLogin();
                object content = WebhookServices.IsGeneral(service) ? new { } : (object)new { service = service.ToLowerInvariant() };
                return await PostAsync<Webhook>($"webhook", AdvancedHttpClient.CreateContentFromObject(content));
            });

            if (webhook != null)
            {
                this.webhookServiceById[webhook.Id] = webhook.Service;
            }

            return webhook;
        }

        public async Task DeleteWebhook(Guid id)
        {
            await this.AuthorizedDesktopApiRequest(async () =>
            {
                await EnsureLogin();
                await DeleteAsync($"webhook/{id}");
            });
        }

        private async Task AsyncWrapper(Task task)
        {
            try
            {
                await task;
            }
            catch (Exception ex) { Logger.Log(ex); }
        }

        private async Task TriggerGenericWebhook(Guid id, string payload)
        {
            try
            {
                if (this.webhookServiceById.TryGetValue(id, out string service) && !WebhookServices.IsGeneral(service))
                {
                    if (webhookServiceHandlers.TryGetValue(service, out Func<string, Task> handler))
                    {
                        await handler(payload);
                    }
                    else
                    {
                        Logger.Log($"Webhook Event - No handler registered for webhook service - {service} - {id}");
                    }
                    return;
                }

                var command = ServiceManager.Get<CommandService>().WebhookCommands.FirstOrDefault(c => c.ID == id);
                if (command != null && command.IsEnabled)
                {
                    if (string.IsNullOrEmpty(payload))
                    {
                        payload = "{}";
                    }

                    Dictionary<string, string> eventCommandSpecialIdentifiers = new Dictionary<string, string>();
                    eventCommandSpecialIdentifiers["webhookpayload"] = payload;

                    // Do JSON => Special Identifier logic
                    CommandParametersModel parameters = new CommandParametersModel(ChannelSession.User, StreamingPlatformTypeEnum.All, eventCommandSpecialIdentifiers);
                    Dictionary<string, string> jsonParameters = command.JSONParameters
                        .Where(param => !string.IsNullOrWhiteSpace(param.JSONParameterName) && !string.IsNullOrWhiteSpace(param.SpecialIdentifierName))
                        .GroupBy(param => param.JSONParameterName)
                        .ToDictionary(g => g.First().JSONParameterName, g => g.First().SpecialIdentifierName);
                    await WebRequestActionModel.ProcessJSONToSpecialIdentifiers(payload, jsonParameters, parameters);

                    await ServiceManager.Get<CommandService>().Queue(command, parameters);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        private CommunityCommandLoginModel GetLoginToken()
        {
            var login = new CommunityCommandLoginModel();
            login.Version = VersionHelper.GetFullVersionString();

            if (ServiceManager.Get<TwitchSession>().IsConnected)
            {
                login.TwitchAccessToken = ServiceManager.Get<TwitchSession>()?.StreamerService?.GetOAuthTokenCopy()?.accessToken;
            }
            if (ServiceManager.Get<YouTubeSession>().IsConnected)
            {
                OAuthTokenModel token = ServiceManager.Get<YouTubeSession>()?.StreamerService?.GetOAuthTokenCopy();

                login.YouTubeOAuthToken = new StreamingClient.Base.Model.OAuth.OAuthTokenModel()
                {
                    clientID = ServiceManager.Get<YouTubeSession>().StreamerOAuthService.ClientID,
                    clientSecret = ServiceManager.Get<YouTubeSession>().StreamerOAuthService.ClientSecret,

                    accessToken = token.accessToken,
                    refreshToken = token.refreshToken,

                    expiresIn = token.expiresIn,
                };
            }
            if (ServiceManager.Get<KickSession>().IsConnected)
            {
                login.KickAccessToken = ServiceManager.Get<KickSession>()?.StreamerService?.GetOAuthTokenCopy()?.accessToken;
            }
            if (ServiceManager.Get<VeloraSession>().IsConnected)
            {
                login.VeloraAccessToken = ServiceManager.Get<VeloraSession>()?.StreamerService?.GetOAuthTokenCopy()?.accessToken;
            }
            if (ServiceManager.Get<VPZoneSession>().IsConnected)
            {
                login.VPZoneAccessToken = ServiceManager.Get<VPZoneSession>()?.StreamerService?.GetOAuthTokenCopy()?.accessToken;
            }
            return login;
        }

        // Notifications UtilService
        public async Task StartNotificationPolling()
        {
            if (notificationCancellationTokenSource != null)
            {
                return;
            }

            notificationCancellationTokenSource = new CancellationTokenSource();

            await CheckForNewNotifications();

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            var notificationPollingTokenSource = notificationCancellationTokenSource;
            if (notificationPollingTokenSource != null)
            {
                AsyncRunner.RunAsyncBackground(this.NotificationPollingBackground, notificationPollingTokenSource.Token, 60 * 60000);
            }
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }

        public void StopNotificationPolling()
        {
            notificationCancellationTokenSource?.Cancel();
            notificationCancellationTokenSource?.Dispose();
            notificationCancellationTokenSource = null;
        }

        private async Task NotificationPollingBackground(CancellationToken cancellationToken)
        {
            await CheckForNewNotifications();
        }

        private async Task CheckForNewNotifications()
        {
            try
            {
                using (AdvancedHttpClient client = new AdvancedHttpClient(MixItUpAPIEndpoint))
                {
                    HttpResponseMessage response = await client.GetAsync("services/notifications/id");
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        string json = await response.Content.ReadAsStringAsync();
                        JObject data = JObject.Parse(json);
                        int latestId = data["latestId"]?.Value<int>() ?? 0;

                        int lastReadId = ChannelSession.AppSettings.LastReadNotificationId;
                        bool hasUnread = latestId > lastReadId;

                        if (cachedLatestNotificationId.HasValue && latestId > cachedLatestNotificationId.Value)
                        {
                            Logger.Log(LogLevel.Debug, $"New notification detected (ID: {latestId})");
                            cachedNotifications = null;
                            lastNotificationFetch = null;
                        }

                        cachedLatestNotificationId = latestId;

                        if (HasUnreadNotifications != hasUnread)
                        {
                            HasUnreadNotifications = hasUnread;
                            NotificationStatusChanged?.Invoke(this, hasUnread);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Warning, $"Failed to check for new notifications: {ex.Message}");
            }
        }

        public async Task<List<NotificationModel>> GetNotifications()
        {
            if (cachedNotifications != null &&
                lastNotificationFetch.HasValue &&
                DateTime.UtcNow - lastNotificationFetch.Value < notificationCacheExpiry)
            {
                return cachedNotifications;
            }

            try
            {
                using (AdvancedHttpClient client = new AdvancedHttpClient(MixItUpAPIEndpoint))
                {
                    HttpResponseMessage response = await client.GetAsync("services/notifications");
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        string json = await response.Content.ReadAsStringAsync();
                        JObject data = JObject.Parse(json);
                        JArray notificationsArray = (JArray)data["notifications"];

                        var notifications = new List<NotificationModel>();
                        foreach (JObject notif in notificationsArray)
                        {
                            notifications.Add(new NotificationModel
                            {
                                Id = notif["id"]?.Value<int>() ?? 0,
                                Title = notif["title"]?.ToString(),
                                Message = notif["message"]?.ToString(),
                                Timestamp = DateTime.Parse(notif["timestamp"].ToString()),
                                Icon = notif["icon"]?.ToString() ?? "Bell",
                                IconColor = notif["iconColor"]?.ToString() ?? "#808080",
                                Url = notif["url"]?.ToString(),
                                IsPinned = notif["isPinned"]?.Value<bool>() ?? false
                            });
                        }

                        cachedNotifications = notifications;
                        lastNotificationFetch = DateTime.UtcNow;

                        return notifications;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Warning, $"Failed to fetch notifications: {ex.Message}");
            }

            return null;
        }

        public void MarkNotificationsAsRead()
        {
            if (cachedLatestNotificationId.HasValue)
            {
                ChannelSession.AppSettings.LastReadNotificationId = cachedLatestNotificationId.Value;
                _ = ChannelSession.AppSettings.Save();

                HasUnreadNotifications = false;
                NotificationStatusChanged?.Invoke(this, false);
            }
        }

        public async Task<OutageModel> CheckOutageStatus()
        {
            try
            {
                using (AdvancedHttpClient client = new AdvancedHttpClient(MixItUpAPIEndpoint))
                {
                    HttpResponseMessage response = await client.GetAsync("services/notifications/outage");
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        string json = await response.Content.ReadAsStringAsync();
                        JObject data = JObject.Parse(json);

                        return new OutageModel
                        {
                            Enabled = data["enabled"]?.Value<bool>() ?? false,
                            Message = data["message"]?.ToString() ?? "",
                            Severity = data["severity"]?.ToString() ?? "warning"
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Warning, $"Failed to check outage status: {ex.Message}");
            }

            return new OutageModel { Enabled = false, Message = "", Severity = "warning" };
        }

        public Task<PatreonMemberShoutoutModel> GetRandomPatreonMemberShoutout()
        {
            lock (this.patreonShoutoutFetchLock)
            {
                if (this.patreonShoutoutFetchTask == null)
                {
                    this.patreonShoutoutFetchTask = this.FetchRandomPatreonMemberShoutout();
                }
                return this.patreonShoutoutFetchTask;
            }
        }

        private async Task<PatreonMemberShoutoutModel> FetchRandomPatreonMemberShoutout()
        {
            try
            {
                using (AdvancedHttpClient client = new AdvancedHttpClient(MixItUpAPIEndpoint))
                {
                    client.Timeout = TimeSpan.FromSeconds(5);

                    HttpResponseMessage response = await client.GetAsync("services/patreon/members/random/v2");
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        string json = await response.Content.ReadAsStringAsync();
                        JObject data = JObject.Parse(json);
                        if (data["success"]?.Value<bool>() == true)
                        {
                            JObject member = data["member"] as JObject;
                            string displayName = member?["display_name"]?.ToString()?.Trim();
                            if (!string.IsNullOrWhiteSpace(displayName))
                            {
                                return new PatreonMemberShoutoutModel()
                                {
                                    DisplayName = displayName,
                                    AvatarUrl = member?["avatar_url"]?.ToString(),
                                    SocialMediaLink = member?["social_media_link"]?.ToString(),
                                    Platform = member?["platform"]?.ToString(),
                                    PlatformUsername = member?["platform_username"]?.ToString(),
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            return null;
        }

        public Task<List<PatreonMemberV2Model>> GetAllPatreonMembersV2()
        {
            if (this.patreonMembersFetchTask == null)
            {
                this.patreonMembersFetchTask = this.FetchAllPatreonMembersV2();
            }
            return this.patreonMembersFetchTask;
        }

        private async Task<List<PatreonMemberV2Model>> FetchAllPatreonMembersV2()
        {
            try
            {
                using (AdvancedHttpClient client = new AdvancedHttpClient(MixItUpAPIEndpoint))
                {
                    client.Timeout = TimeSpan.FromSeconds(10);

                    HttpResponseMessage response = await client.GetAsync("services/patreon/members/all/v2");
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        string json = await response.Content.ReadAsStringAsync();
                        JObject data = JObject.Parse(json);
                        if (data["success"]?.Value<bool>() == true)
                        {
                            JArray membersArray = data["members"] as JArray;
                            if (membersArray != null)
                            {
                                return membersArray
                                    .OfType<JObject>()
                                    .Select(m => new PatreonMemberV2Model
                                    {
                                        DisplayName = m["display_name"]?.ToString()?.Trim(),
                                        SocialMediaLink = m["social_media_link"]?.ToString(),
                                        Platform = m["platform"]?.ToString(),
                                        PlatformUsername = m["platform_username"]?.ToString()?.TrimStart('@'),
                                    })
                                    .Where(m => !string.IsNullOrWhiteSpace(m.DisplayName))
                                    .OrderBy(m => m.DisplayName, StringComparer.OrdinalIgnoreCase)
                                    .ToList();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            return new List<PatreonMemberV2Model>();
        }

        /// <summary>
        /// The Desktop API address VPZone posts its webhook deliveries to. The relay verifies the
        /// signature and forwards the delivery down the webhook hub, so nothing has to be exposed on
        /// the streamer's own machine.
        /// </summary>
        public string GetVPZoneWebhookCallbackURL(string channelSlug)
        {
            if (string.IsNullOrWhiteSpace(channelSlug))
            {
                return null;
            }
            return $"{MixItUpAPIEndpoint}vpzone/webhooks/callback/{AdvancedHttpClient.URLEncodeString(channelSlug)}";
        }

        /// <summary>
        /// Hands the relay the signing secret VPZone issued for this registration. VPZone generates
        /// the secret itself and returns it only once, at creation, so the relay cannot obtain it any
        /// other way and cannot verify a delivery without it.
        /// </summary>
        public async Task<bool> RegisterVPZoneWebhookSecret(string channelSlug, string secret)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(channelSlug) || string.IsNullOrWhiteSpace(secret))
                {
                    return false;
                }

                await EnsureLogin();

                JObject body = new JObject();
                body["channelSlug"] = channelSlug;
                body["secret"] = secret;

                HttpResponseMessage response = await this.PostAsync("v2/vpzone/webhooks/secret", AdvancedHttpClient.CreateContentFromObject(body));
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            return false;
        }

        public async Task RecordClientSession()
        {
            try
            {
                await EnsureLogin();

                JObject body = new JObject();
                body["telemetryId"] = ChannelSession.Settings.TelemetryUserID;
                body["hasTwitch"] = ServiceManager.Get<TwitchSession>().IsConnected;
                body["hasYouTube"] = ServiceManager.Get<YouTubeSession>().IsConnected;
                body["hasKick"] = ServiceManager.Get<KickSession>().IsConnected;
                body["hasVelora"] = ServiceManager.Get<VeloraSession>().IsConnected;
                body["hasVPZone"] = ServiceManager.Get<VPZoneSession>().IsConnected;
                body["version"] = VersionHelper.GetFullVersionString();
                body["release"] = BuildChannelHelper.GetReleaseChannel();

                HttpResponseMessage response = await this.PostAsync("v2/client/session", AdvancedHttpClient.CreateContentFromObject(body));
                if (response.IsSuccessStatusCode)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    Logger.Log(LogLevel.Debug, $"Client session response: {content}");
                    JObject result = JObject.Parse(content);
                    Options = result?["options"]?.ToObject<ClientOptionsModel>() ?? new ClientOptionsModel();
                }
                else
                {
                    string content = await response.Content.ReadAsStringAsync();
                    Logger.Log(LogLevel.Warning, $"Failed to record client session: {(int)response.StatusCode} - {content}");
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Warning, $"Failed to record client session: {ex.Message}");
            }
        }

        public async Task<Stream> GenerateEdgeTTSAudio(string text, string voice, int pitch, int rate)
        {
            return await this.AuthorizedDesktopApiRequest(async () =>
            {
                await EnsureLogin();

                JObject body = new JObject();
                body["text"] = text;
                body["voice"] = voice;

                if (pitch != 0)
                {
                    body["pitch"] = pitch > 0 ? $"+{pitch}%" : $"{pitch}%";
                }
                if (rate != 0)
                {
                    body["rate"] = rate > 0 ? $"+{rate}%" : $"{rate}%";
                }

                string[] voiceParts = voice.Split('-');
                if (voiceParts.Length >= 2)
                {
                    body["lang"] = $"{voiceParts[0]}-{voiceParts[1]}";
                }

                HttpResponseMessage response = await this.PostAsync("util/tts/edge", AdvancedHttpClient.CreateContentFromObject(body));
                if (response.IsSuccessStatusCode)
                {
                    MemoryStream stream = new MemoryStream();
                    using (Stream responseStream = await response.Content.ReadAsStreamAsync())
                    {
                        await responseStream.CopyToAsync(stream);
                        stream.Position = 0;
                    }
                    return stream;
                }

                string content = await response.Content.ReadAsStringAsync();
                Logger.Log(LogLevel.Error, $"Edge TTS Error ({(int)response.StatusCode}): {content}");
                return null;
            });
        }

        public async Task<Stream> GenerateTikTokTTSAudio(string text, string voice)
        {
            return await this.AuthorizedDesktopApiRequest(async () =>
            {
                await EnsureLogin();

                JObject body = new JObject();
                body["text"] = text;
                body["voice"] = voice;

                HttpResponseMessage response = await this.PostAsync("util/tts/tiktok", AdvancedHttpClient.CreateContentFromObject(body));
                if (response.IsSuccessStatusCode)
                {
                    MemoryStream stream = new MemoryStream();
                    using (Stream responseStream = await response.Content.ReadAsStreamAsync())
                    {
                        await responseStream.CopyToAsync(stream);
                        stream.Position = 0;
                    }
                    return stream;
                }

                string content = await response.Content.ReadAsStringAsync();
                Logger.Log(LogLevel.Error, $"TikTok TTS Error ({(int)response.StatusCode}): {content}");
                return null;
            });
        }

        public async Task<string> GetTwitchClipUrl(string clipId)
        {
            return await this.AuthorizedDesktopApiRequest(async () =>
            {
                await EnsureLogin();

                HttpResponseMessage response = await this.GetAsync($"util/twitch/clips?id={Uri.EscapeDataString(clipId)}");
                if (response.IsSuccessStatusCode)
                {
                    string url = await response.Content.ReadAsStringAsync();
                    return url?.Trim();
                }

                string content = await response.Content.ReadAsStringAsync();
                Logger.Log(LogLevel.Error, $"Twitch Clip URL Error ({(int)response.StatusCode}): {content}");
                return null;
            });
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // Dispose managed state (managed objects).
                    this.cancellationTokenSource.Dispose();
                    this.StopNotificationPolling();
                }

                // Free unmanaged resources (unmanaged objects) and override a finalizer below.
                // Set large fields to null.

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion
    }
}
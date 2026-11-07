using MixItUp.Base.Model;
using MixItUp.Base.Model.Velora.Badges;
using MixItUp.Base.Model.Velora.Bots;
using MixItUp.Base.Model.Velora.ChannelPoints;
using MixItUp.Base.Model.Velora.Chat;
using MixItUp.Base.Model.Velora.Emotes;
using MixItUp.Base.Model.Velora.Streams;
using MixItUp.Base.Model.Velora.Subscriptions;
using MixItUp.Base.Model.Velora.Users;
using MixItUp.Base.Model.Web;
using MixItUp.Base.Util;
using MixItUp.Base.Web;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MixItUp.Base.Services.Velora.New
{
    /// <summary>Result of a chat moderation call, carrying the HTTP status so callers can tell a rejected
    /// request shape (4xx) apart from a transient failure.</summary>
    public class VeloraModerationResult : Result
    {
        public VeloraModerationResult() : base() { }

        public VeloraModerationResult(string message) : base(message) { }

        public int? StatusCode { get; set; }

        public bool IsRequestRejected { get { return this.StatusCode.HasValue && this.StatusCode.Value >= 400 && this.StatusCode.Value < 500; } }
    }

    public class VeloraService : StreamingPlatformServiceBaseNew
    {
        private const string OAuthBaseAddress = "https://velora.tv/oauth/authorize";
        private const string OAuthTokenAddress = "https://api.velora.tv/api/developer/oauth/token";

        // Velora now accepts http://localhost:8919/ as a registered redirect URI, so the desktop
        // client catches the OAuth redirect on its own local listener (LocalOAuthKestrelServer,
        // REDIRECT_URL) exactly like the other streaming platforms - no Desktop API relay in the path.
        public const string OAuthRedirectAddress = "http://localhost:8919/";

        // Retained as a still-registered fallback: the Mix It Up Desktop API callback that catches the
        // OAuth redirect and relays it down to the local OAuth server. Kept so the vNext server-side
        // single-socket model stays open; not used by the local redirect flow above.
        public const string OAuthRedirectFallbackAddress = "https://desktop.api.mixitup.bot/api/v2/user/velora/callback";

        private const string BaseAddressFormat = "https://api.velora.tv/api/";

        public override string Name { get { return "Velora"; } }

        public override string ClientID { get { return "velora_03e45173125ad314"; } }
        public override string ClientSecret { get { return ServiceManager.Get<SecretsService>().GetSecret("VeloraSecret"); } }

        public override StreamingPlatformTypeEnum Platform { get { return StreamingPlatformTypeEnum.Velora; } }

        public override bool IsConnected { get; protected set; }

        private Dictionary<string, string> stateToCodeVerifier = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public VeloraService(IEnumerable<string> scopes, bool isBotService = false)
            : base(BaseAddressFormat, scopes, isBotService) { }

        public async Task<UserModel> GetCurrentUser()
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                return await this.HttpClient.GetAsync<UserModel>("users/me");
            });
        }

        public async Task<UserModel> GetUserByUsername(string username)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                return await this.HttpClient.GetAsync<UserModel>("users/" + AdvancedHttpClient.URLEncodeString(username));
            });
        }

        public async Task<StreamInfoModel> GetStreamInfo()
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                return await this.HttpClient.GetAsync<StreamInfoModel>("integrations/oauth/stream/info");
            });
        }

        public async Task<Result> UpdateStreamInfo(string title = null, string categorySlug = null, IEnumerable<string> tags = null)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                JObject jobj = new JObject();
                if (!string.IsNullOrEmpty(title)) { jobj["title"] = title; }
                if (!string.IsNullOrEmpty(categorySlug)) { jobj["categorySlug"] = categorySlug; }
                if (tags != null) { jobj["tags"] = JArray.FromObject(tags.ToArray()); }
                if (jobj.Count > 0)
                {
                    HttpResponseMessage response = await this.HttpClient.PutAsync("integrations/oauth/stream/info", AdvancedHttpClient.CreateContentFromObject(jobj));
                    if (!response.IsSuccessStatusCode)
                    {
                        return new Result(await response.Content.ReadAsStringAsync());
                    }
                }

                return new Result();
            });
        }

        // Returns the first 100 categories alphabetically with no way to page past them, so this only
        // ever sees Velora's own categories plus the leading edge of the TGDB list. Use
        // SearchStreamCategories to reach anything beyond that.
        public async Task<IEnumerable<CategoryModel>> GetStreamCategories()
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                CategoriesResponseModel response = await this.HttpClient.GetAsync<CategoriesResponseModel>("integrations/oauth/stream/categories");
                return response?.Categories ?? new List<CategoryModel>();
            });
        }

        /// <summary>Searches the full category set (Velora's own plus the synced TGDB database) by name.</summary>
        public async Task<IEnumerable<CategoryModel>> SearchStreamCategories(string query, int limit = 25)
        {
            // TGDB results need at least two characters, per the endpoint docs.
            if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
            {
                return new List<CategoryModel>();
            }

            return await AsyncRunner.RunAsync(async () =>
            {
                JToken response = await this.HttpClient.GetAsync<JToken>($"categories/search?query={AdvancedHttpClient.URLEncodeString(query.Trim())}&limit={limit}");
                return CategoryModel.ParseList(response);
            });
        }

        public async Task<SendChatMessageResponseModel> SendChatMessage(string channelID, string message, string replyToMessageID = null, string replyToUsername = null, string replyToSnippet = null, bool sendAsBot = false, string effect = null, string effectColor = null)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                JObject jobj = new JObject();
                jobj["message"] = message;

                // sendAsBot posts as the app's connected bot (POST /integrations/oauth/bot/select|create)
                // on the streamer's own token - Velora bots have no credentials of their own. Confirmed
                // whitelisted live; omitted entirely when false to stay clear of the DTO whitelist.
                if (sendAsBot) { jobj["sendAsBot"] = true; }

                if (!string.IsNullOrEmpty(effect)) { jobj["effect"] = effect; }
                if (!string.IsNullOrEmpty(effectColor)) { jobj["effectColor"] = effectColor; }

                if (!string.IsNullOrEmpty(replyToMessageID))
                {
                    JObject replyTo = new JObject();
                    replyTo["messageId"] = replyToMessageID;
                    if (!string.IsNullOrEmpty(replyToUsername)) { replyTo["username"] = replyToUsername; }
                    if (!string.IsNullOrEmpty(replyToSnippet)) { replyTo["snippet"] = replyToSnippet; }
                    jobj["replyTo"] = replyTo;
                }

                return await this.HttpClient.PostAsync<SendChatMessageResponseModel>($"integrations/oauth/chat/channels/{AdvancedHttpClient.URLEncodeString(channelID)}/messages", AdvancedHttpClient.CreateContentFromObject(jobj));
            });
        }

        // ===== Bot management (streamer-token; Velora bots are entities under the streamer's account) =====
        //
        // The app's chat-bot identity on Velora is a server-side connection between this OAuth app and a
        // bot owned by the authenticated user - there is no bot OAuth login. All of these endpoints run on
        // the STREAMER's token. Request shapes confirmed live (the published DTOs are empty):
        //   select -> { botId }, create -> { botName } (3-20 chars, letters/numbers/underscores).

        /// <summary>GET the app's current bot connection: { connected, bot } (confirmed live with bot null
        /// when disconnected). The connected-state payload is parsed defensively since its exact bot key is
        /// not published.</summary>
        public async Task<VeloraBotCurrentResponseModel> GetCurrentBot()
        {
            JToken response = await AsyncRunner.RunAsync(async () =>
            {
                return await this.HttpClient.GetAsync<JToken>("integrations/oauth/bot/current");
            });

            JObject jobj = response as JObject;
            if (jobj == null)
            {
                return null;
            }

            VeloraBotCurrentResponseModel result = new VeloraBotCurrentResponseModel();
            result.Bot = VeloraBotModel.ParseSingle(jobj["bot"] ?? jobj["currentBot"] ?? jobj["botInstance"]);

            JToken connected = jobj["connected"];
            result.Connected = (connected != null && connected.Type == JTokenType.Boolean)
                ? connected.Value<bool>()
                : result.Bot != null;

            return result;
        }

        /// <summary>GET the bots owned by the authenticated user that the app can connect to.</summary>
        public async Task<IEnumerable<VeloraBotModel>> GetAvailableBots()
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                JToken response = await this.HttpClient.GetAsync<JToken>("integrations/oauth/bot/available");
                return (IEnumerable<VeloraBotModel>)VeloraBotModel.ParseList(response);
            });
        }

        /// <summary>GET whether a bot name is free: { available, name }. Defaults to available when the
        /// check itself fails so creation still reaches the server (which enforces the 409 anyway).</summary>
        public async Task<bool> CheckBotNameAvailability(string botName)
        {
            JToken response = await AsyncRunner.RunAsync(async () =>
            {
                return await this.HttpClient.GetAsync<JToken>("integrations/oauth/bot/check-name/" + AdvancedHttpClient.URLEncodeString(botName));
            });

            JToken available = (response as JObject)?["available"];
            return available == null || available.Type != JTokenType.Boolean || available.Value<bool>();
        }

        /// <summary>POST create a bot owned by the user AND connect it to this app (Velora auto-connects
        /// on create). 403 = not an affiliate / bot limit reached; 409 = name taken.</summary>
        public async Task<Result> CreateBot(string botName)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                JObject jobj = new JObject();
                jobj["botName"] = botName;

                HttpResponseMessage response = await this.HttpClient.PostAsync("integrations/oauth/bot/create", AdvancedHttpClient.CreateContentFromObject(jobj));
                if (!response.IsSuccessStatusCode)
                {
                    return new Result(await response.Content.ReadAsStringAsync());
                }
                return new Result();
            });
        }

        /// <summary>POST connect an existing bot (by ID) to this app. 404 = "Bot not found or not owned by you".</summary>
        public async Task<Result> SelectBot(string botID)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                JObject jobj = new JObject();
                jobj["botId"] = botID;

                HttpResponseMessage response = await this.HttpClient.PostAsync("integrations/oauth/bot/select", AdvancedHttpClient.CreateContentFromObject(jobj));
                if (!response.IsSuccessStatusCode)
                {
                    return new Result(await response.Content.ReadAsStringAsync());
                }
                return new Result();
            });
        }

        /// <summary>DELETE the app's bot connection. The bot itself survives (it belongs to the user).</summary>
        public async Task<Result> DisconnectBot()
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                bool success = await this.HttpClient.DeleteAsync("integrations/oauth/bot/disconnect");
                return success ? new Result() : new Result("Failed to disconnect the Velora bot");
            });
        }

        /// <summary>Whether the current OAuth token was authorized with the given scope. Lets callers
        /// skip endpoints a pre-upgrade token cannot reach (e.g. bot:commands) instead of collecting 403s.</summary>
        public bool HasScope(string scope)
        {
            string scopeList = this.GetOAuthTokenCopy()?.ScopeList;
            return !string.IsNullOrEmpty(scopeList) && scopeList.Split(',').Contains(scope, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>POST the app's complete command list to Velora for the bot profile page / the
        /// channel's available-commands list. Full replacement per call; requires bot:commands and a
        /// connected bot (404 otherwise); rate limited at 10 req/min; max 500 commands. Body confirmed
        /// live: { botId, commands: [{ trigger, description?, aliases? }] }.</summary>
        public async Task<Result> SyncBotCommands(string botID, IEnumerable<VeloraBotSyncCommandModel> commands)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                JObject jobj = new JObject();
                jobj["botId"] = botID;
                jobj["commands"] = JArray.FromObject((commands ?? Enumerable.Empty<VeloraBotSyncCommandModel>()).Take(500).ToArray());

                HttpResponseMessage response = await this.HttpClient.PostAsync("integrations/oauth/bot/commands/sync", AdvancedHttpClient.CreateContentFromObject(jobj));
                if (!response.IsSuccessStatusCode)
                {
                    return new Result(await response.Content.ReadAsStringAsync());
                }
                return new Result();
            });
        }

        // Velora's documented avatar constraints: JPEG, PNG, GIF, or WebP up to 5MB.
        public static readonly IReadOnlyCollection<string> BotAvatarValidExtensions = new List<string>() { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        public const long BotAvatarMaxFileSizeBytes = 5 * 1024 * 1024;

        private static readonly Dictionary<string, string> BotAvatarContentTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { ".jpg", "image/jpeg" },
            { ".jpeg", "image/jpeg" },
            { ".png", "image/png" },
            { ".gif", "image/gif" },
            { ".webp", "image/webp" },
        };

        /// <summary>POST a multipart avatar upload for a bot the user owns (field name "file";
        /// JPEG/PNG/GIF/WebP up to 5MB per the endpoint description).</summary>
        public async Task<Result> UploadBotAvatar(string botID, string filePath)
        {
            string extension = Path.GetExtension(filePath) ?? string.Empty;
            if (!BotAvatarContentTypes.TryGetValue(extension, out string contentType))
            {
                return new Result(string.Format(Resources.ImageFileBrowserUnsupportedType, string.Join(", ", BotAvatarValidExtensions)));
            }

            byte[] bytes = await ServiceManager.Get<IFileService>().ReadFileAsBytes(filePath);
            if (bytes == null || bytes.Length == 0)
            {
                return new Result(string.Format(Resources.FailedToReadFile, filePath));
            }
            if (bytes.Length > BotAvatarMaxFileSizeBytes)
            {
                return new Result(string.Format(Resources.ImageFileBrowserFileTooLarge, BotAvatarMaxFileSizeBytes / (1024 * 1024)));
            }

            return await AsyncRunner.RunAsync(async () =>
            {
                using (MultipartFormDataContent content = new MultipartFormDataContent())
                {
                    ByteArrayContent fileContent = new ByteArrayContent(bytes);
                    fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                    content.Add(fileContent, "file", Path.GetFileName(filePath));

                    HttpResponseMessage response = await this.HttpClient.PostAsync($"integrations/oauth/bot/{AdvancedHttpClient.URLEncodeString(botID)}/avatar", content);
                    if (!response.IsSuccessStatusCode)
                    {
                        return new Result(await response.Content.ReadAsStringAsync());
                    }
                    return new Result();
                }
            });
        }

        /// <summary>GET the bot's rename-cooldown status (Velora allows one username change per 90 days).
        /// The response shape is unpublished, so the caller interprets it defensively.</summary>
        public async Task<JToken> GetBotUsernameCooldown(string botID)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                return await this.HttpClient.GetAsync<JToken>($"bots/{AdvancedHttpClient.URLEncodeString(botID)}/username-cooldown");
            });
        }

        /// <summary>PATCH a bot the user owns (Bot Studio surface; UpdateBotDto). Only the supplied
        /// fields are sent. 409 = username taken.</summary>
        public async Task<Result> UpdateBot(string botID, string username = null, string displayName = null)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                JObject jobj = new JObject();
                if (!string.IsNullOrEmpty(username)) { jobj["username"] = username; }
                if (!string.IsNullOrEmpty(displayName)) { jobj["displayName"] = displayName; }
                if (jobj.Count == 0) { return new Result(); }

                HttpResponseMessage response = await this.HttpClient.PatchAsync($"bots/{AdvancedHttpClient.URLEncodeString(botID)}", AdvancedHttpClient.CreateContentFromObject(jobj));
                if (!response.IsSuccessStatusCode)
                {
                    return new Result(await response.Content.ReadAsStringAsync());
                }
                return new Result();
            });
        }

        // Velora publishes ModerateChatDto with no properties, so the request shape is pinned to what the
        // endpoint itself reports. It whitelists "targetUserId" / "targetUsername" (NOT "userId" / "username")
        // and requires one of them for every action, including "delete" - unlike the session-authenticated
        // /api/chat/channels/{id}/moderate route the Velora website uses, which takes only messageId + action.
        private static readonly HashSet<string> RequiredModerationProperties = new HashSet<string>(StringComparer.Ordinal)
        {
            "action", "targetUserId", "targetUsername",
        };

        // The endpoint validates against a property whitelist and names each rejected property in the 400 body:
        // {"message":["property userId should not exist"],"error":"Bad Request","statusCode":400}
        private static readonly Regex DisallowedModerationPropertyRegex = new Regex(@"property (\w+) should not exist", RegexOptions.IgnoreCase);

        private readonly HashSet<string> unsupportedModerationProperties = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Returns null when the request could not be made at all (AsyncRunner swallows the exception).</summary>
        public async Task<VeloraModerationResult> ModerateUser(string channelID, string action, string targetUserID = null, string targetUsername = null, int? durationSeconds = null, string reason = null, string messageID = null)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                if (string.IsNullOrEmpty(targetUserID) && string.IsNullOrEmpty(targetUsername))
                {
                    return new VeloraModerationResult($"chat moderate '{action}' requires a target user");
                }

                VeloraModerationResult result = await this.PostModeration(channelID, action, targetUserID, targetUsername, durationSeconds, reason, messageID);
                if (result.Success || result.StatusCode != 400)
                {
                    return result;
                }

                // An optional property Velora does not accept (a reason, say) should not fail the whole
                // moderation action: drop whatever it named and retry once, then remember it for this session.
                if (this.RecordUnsupportedModerationProperties(result.Message))
                {
                    result = await this.PostModeration(channelID, action, targetUserID, targetUsername, durationSeconds, reason, messageID);
                }
                return result;
            });
        }

        private async Task<VeloraModerationResult> PostModeration(string channelID, string action, string targetUserID, string targetUsername, int? durationSeconds, string reason, string messageID)
        {
            JObject jobj = new JObject();
            this.AddModerationProperty(jobj, "action", action);

            // Prefer the ID; the username is only a fallback for a user whose platform ID was never resolved.
            if (!string.IsNullOrEmpty(targetUserID)) { this.AddModerationProperty(jobj, "targetUserId", targetUserID); }
            else { this.AddModerationProperty(jobj, "targetUsername", targetUsername); }

            if (durationSeconds.HasValue) { this.AddModerationProperty(jobj, "durationSeconds", durationSeconds.Value); }
            if (!string.IsNullOrEmpty(reason)) { this.AddModerationProperty(jobj, "reason", reason); }
            if (!string.IsNullOrEmpty(messageID)) { this.AddModerationProperty(jobj, "messageId", messageID); }

            HttpResponseMessage response = await this.HttpClient.PostAsync($"integrations/oauth/chat/channels/{AdvancedHttpClient.URLEncodeString(channelID)}/moderate", AdvancedHttpClient.CreateContentFromObject(jobj));
            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync();
                return new VeloraModerationResult($"HTTP {(int)response.StatusCode} {response.ReasonPhrase} on chat moderate '{action}': {body}") { StatusCode = (int)response.StatusCode };
            }
            return new VeloraModerationResult();
        }

        private void AddModerationProperty(JObject jobj, string name, JToken value)
        {
            if (!this.unsupportedModerationProperties.Contains(name))
            {
                jobj[name] = value;
            }
        }

        private bool RecordUnsupportedModerationProperties(string responseBody)
        {
            bool discovered = false;
            foreach (Match match in DisallowedModerationPropertyRegex.Matches(responseBody ?? string.Empty))
            {
                string name = match.Groups[1].Value;
                if (RequiredModerationProperties.Contains(name))
                {
                    continue;
                }

                if (this.unsupportedModerationProperties.Add(name))
                {
                    Logger.Log(LogLevel.Error, $"Velora rejected the chat moderate property '{name}'; omitting it from subsequent requests.");
                    discovered = true;
                }
            }
            return discovered;
        }

        public async Task<IEnumerable<ChannelPointRewardModel>> GetChannelPointRewards(string channelID)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                // includeDisabled is a required query parameter on this endpoint; include disabled
                // rewards so the channel points command editor can still map to a temporarily
                // disabled reward.
                ChannelPointRewardsResponseModel response = await this.HttpClient.GetAsync<ChannelPointRewardsResponseModel>($"channel-points/{AdvancedHttpClient.URLEncodeString(channelID)}/items/with-built-in?includeDisabled=true");
                return response?.AllRewards ?? new List<ChannelPointRewardModel>();
            });
        }

        public async Task<IEnumerable<EmoteModel>> GetEmotes(string channelUsername = null)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                string requestUri = "emotes";
                if (!string.IsNullOrEmpty(channelUsername))
                {
                    requestUri += "?channel=" + AdvancedHttpClient.URLEncodeString(channelUsername);
                }

                JToken response = await this.HttpClient.GetAsync<JToken>(requestUri);
                return EmoteModel.ParseEmotes(response);
            });
        }

        public async Task<int?> GetSubscriberCount()
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                JToken response = await this.HttpClient.GetAsync<JToken>("developer/subscriptions/count");
                if (response == null)
                {
                    return (int?)null;
                }
                return VeloraSubscriberCount.Parse(response);
            });
        }

        public async Task<UserStreamModel> GetUserStream(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return null;
            }

            return await AsyncRunner.RunAsync(async () =>
            {
                JToken response = await this.HttpClient.GetAsync<JToken>("streams/user/" + AdvancedHttpClient.URLEncodeString(username));
                return UserStreamModel.Parse(response);
            });
        }

        public async Task<IEnumerable<ChannelSubscriptionBadgeModel>> GetChannelSubscriptionBadges(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return new List<ChannelSubscriptionBadgeModel>();
            }

            return await AsyncRunner.RunAsync(async () =>
            {
                JToken response = await this.HttpClient.GetAsync<JToken>("badges/channel/" + AdvancedHttpClient.URLEncodeString(username));
                return ChannelSubscriptionBadgeModel.ParseList(response);
            });
        }

        // GET the global/platform badge catalog (public, no auth). Chat messages reference these
        // badges by slug in their badges[] list.
        public async Task<IEnumerable<CatalogBadgeModel>> GetBadgeCatalog()
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                JToken response = await this.HttpClient.GetAsync<JToken>("badges/catalog");
                return CatalogBadgeModel.ParseList(response);
            });
        }

        // ===== Phase 3: slim REST management / config (features with no socket equivalent) =====

        // GET chat settings - slow/followers-only/subscribers-only/emote-only modes. Shape confirmed live.
        public async Task<VeloraChatSettingsModel> GetChatSettings()
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                return await this.HttpClient.GetAsync<VeloraChatSettingsModel>("integrations/oauth/chat/settings");
            });
        }

        // PATCH chat settings - only the supplied fields are sent (partial update).
        public async Task<Result> UpdateChatSettings(bool? slowMode = null, int? slowModeSeconds = null, bool? followersOnly = null, bool? subscribersOnly = null, bool? emoteOnly = null)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                JObject jobj = new JObject();
                if (slowMode.HasValue) { jobj["slowMode"] = slowMode.Value; }
                if (slowModeSeconds.HasValue) { jobj["slowModeSeconds"] = slowModeSeconds.Value; }
                if (followersOnly.HasValue) { jobj["followersOnly"] = followersOnly.Value; }
                if (subscribersOnly.HasValue) { jobj["subscribersOnly"] = subscribersOnly.Value; }
                if (emoteOnly.HasValue) { jobj["emoteOnly"] = emoteOnly.Value; }
                if (jobj.Count == 0) { return new Result(); }

                HttpResponseMessage response = await this.HttpClient.PatchAsync("integrations/oauth/chat/settings", AdvancedHttpClient.CreateContentFromObject(jobj));
                if (!response.IsSuccessStatusCode)
                {
                    return new Result(await response.Content.ReadAsStringAsync());
                }
                return new Result();
            });
        }

        // GET paginated subscriber roster. Envelope confirmed live: { data[], total, page, perPage, hasMore }.
        public async Task<VeloraSubscriberRosterModel> GetSubscribers(int? page = null, int? perPage = null)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                string requestUri = "developer/subscriptions";
                List<string> query = new List<string>();
                if (page.HasValue) { query.Add("page=" + page.Value); }
                if (perPage.HasValue) { query.Add("limit=" + perPage.Value); }
                if (query.Count > 0) { requestUri += "?" + string.Join("&", query); }
                return await this.HttpClient.GetAsync<VeloraSubscriberRosterModel>(requestUri);
            });
        }

        // POST create a clip. Body documented: { title, durationMs (15000-120000), startOffsetMs?, highlight? }.
        public async Task<VeloraClipModel> CreateClip(string username, string title = null, int durationMs = 30000, int? startOffsetMs = null, bool highlight = false)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return null;
            }

            return await AsyncRunner.RunAsync(async () =>
            {
                JObject jobj = new JObject();
                if (!string.IsNullOrEmpty(title)) { jobj["title"] = title; }
                jobj["durationMs"] = Math.Min(Math.Max(durationMs, 15000), 120000);
                if (startOffsetMs.HasValue) { jobj["startOffsetMs"] = startOffsetMs.Value; }
                if (highlight) { jobj["highlight"] = true; }

                JToken response = await this.HttpClient.PostAsync<JToken>($"streams/{AdvancedHttpClient.URLEncodeString(username)}/clips", AdvancedHttpClient.CreateContentFromObject(jobj));
                return VeloraClipModel.Parse(response);
            });
        }

        // POST refund a channel-point redemption. Velora supports ONLY refund (no fulfill/cancel/approve/reject).
        public async Task<Result> RefundRedemption(string channelID, string redemptionID)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                HttpResponseMessage response = await this.HttpClient.PostAsync($"channel-points/{AdvancedHttpClient.URLEncodeString(channelID)}/redemptions/{AdvancedHttpClient.URLEncodeString(redemptionID)}/refund", AdvancedHttpClient.CreateContentFromObject(new JObject()));
                if (!response.IsSuccessStatusCode)
                {
                    return new Result(await response.Content.ReadAsStringAsync());
                }
                return new Result();
            });
        }

        // DELETE a channel-point item.
        public async Task<Result> DeleteChannelPointItem(string channelID, string itemID)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                bool success = await this.HttpClient.DeleteAsync($"channel-points/{AdvancedHttpClient.URLEncodeString(channelID)}/items/{AdvancedHttpClient.URLEncodeString(itemID)}");
                return success ? new Result() : new Result("Failed to delete the Velora channel-point item");
            });
        }

        // ⚠️ VERIFY-LIVE: the channel-point item create/update REQUEST DTOs are undocumented. The caller
        // supplies the body; field names should mirror the documented item response shape
        // (name/cost/description/iconUrl/enabled/builtInType/...). Confirm from Velora web-client DevTools.
        public async Task<JToken> CreateChannelPointItem(string channelID, JObject item)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                return await this.HttpClient.PostAsync<JToken>($"channel-points/{AdvancedHttpClient.URLEncodeString(channelID)}/items", AdvancedHttpClient.CreateContentFromObject(item ?? new JObject()));
            });
        }

        public async Task<Result> UpdateChannelPointItem(string channelID, string itemID, JObject changes)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                HttpResponseMessage response = await this.HttpClient.PatchAsync($"channel-points/{AdvancedHttpClient.URLEncodeString(channelID)}/items/{AdvancedHttpClient.URLEncodeString(itemID)}", AdvancedHttpClient.CreateContentFromObject(changes ?? new JObject()));
                if (!response.IsSuccessStatusCode)
                {
                    return new Result(await response.Content.ReadAsStringAsync());
                }
                return new Result();
            });
        }

        // ⚠️ VERIFY-LIVE: Velora-unique grant/deduct channel-points admin DTO is undocumented (best-effort
        // { userId/username, amount }). action is "grant" or "deduct".
        public async Task<Result> AdjustChannelPoints(string channelID, string action, string userID = null, string username = null, int amount = 0)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                JObject jobj = new JObject();
                if (!string.IsNullOrEmpty(userID)) { jobj["userId"] = userID; }
                if (!string.IsNullOrEmpty(username)) { jobj["username"] = username; }
                jobj["amount"] = amount;

                string endpoint = string.Equals(action, "deduct", StringComparison.OrdinalIgnoreCase) ? "deduct" : "grant";
                HttpResponseMessage response = await this.HttpClient.PostAsync($"channel-points/{AdvancedHttpClient.URLEncodeString(channelID)}/admin/{endpoint}", AdvancedHttpClient.CreateContentFromObject(jobj));
                if (!response.IsSuccessStatusCode)
                {
                    return new Result(await response.Content.ReadAsStringAsync());
                }
                return new Result();
            });
        }

        // ⚠️ VERIFY-LIVE: the /creator/roles roleType vocabulary (moderator/vip/...) is undocumented; the
        // Chat WS slash command is the alternative once its grammar is confirmed.
        public async Task<Result> GrantCreatorRole(string memberID, string roleType)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                JObject jobj = new JObject();
                if (!string.IsNullOrEmpty(memberID)) { jobj["memberId"] = memberID; }
                if (!string.IsNullOrEmpty(roleType)) { jobj["roleType"] = roleType; }

                HttpResponseMessage response = await this.HttpClient.PostAsync("creator/roles", AdvancedHttpClient.CreateContentFromObject(jobj));
                if (!response.IsSuccessStatusCode)
                {
                    return new Result(await response.Content.ReadAsStringAsync());
                }
                return new Result();
            });
        }

        public async Task<Result> RevokeCreatorRole(string memberID, string roleType)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                bool success = await this.HttpClient.DeleteAsync($"creator/roles/{AdvancedHttpClient.URLEncodeString(memberID)}/{AdvancedHttpClient.URLEncodeString(roleType)}");
                return success ? new Result() : new Result("Failed to revoke the Velora creator role");
            });
        }

        // Polls. GET active polls is read-only; poll results also arrive on the Events WS.
        public async Task<JToken> GetActivePolls(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return null;
            }

            return await AsyncRunner.RunAsync(async () =>
            {
                return await this.HttpClient.GetAsync<JToken>($"streams/{AdvancedHttpClient.URLEncodeString(username)}/polls/active");
            });
        }

        // The caller supplies the create-poll body defined by the Velora OpenAPI schema.
        public async Task<JToken> CreatePoll(string username, JObject poll)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                return await this.HttpClient.PostAsync<JToken>($"streams/{AdvancedHttpClient.URLEncodeString(username)}/polls", AdvancedHttpClient.CreateContentFromObject(poll ?? new JObject()));
            });
        }

        public async Task<Result> EndPoll(string username, string pollID)
        {
            return await AsyncRunner.RunAsync(async () =>
            {
                HttpResponseMessage response = await this.HttpClient.PostAsync($"streams/{AdvancedHttpClient.URLEncodeString(username)}/polls/{AdvancedHttpClient.URLEncodeString(pollID)}/end", AdvancedHttpClient.CreateContentFromObject(new JObject()));
                if (!response.IsSuccessStatusCode)
                {
                    return new Result(await response.Content.ReadAsStringAsync());
                }
                return new Result();
            });
        }

        protected override async Task<string> GetAuthorizationCodeURL(IEnumerable<string> scopes, string state, bool forceApprovalPrompt = false)
        {
            string codeVerifier = CreateCodeVerifier();
            string codeChallenge = CreateCodeChallenge(codeVerifier);
            this.stateToCodeVerifier[state] = codeVerifier;

            Dictionary<string, string> parameters = new Dictionary<string, string>()
            {
                { "client_id", this.ClientID },
                { "redirect_uri", OAuthRedirectAddress },
                { "response_type", "code" },
                { "scope", string.Join(" ", scopes) },
                { "state", state },
                { "code_challenge", codeChallenge },
                { "code_challenge_method", "S256" },
            };

            FormUrlEncodedContent content = new FormUrlEncodedContent(parameters.AsEnumerable());
            return OAuthBaseAddress + "?" + await content.ReadAsStringAsync();
        }

        protected override async Task<OAuthTokenModel> RequestOAuthToken(string authorizationCode, IEnumerable<string> scopes, string state)
        {
            this.stateToCodeVerifier.TryGetValue(state, out string codeVerifier);
            this.stateToCodeVerifier.Remove(state);

            if (string.IsNullOrWhiteSpace(codeVerifier))
            {
                Logger.Log(LogLevel.Error, "Velora OAuth PKCE verifier missing for token exchange");
                return null;
            }

            JObject body = new JObject();
            body["grant_type"] = "authorization_code";
            body["client_id"] = this.ClientID;
            body["client_secret"] = this.ClientSecret;
            body["code"] = authorizationCode;
            body["redirect_uri"] = OAuthRedirectAddress;
            body["code_verifier"] = codeVerifier;

            OAuthTokenModel token = await this.RequestJSONOAuthToken(body);
            if (token != null)
            {
                token.clientID = this.ClientID;
                token.ScopeList = OAuthTokenModel.GenerateScopeList(scopes);
            }
            return token;
        }

        protected override async Task RefreshOAuthToken()
        {
            JObject body = new JObject();
            body["grant_type"] = "refresh_token";
            body["client_id"] = this.ClientID;
            body["client_secret"] = this.ClientSecret;
            body["refresh_token"] = this.OAuthToken.refreshToken;

            OAuthTokenModel newToken = await this.RequestJSONOAuthToken(body);
            if (newToken != null)
            {
                newToken.clientID = this.OAuthToken.clientID;
                newToken.ScopeList = this.OAuthToken.ScopeList;
                this.OAuthToken = newToken;
            }
        }

        private async Task<OAuthTokenModel> RequestJSONOAuthToken(JObject body)
        {
            try
            {
                using (AdvancedHttpClient client = new AdvancedHttpClient())
                {
                    return await client.PostAsync<OAuthTokenModel>(OAuthTokenAddress, AdvancedHttpClient.CreateContentFromObject(body));
                }
            }
            catch (Exception ex) { Logger.Log(ex); }
            return null;
        }

        private static string CreateCodeVerifier()
        {
            byte[] randomBytes = new byte[64];
            RandomNumberGenerator.Fill(randomBytes);
            return Base64UrlEncode(randomBytes);
        }

        private static string CreateCodeChallenge(string codeVerifier)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.ASCII.GetBytes(codeVerifier);
                byte[] hash = sha256.ComputeHash(bytes);
                return Base64UrlEncode(hash);
            }
        }

        private static string Base64UrlEncode(byte[] bytes)
        {
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
    }
}

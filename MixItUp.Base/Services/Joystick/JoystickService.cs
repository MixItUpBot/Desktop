using MixItUp.Base.Model;
using MixItUp.Base.Model.Web;
using MixItUp.Base.Util;
using MixItUp.Base.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace MixItUp.Base.Services.Joystick
{
    public class JoystickService : StreamingPlatformServiceBaseNew
    {
        private static readonly string[] Scopes = { "identity:read", "stream:read", "chat:read", "chat:write" };
        private static readonly HttpClient Relay = new HttpClient(HttpConnectionFactory.CreateHandler(allowAutoRedirect: false))
        {
            BaseAddress = new Uri(MixItUpService.MixItUpAPIEndpoint + "v2/joystick/"),
            Timeout = TimeSpan.FromSeconds(25)
        };

        private readonly HttpClient relay;

        public JoystickService() : this(Relay) { }
        internal JoystickService(HttpClient relay) : base(relay.BaseAddress.AbsoluteUri, Scopes)
        {
            this.relay = relay;
        }
        public override string Name => "Joystick.tv";
        public override StreamingPlatformTypeEnum Platform => StreamingPlatformTypeEnum.Joystick;
        public override string ClientID => ServiceManager.Get<SecretsService>().GetSecret("JOYSTICK_TV_CLIENT_ID");
        public override string ClientSecret => null;
        public override bool IsConnected { get; protected set; }

        public override Task<Result> AutomaticConnect()
        {
            this.OAuthToken = this.GetAuthenticationSettings()?.UserOAuthToken;
            this.IsConnected = !string.IsNullOrWhiteSpace(this.OAuthToken?.accessToken);
            return Task.FromResult(new Result(this.IsConnected));
        }

        protected override Task<string> GetAuthorizationCodeURL(IEnumerable<string> scopes, string state, bool forceApprovalPrompt = false)
        {
            var query = HttpUtility.ParseQueryString(string.Empty);
            query["client_id"] = this.ClientID;
            query["response_type"] = "code";
            query["redirect_uri"] = LocalOAuthKestrelServer.REDIRECT_URL;
            query["scope"] = string.Join(" ", scopes);
            query["state"] = state;
            return Task.FromResult("https://joystick.tv/api/oauth/authorize?" + query);
        }

        protected override async Task<OAuthTokenModel> RequestOAuthToken(string authorizationCode, IEnumerable<string> scopes, string state)
        {
            JObject result = await this.Request(HttpMethod.Post, "exchange", new { code = authorizationCode }, authenticated: false);
            string session = result?.Value<string>("session");
            return string.IsNullOrEmpty(session) ? null : new OAuthTokenModel
            {
                accessToken = session, clientID = this.ClientID, ScopeList = OAuthTokenModel.GenerateScopeList(scopes)
            };
        }

        // Desktop holds a Mix It Up session. Joystick token rotation is handled by DesktopAPI.
        protected override Task RefreshOAuthToken() => Task.CompletedTask;

        public override async Task Disable()
        {
            if (this.OAuthToken != null) { await this.Request(HttpMethod.Delete, "installation"); }
            await base.Disable();
        }

        internal async Task<JObject> Request(HttpMethod method, string path, object body = null, bool authenticated = true)
        {
            using var request = new HttpRequestMessage(method, path);
            if (authenticated) { request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", this.OAuthToken?.accessToken); }
            if (body != null) { request.Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json"); }
            using var response = await this.relay.SendAsync(request);
            response.EnsureSuccessStatusCode();
            string json = await response.Content.ReadAsStringAsync();
            return string.IsNullOrWhiteSpace(json) ? null : JObject.Parse(json);
        }
    }
}

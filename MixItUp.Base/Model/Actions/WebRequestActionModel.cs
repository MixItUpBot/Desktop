using MixItUp.Base.Model.Commands;
using MixItUp.Base.Services;
using Newtonsoft.Json.Linq;
using MixItUp.Base.Util;
using MixItUp.Base.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace MixItUp.Base.Model.Actions
{
    public enum WebRequestResponseParseTypeEnum
    {
        PlainText,
        JSONToSpecialIdentifiers
    }

    public enum HttpMethodEnum
    {
        GET,
        POST,
        PUT,
        DELETE
    }

    [DataContract]
    public class WebRequestActionModel : ActionModelBase
    {
        public const string ResponseSpecialIdentifier = "webrequestresult";

        [DataMember]
        public string Url { get; set; }

        [DataMember]
        public WebRequestResponseParseTypeEnum ResponseType { get; set; }

        [DataMember]
        public Dictionary<string, string> JSONToSpecialIdentifiers { get; set; }

        [DataMember]
        public HttpMethodEnum HttpMethod { get; set; } = HttpMethodEnum.GET;

        [DataMember]
        public Dictionary<string, string> CustomHeaders { get; set; }

        [DataMember]
        public string RequestBody { get; set; }

        public WebRequestActionModel(string url, WebRequestResponseParseTypeEnum responseType, HttpMethodEnum httpMethod = HttpMethodEnum.GET, Dictionary<string, string> customHeaders = null, string requestBody = null)
            : base(ActionTypeEnum.WebRequest)
        {
            this.Url = url;
            this.ResponseType = responseType;
            this.HttpMethod = httpMethod;
            this.CustomHeaders = customHeaders ?? new Dictionary<string, string>();
            this.RequestBody = requestBody;
        }

        public WebRequestActionModel(string url, Dictionary<string, string> jsonToSpecialIdentifiers, HttpMethodEnum httpMethod = HttpMethodEnum.GET, Dictionary<string, string> customHeaders = null, string requestBody = null)
            : this(url, WebRequestResponseParseTypeEnum.JSONToSpecialIdentifiers, httpMethod, customHeaders, requestBody)
        {
            this.JSONToSpecialIdentifiers = jsonToSpecialIdentifiers;
        }

        [Obsolete]
        public WebRequestActionModel() { }

        protected override async Task PerformInternal(CommandParametersModel parameters)
        {
            string url = await ReplaceStringWithSpecialModifiers(this.Url, parameters);
            if (ServiceManager.Get<IFileService>().FileExists(url))
            {
                await this.ProcessContents(parameters, await ServiceManager.Get<IFileService>().ReadFile(url));
            }
            else
            {
                using (AdvancedHttpClient httpClient = new AdvancedHttpClient())
                {
                    httpClient.DefaultRequestHeaders.Add("User-Agent", $"MixItUp/{Assembly.GetEntryAssembly().GetName().Version.ToString()} (Web call from Mix It Up; https://mixitup.bot; support@mixitupapp.com)");

                    string requestContentType = "application/json";

                    if (this.CustomHeaders != null)
                    {
                        foreach (var header in this.CustomHeaders)
                        {
                            string headerValue = await ReplaceStringWithSpecialModifiers(header.Value, parameters);

                            if (string.Equals(header.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))
                            {
                                if (!string.IsNullOrWhiteSpace(headerValue))
                                {
                                    requestContentType = headerValue;
                                }
                                continue;
                            }

                            try
                            {
                                httpClient.DefaultRequestHeaders.Add(header.Key, headerValue);
                            }
                            catch (Exception ex)
                            {
                                Logger.Log(LogLevel.Error, $"Failed to add custom header '{header.Key}': {ex.Message}");
                            }
                        }
                    }

                    string targetUrl = await ReplaceStringWithSpecialModifiers(this.Url, parameters, encode: true);
                    if (!Uri.TryCreate(targetUrl, UriKind.Absolute, out _))
                    {
                        targetUrl = url;
                    }

                    HttpContent content = null;
                    if (!string.IsNullOrEmpty(this.RequestBody) && (this.HttpMethod == HttpMethodEnum.POST || this.HttpMethod == HttpMethodEnum.PUT))
                    {
                        string processedBody = await ReplaceStringWithSpecialModifiers(this.RequestBody, parameters);
                        content = new StringContent(processedBody, Encoding.UTF8, requestContentType);
                    }

                    HttpResponseMessage response = null;
                    switch (this.HttpMethod)
                    {
                        case HttpMethodEnum.GET:
                            response = await httpClient.GetAsync(targetUrl);
                            break;
                        case HttpMethodEnum.POST:
                            response = await httpClient.PostAsync(targetUrl, content);
                            break;
                        case HttpMethodEnum.PUT:
                            response = await httpClient.PutAsync(targetUrl, content);
                            break;
                        case HttpMethodEnum.DELETE:
                            response = await httpClient.DeleteAsyncWithResponse(targetUrl, content);
                            break;
                    }

                    using (response)
                    {
                        if (string.Equals(response?.Content?.Headers?.ContentType?.CharSet, "utf8", StringComparison.OrdinalIgnoreCase))
                        {
                            response.Content.Headers.ContentType.CharSet = "utf-8";
                        }

                        if (response.IsSuccessStatusCode)
                        {
                            await this.ProcessContents(parameters, await response.Content.ReadAsStringAsync());
                        }
                        else
                        {
                            string body = string.Empty;
                            try
                            {
                                body = await response.Content.ReadAsStringAsync();
                            }
                            catch { }

                            Logger.Log(LogLevel.Error, $"{nameof(WebRequestActionModel)}: Failed to call '{targetUrl}'. Status code: {response.StatusCode}");
                            Logger.Log(LogLevel.Error, $"Response Body: {body}");
                        }
                    }
                }
            }
        }

        private async Task ProcessContents(CommandParametersModel parameters, string webRequestResult)
        {
            if (!string.IsNullOrEmpty(webRequestResult))
            {
                string decodedWebRequestResult = HttpUtility.HtmlDecode(webRequestResult);
                if (this.ResponseType == WebRequestResponseParseTypeEnum.JSONToSpecialIdentifiers)
                {
                    try
                    {
                        if (this.JSONToSpecialIdentifiers != null)
                        {
                            await ProcessJSONToSpecialIdentifiers(webRequestResult, this.JSONToSpecialIdentifiers, parameters);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(ex);
                    }
                }
                else
                {
                    parameters.SpecialIdentifiers[ResponseSpecialIdentifier] = decodedWebRequestResult;
                }
            }
        }

        public static async Task ProcessJSONToSpecialIdentifiers(string body, Dictionary<string, string> jsonToSpecialIdentifiers, CommandParametersModel parameters)
        {
            try
            {
                JToken jToken = JToken.Parse(body);

                foreach (var kvp in jsonToSpecialIdentifiers)
                {
                    try
                    {
                        string key = await ReplaceStringWithSpecialModifiers(kvp.Key, parameters);
                        string[] splits = key.Split(new char[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                        if (splits.Count() > 0)
                        {
                            JToken currentToken = jToken;
                            for (int i = 0; i < splits.Count(); i++)
                            {
                                if (currentToken is JObject)
                                {
                                    JObject jobjToken = (JObject)currentToken;
                                    if (jobjToken.ContainsKey(splits[i]))
                                    {
                                        currentToken = jobjToken[splits[i]];
                                    }
                                    else
                                    {
                                        currentToken = null;
                                        break;
                                    }
                                }
                                else if (currentToken is JArray)
                                {
                                    JArray jarrayToken = (JArray)currentToken;
                                    if (int.TryParse(splits[i], out int index) && index >= 0 && index < jarrayToken.Count)
                                    {
                                        currentToken = jarrayToken[index];
                                    }
                                    else
                                    {
                                        currentToken = null;
                                        break;
                                    }
                                }
                                else
                                {
                                    currentToken = null;
                                    break;
                                }
                            }

                            if (currentToken != null)
                            {
                                parameters.SpecialIdentifiers[kvp.Value] = await ReplaceStringWithSpecialModifiers(HttpUtility.HtmlDecode(currentToken.ToString()), parameters);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }
    }
}

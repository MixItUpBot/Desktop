using MixItUp.Base.Model;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Util;
using MixItUp.Base.Web;
using MixItUp.Base.ViewModel.Chat;
using MixItUp.Base.ViewModel.User;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.ServerSentEvents;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Base.Services.External
{
    public class StreamlootsPurchaseModel
    {
        public string type { get; set; }
        public StreamlootsPurchaseDataModel data { get; set; }
    }

    public class StreamlootsPurchaseDataModel
    {
        public List<StreamlootsDataFieldModel> fields { get; set; }

        // This is the person receiving the action (if gifted)
        public string Giftee
        {
            get
            {
                var fieldItem = this.fields.FirstOrDefault(f => f.name.Equals("giftee", StringComparison.OrdinalIgnoreCase));
                return (fieldItem != null) ? fieldItem.value : string.Empty;
            }
        }

        public int Quantity
        {
            get
            {
                var fieldItem = this.fields.FirstOrDefault(f => f.name.Equals("quantity", StringComparison.OrdinalIgnoreCase));
                return (fieldItem != null) ? int.Parse(fieldItem.value) : 0;
            }
        }

        // This is the person doing the action (purchase or gifter)
        public string Username
        {
            get
            {
                var fieldItem = this.fields.FirstOrDefault(f => f.name.Equals("username", StringComparison.OrdinalIgnoreCase));
                return (fieldItem != null) ? fieldItem.value : string.Empty;
            }
        }
    }

    public class StreamlootsCardModel
    {
        public string type { get; set; }
        public string imageUrl { get; set; }
        public string videoUrl { get; set; }
        public string soundUrl { get; set; }
        public string message { get; set; }
        public StreamlootsCardDataModel data { get; set; }
    }

    public class StreamlootsCardDataModel
    {
        public string cardName { get; set; }
        public string description { get; set; }
        public List<StreamlootsDataFieldModel> fields { get; set; }

        public string Message
        {
            get
            {
                StreamlootsDataFieldModel fieldItem = this.fields.FirstOrDefault(f => f.name.Equals("message", StringComparison.OrdinalIgnoreCase));
                return (fieldItem != null) ? fieldItem.value : string.Empty;
            }
        }

        public string LongMessage
        {
            get
            {
                StreamlootsDataFieldModel fieldItem = this.fields.FirstOrDefault(f => f.name.Equals("longmessage", StringComparison.OrdinalIgnoreCase));
                return (fieldItem != null) ? fieldItem.value : string.Empty;
            }
        }

        public string Rarity
        {
            get
            {
                StreamlootsDataFieldModel fieldItem = this.fields.FirstOrDefault(f => f.name.Equals("rarity", StringComparison.OrdinalIgnoreCase));
                return (fieldItem != null) ? fieldItem.value : string.Empty;
            }
        }

        public string Username
        {
            get
            {
                StreamlootsDataFieldModel fieldItem = this.fields.FirstOrDefault(f => f.name.Equals("username", StringComparison.OrdinalIgnoreCase));
                return (fieldItem != null) ? fieldItem.value : string.Empty;
            }
        }
    }

    public class StreamlootsDataFieldModel
    {
        public string name { get; set; }
        public string value { get; set; }
    }

    public class StreamlootsService : OAuthExternalServiceBase
    {
        public static event EventHandler<Tuple<UserV2ViewModel, int>> OnStreamlootsPurchaseOccurred = delegate { };
        public static void StreamlootsPurchaseOccurred(UserV2ViewModel user, int amount) { OnStreamlootsPurchaseOccurred(null, new Tuple<UserV2ViewModel, int>(user, amount)); }

        public event EventHandler OnStreamlootsConnectionChanged = delegate { };

        private HttpClient httpClient;
        private CancellationTokenSource cancellationTokenSource;

        public StreamlootsService() : base("") { }

        public override string Name { get { return MixItUp.Base.Resources.Streamloots; } }

        public override Task<Result> Connect()
        {
            return Task.FromResult(new Result(false));
        }

        public override async Task Disconnect()
        {
            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel();
            }

            this.token = null;

            if (httpClient != null)
            {
                httpClient.Dispose();
                httpClient = null;
            }

            this.OnStreamlootsConnectionChanged(this, new EventArgs());
        }

        protected override Task<Result> InitializeInternal()
        {
            cancellationTokenSource = new CancellationTokenSource();
            httpClient = new HttpClient(HttpConnectionFactory.CreateHandler())
            {
                Timeout = Timeout.InfiniteTimeSpan
            };

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            Task.Run(() => BackgroundCheck(cancellationTokenSource.Token), cancellationTokenSource.Token);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

            this.TrackServiceTelemetry("Streamloots");

            this.OnStreamlootsConnectionChanged(this, new EventArgs());

            return Task.FromResult(new Result());
        }

        protected override void ClearPersistedCredentials()
        {
            if (ChannelSession.Settings != null)
            {
                ChannelSession.Settings.StreamlootsOAuthToken = null;
            }
        }

        protected override Task RefreshOAuthToken() { return Task.CompletedTask; }

        protected override void DisposeInternal()
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
            httpClient?.Dispose();
        }

        private async Task BackgroundCheck(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    string url = string.Format("https://widgets.streamloots.com/alerts/{0}/media-stream", this.token.accessToken);

                    using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                    {
                        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

                        using (HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                        {
                            response.EnsureSuccessStatusCode();

                            await using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken))
                            {
                                await foreach (SseItem<string> sseItem in SseParser.Create(stream).EnumerateAsync(cancellationToken))
                                {
                                    if (cancellationToken.IsCancellationRequested)
                                        break;

                                    if (sseItem.EventType == "message" && !string.IsNullOrEmpty(sseItem.Data))
                                    {
                                        try
                                        {
                                            Logger.Log(LogLevel.Debug, "Streamloots Packet Received: " + sseItem.Data);

                                            JObject jobj = JObject.Parse(sseItem.Data);
                                            if (jobj != null && jobj.ContainsKey("data"))
                                            {
                                                Logger.Log(LogLevel.Debug, "Streamloots Full Packet Received: " + sseItem.Data);

                                                JObject dataObj = jobj.Value<JObject>("data");
                                                if (dataObj != null && dataObj.ContainsKey("type"))
                                                {
                                                    var type = dataObj.Value<string>("type");
                                                    switch (type?.ToLower())
                                                    {
                                                        case "purchase":
                                                            await ProcessPurchase(jobj);
                                                            break;
                                                        case "redemption":
                                                            await ProcessCardRedemption(jobj);
                                                            break;
                                                        case "random-community-gift":
                                                            await ProcessCommunityGift(jobj);
                                                            break;
                                                        default:
                                                            Logger.Log(LogLevel.Debug, $"Unknown Streamloots packet type: {type}");
                                                            break;
                                                    }
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
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);

                    if (!cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(10000, cancellationToken);
                    }
                }
            }
        }

        private async Task ProcessPurchase(JObject jobj)
        {
            var purchase = jobj["data"].ToObject<StreamlootsPurchaseDataModel>();
            if (purchase != null)
            {
                UserV2ViewModel user = this.GetUser(purchase.Username);
                UserV2ViewModel giftee = (string.IsNullOrEmpty(purchase.Giftee)) ? null : this.GetUser(purchase.Giftee);

                CommandParametersModel parameters = new CommandParametersModel(user);
                parameters.SpecialIdentifiers["streamlootspurchasequantity"] = purchase.Quantity.ToString();
                if (giftee != null)
                {
                    parameters.Arguments.Add(giftee.Username);
                    await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.StreamlootsPackGifted, parameters);
                }
                else
                {
                    await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.StreamlootsPackPurchased, parameters);
                }

                StreamlootsService.StreamlootsPurchaseOccurred(user, purchase.Quantity);
                EventService.StreamlootsOccurred(user, purchase.Quantity);

                if (giftee != null)
                {
                    await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format(MixItUp.Base.Resources.StreamlootsGiftedPacksAlert, user.FullDisplayName, purchase.Quantity, giftee.Username), ChannelSession.Settings.AlertStreamlootsColor));
                }
                else
                {
                    await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format(MixItUp.Base.Resources.StreamlootsPurchasedPacksAlert, user.FullDisplayName, purchase.Quantity), ChannelSession.Settings.AlertStreamlootsColor));
                }
            }
        }

        private async Task ProcessCommunityGift(JObject jobj)
        {
            var purchase = jobj["data"].ToObject<StreamlootsPurchaseDataModel>();
            if (purchase != null)
            {
                UserV2ViewModel user = this.GetUser(purchase.Username);

                CommandParametersModel parameters = new CommandParametersModel(user);
                parameters.SpecialIdentifiers["streamlootspurchasequantity"] = purchase.Quantity.ToString();

                await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.StreamlootsPackCommunityGifted, parameters);

                StreamlootsService.StreamlootsPurchaseOccurred(user, purchase.Quantity);
                EventService.StreamlootsOccurred(user, purchase.Quantity);

                await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format(MixItUp.Base.Resources.StreamlootsCommunityGiftedPacksAlert, user.FullDisplayName, purchase.Quantity), ChannelSession.Settings.AlertStreamlootsColor));
            }
        }

        private async Task ProcessCardRedemption(JObject jobj)
        {
            StreamlootsCardModel card = jobj.ToObject<StreamlootsCardModel>();
            if (card != null && !string.IsNullOrEmpty(card.data?.cardName))
            {
                UserV2ViewModel user = this.GetUser(card.data.Username);

                Dictionary<string, string> specialIdentifiers = new Dictionary<string, string>();
                specialIdentifiers["streamlootscardname"] = card.data.cardName;
                specialIdentifiers["streamlootscarddescription"] = card.data.description;
                specialIdentifiers["streamlootscardimage"] = card.imageUrl;
                specialIdentifiers["streamlootscardhasvideo"] = (!string.IsNullOrEmpty(card.videoUrl)).ToString();
                specialIdentifiers["streamlootscardvideo"] = card.videoUrl;
                specialIdentifiers["streamlootscardsound"] = card.soundUrl;
                specialIdentifiers["streamlootscardrarity"] = card.data.Rarity;
                specialIdentifiers["streamlootscardalertmessage"] = card.message;

                string message = card.data.Message;
                if (string.IsNullOrEmpty(message))
                {
                    message = card.data.LongMessage;
                }
                specialIdentifiers["streamlootsmessage"] = message;

                List<string> arguments = new List<string>();
                if (!string.IsNullOrEmpty(message))
                {
                    arguments = new List<string>(message.Split(' '));
                }

                await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.StreamlootsCardRedeemed, new CommandParametersModel(user, arguments, specialIdentifiers));

                EventService.StreamlootsOccurred(user, 1);

                StreamlootsCardCommandModel command = ServiceManager.Get<CommandService>().StreamlootsCardCommands.FirstOrDefault(c => string.Equals(c.Name, card.data.cardName, StringComparison.CurrentCultureIgnoreCase));
                if (command != null)
                {
                    Dictionary<string, string> cardsCommandSpecialIdentifiers = new Dictionary<string, string>(specialIdentifiers);
                    await ServiceManager.Get<CommandService>().Queue(command, new CommandParametersModel(user, platform: user.Platform, arguments: arguments, specialIdentifiers: cardsCommandSpecialIdentifiers));
                }

                await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format(MixItUp.Base.Resources.StreamlootsRedeemedCardAlert, user.FullDisplayName, card.data.cardName), ChannelSession.Settings.AlertStreamlootsColor));
            }
        }

        private UserV2ViewModel GetUser(string username)
        {
            UserV2ViewModel user = ServiceManager.Get<UserService>().GetActiveUserByPlatform(StreamingPlatformTypeEnum.All, platformUsername: username);
            if (user == null)
            {
                user = UserV2ViewModel.CreateUnassociated(username);
            }
            return user;
        }
    }
}

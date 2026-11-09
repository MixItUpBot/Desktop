using MixItUp.Base.Model.Web;
using MixItUp.Base.Util;
using MixItUp.Base.Web;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace MixItUp.Base.Services.External
{
    [DataContract]
    public class TTSMonsterAPIVoiceModel
    {
        [DataMember]
        public string voice_id { get; set; }
        [DataMember]
        public string name { get; set; }
        [DataMember]
        public string sample { get; set; }
        [DataMember]
        public string metadata { get; set; }
        [DataMember]
        public string language { get; set; }
    }

    [DataContract]
    public class TTSMonsterAPIGetVoicesResponseModel
    {
        [DataMember]
        public List<TTSMonsterAPIVoiceModel> voices { get; set; } = new List<TTSMonsterAPIVoiceModel>();
        [DataMember]
        public List<TTSMonsterAPIVoiceModel> customVoices { get; set; } = new List<TTSMonsterAPIVoiceModel>();
    }

    [DataContract]
    public class TTSMonsterAPIGenerateTTSRequestModel
    {
        [DataMember]
        public string voice_id { get; set; }
        [DataMember]
        public string message { get; set; }

        public TTSMonsterAPIGenerateTTSRequestModel(string voiceID, string message)
        {
            this.voice_id = voiceID;
            this.message = message;
        }
    }

    [DataContract]
    public class TTSMonsterAPIGenerateTTSResponseModel
    {
        [DataMember]
        public string url { get; set; }
    }

    public class TTSMonsterAPIService : OAuthExternalServiceBase, ITTSMonsterAPIService
    {
        private static readonly HashSet<string> BlockedVoices = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "diana", "dagoth", "tate", "gordon", "megan" };

        public TextToSpeechProviderType ProviderType { get { return TextToSpeechProviderType.TTSMonsterAPI; } }

        public int VolumeMinimum { get { return 0; } }
        public int VolumeMaximum { get { return 100; } }
        public int VolumeDefault { get { return 100; } }

        public int PitchMinimum { get { return 0; } }
        public int PitchMaximum { get { return 0; } }
        public int PitchDefault { get { return 0; } }

        public int RateMinimum { get { return 0; } }
        public int RateMaximum { get { return 0; } }
        public int RateDefault { get { return 0; } }

        public override string Name { get { return Resources.TTSMonsterAPI; } }

        private List<TextToSpeechVoice> voicesCache = new List<TextToSpeechVoice>();

        public TTSMonsterAPIService() : base("https://api.console.tts.monster/") { }

        public override Task<Result> Connect()
        {
            return Task.FromResult(new Result(false));
        }

        public override Task Disconnect() { return Task.CompletedTask; }

        protected override async Task<Result> InitializeInternal()
        {
            try
            {
                this.voicesCache.Clear();
                using (AdvancedHttpClient client = this.GetHttpClient())
                {
                    TTSMonsterAPIGetVoicesResponseModel response = await client.PostAsync<TTSMonsterAPIGetVoicesResponseModel>("voices", AdvancedHttpClient.CreateContentFromString("{}"));
                    if (response != null)
                    {
                        if (response.customVoices != null)
                        {
                            foreach (TTSMonsterAPIVoiceModel voice in response.customVoices)
                            {
                                if (!string.IsNullOrEmpty(voice?.voice_id) && !string.IsNullOrEmpty(voice?.name))
                                {
                                    this.voicesCache.Add(new TextToSpeechVoice(voice.voice_id, voice.name));
                                }
                            }
                        }

                        if (response.voices != null)
                        {
                            foreach (TTSMonsterAPIVoiceModel voice in response.voices)
                            {
                                if (!string.IsNullOrEmpty(voice?.voice_id) && !string.IsNullOrEmpty(voice?.name) && !TTSMonsterAPIService.BlockedVoices.Contains(voice.name))
                                {
                                    this.voicesCache.Add(new TextToSpeechVoice(voice.voice_id, voice.name));
                                }
                            }
                        }

                        if (this.voicesCache.Count > 0)
                        {
                            return new Result();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            return new Result(Resources.TTSMonsterAPIFailedToGetVoices);
        }

        protected override void ClearPersistedCredentials()
        {
            if (ChannelSession.Settings != null)
            {
                ChannelSession.Settings.TTSMonsterAPIOAuthToken = null;
            }
        }

        protected override Task RefreshOAuthToken() { return Task.CompletedTask; }

        public override OAuthTokenModel GetOAuthTokenCopy()
        {
            if (this.token != null)
            {
                return new OAuthTokenModel()
                {
                    accessToken = this.token.accessToken,
                };
            }
            return null;
        }

        public IEnumerable<TextToSpeechVoice> GetVoices() { return this.voicesCache; }

        public async Task Speak(string outputDevice, Guid overlayEndpointID, string text, string voice, int volume, int pitch, int rate, bool ssml, bool waitForFinish)
        {
            try
            {
                using (AdvancedHttpClient client = this.GetHttpClient())
                {
                    client.Timeout = new TimeSpan(0, 0, 30);

                    TTSMonsterAPIGenerateTTSResponseModel response = await client.PostAsync<TTSMonsterAPIGenerateTTSResponseModel>("generate", AdvancedHttpClient.CreateContentFromObject(new TTSMonsterAPIGenerateTTSRequestModel(voice, text)));
                    if (response != null && !string.IsNullOrEmpty(response.url))
                    {
                        string filename = response.url.Split(new char[] { '?' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                        filename = filename.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                        using (HttpClient httpClient = HttpConnectionFactory.CreateClient())
                        {
                            string filePath = Path.Combine(ServiceManager.Get<IFileService>().GetTempFolder(), filename);
                            byte[] fileBytes = await httpClient.GetByteArrayAsync(response.url);
                            await File.WriteAllBytesAsync(filePath, fileBytes);
                            await ServiceManager.Get<IAudioService>().Play(filePath, volume, outputDevice, waitForFinish: waitForFinish);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        private AdvancedHttpClient GetHttpClient()
        {
            AdvancedHttpClient client = new AdvancedHttpClient(this.baseAddress);
            client.AddHeader("Authorization", this.token?.accessToken);
            return client;
        }
    }
}

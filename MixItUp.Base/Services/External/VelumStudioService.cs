using MixItUp.Base.Model.Actions;
using MixItUp.Base.Util;
using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Velum.Studio;

namespace MixItUp.Base.Services.External
{
    public class VelumStudioService : ServiceBase
    {
        private readonly SemaphoreSlim connectionLock = new SemaphoreSlim(1, 1);
        private VelumStudioClient client;

        public override string Name => Resources.VelumStudio;
        public override bool IsEnabled => ChannelSession.Settings.VelumStudioEnabled;
        public override bool IsConnected { get => this.client?.IsConnected == true; protected set { } }

        public override async Task<Result> ManualConnect(CancellationToken cancellationToken)
        {
            await this.connectionLock.WaitAsync(cancellationToken);
            try
            {
                if (this.IsConnected) { return new Result(); }
                if (this.client != null) { await this.client.DisposeAsync(); this.client = null; }
                VelumClientOptions options = new VelumClientOptions()
                {
                    ClientName = "Mix It Up",
                    RequestTimeout = TimeSpan.FromSeconds(30),
                };
                string address = ChannelSession.Settings.VelumStudioWebSocketAddress?.Trim();
                if (!string.IsNullOrEmpty(address))
                {
                    if (!Uri.TryCreate(address, UriKind.Absolute, out Uri endpoint)
                        || (endpoint.Scheme != "ws" && endpoint.Scheme != "wss")
                        || !string.IsNullOrEmpty(endpoint.UserInfo) || !string.IsNullOrEmpty(endpoint.Query)
                        || !string.IsNullOrEmpty(endpoint.Fragment))
                    {
                        return new Result(Resources.VelumStudioInvalidConnection);
                    }
                    string fingerprint = ChannelSession.Settings.VelumStudioCertificateFingerprint?.Replace(":", string.Empty).Replace(" ", string.Empty).Trim();
                    if (!string.IsNullOrEmpty(fingerprint) && !Pairing.IsFingerprint(fingerprint))
                    {
                        return new Result(Resources.VelumStudioInvalidConnection);
                    }
                    options.Url = endpoint;
                    options.Token = string.IsNullOrWhiteSpace(ChannelSession.Settings.VelumStudioToken) ? null : ChannelSession.Settings.VelumStudioToken.Trim();
                    options.Fingerprint = string.IsNullOrEmpty(fingerprint) ? null : fingerprint;
                    if (endpoint.IsLoopback && string.IsNullOrEmpty(options.Token))
                    {
                        options.TokenProvider = () => StudioDiscovery.FindRunning().FirstOrDefault(instance => instance.Port == endpoint.Port)?.Token;
                    }
                }
                this.client = await VelumStudioClient.ConnectAsync(options, cancellationToken);
                await this.client.SubscribeAsync(new[] { TopicType.Scenes, TopicType.Layers, TopicType.Sources, TopicType.Audio, TopicType.Stream, TopicType.Record, TopicType.Replay }, cancellationToken);
                return new Result();
            }
            catch (Exception ex)
            {
                if (this.client != null) { await this.client.DisposeAsync(); this.client = null; }
                Logger.Log(ex);
                return new Result(ex);
            }
            finally { this.connectionLock.Release(); }
        }

        public override async Task Disconnect()
        {
            await this.connectionLock.WaitAsync();
            try
            {
                if (this.client != null) { await this.client.DisposeAsync(); this.client = null; }
            }
            finally { this.connectionLock.Release(); }
        }

        public override async Task Disable()
        {
            ChannelSession.Settings.VelumStudioEnabled = false;
            await this.Disconnect();
        }

        public static bool SupportsAction(StreamingSoftwareActionTypeEnum type)
        {
            return type == StreamingSoftwareActionTypeEnum.Scene || type == StreamingSoftwareActionTypeEnum.SourceVisibility
                || type == StreamingSoftwareActionTypeEnum.ToggleSourceVisibility || type == StreamingSoftwareActionTypeEnum.TextSource
                || type == StreamingSoftwareActionTypeEnum.SourceFilterVisibility || type == StreamingSoftwareActionTypeEnum.SceneCollection
                || type == StreamingSoftwareActionTypeEnum.StartStopStream || type == StreamingSoftwareActionTypeEnum.StartStopRecording
                || type == StreamingSoftwareActionTypeEnum.SaveReplayBuffer || type == StreamingSoftwareActionTypeEnum.MuteAudioSource
                || type == StreamingSoftwareActionTypeEnum.UnmuteAudioSource || type == StreamingSoftwareActionTypeEnum.ToggleAudioMute;
        }

        public async Task PerformAction(StreamingSoftwareActionTypeEnum type, string name, string parent, bool visible, string text)
        {
            if (!SupportsAction(type)) { throw new NotSupportedException(Resources.NotSupportedStreamingSoftware); }
            if (!this.IsEnabled) { throw new InvalidOperationException(Resources.ServiceConnectFailed); }
            if (!this.IsConnected)
            {
                Result result = await this.AutomaticConnect();
                if (!result.Success) { throw new InvalidOperationException(result.Message); }
            }
            string method;
            JsonObject args = new JsonObject();
            switch (type)
            {
                case StreamingSoftwareActionTypeEnum.Scene:
                    method = "scene.switch"; args["scene"] = name; break;
                case StreamingSoftwareActionTypeEnum.SourceVisibility:
                case StreamingSoftwareActionTypeEnum.ToggleSourceVisibility:
                    method = "layer.visibility";
                    args["layer"] = name;
                    if (!string.IsNullOrWhiteSpace(parent)) { args["scene"] = parent; }
                    args["mode"] = type == StreamingSoftwareActionTypeEnum.ToggleSourceVisibility ? "toggle" : visible ? "show" : "hide";
                    break;
                case StreamingSoftwareActionTypeEnum.TextSource:
                    await this.client.RunAsync("text.set", new JsonObject { ["source"] = name, ["text"] = text ?? string.Empty });
                    await this.PerformAction(StreamingSoftwareActionTypeEnum.SourceVisibility, name, parent, visible, null);
                    return;
                case StreamingSoftwareActionTypeEnum.SourceFilterVisibility:
                    method = "filter.toggle"; args["filter"] = name; args["owner"] = parent; args["mode"] = visible ? "on" : "off"; break;
                case StreamingSoftwareActionTypeEnum.SceneCollection:
                    method = "collection.switch"; args["collection"] = name; break;
                case StreamingSoftwareActionTypeEnum.StartStopRecording:
                    method = "record.toggle"; break;
                case StreamingSoftwareActionTypeEnum.SaveReplayBuffer:
                    method = "replay.save"; break;
                case StreamingSoftwareActionTypeEnum.StartStopStream:
                    JsonElement streamState = await this.client.CallAsync("state.get", new JsonObject { ["topics"] = new JsonArray("stream") });
                    method = streamState.GetProperty("stream").GetProperty("live").GetBoolean() ? "stream.stop" : "stream.golive";
                    if (method == "stream.stop") { args["confirm"] = true; }
                    break;
                default:
                    method = "audio.mute"; args["input"] = name;
                    args["mode"] = type == StreamingSoftwareActionTypeEnum.ToggleAudioMute ? "toggle" : type == StreamingSoftwareActionTypeEnum.MuteAudioSource ? "mute" : "unmute";
                    break;
            }
            await this.client.RunAsync(method, args);
        }
    }
}

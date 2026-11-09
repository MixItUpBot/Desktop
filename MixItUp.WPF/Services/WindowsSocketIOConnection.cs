using MixItUp.Base.Services;
using MixItUp.Base.Util;
using MixItUp.Base.Web;
using Newtonsoft.Json.Linq;
using SocketIOClient;
using SocketIOClient.Transport;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace MixItUp.WPF.Services
{
    public class WindowsSocketIOConnection : ISocketIOConnection
    {
        public event EventHandler OnConnected = delegate { };
        public event EventHandler OnDisconnected;

        private SocketIOClient.SocketIO socket;
        private Action<string> disconnectListener;
        private readonly Dictionary<string, Action<SocketIOResponse>> listeners = new Dictionary<string, Action<SocketIOResponse>>();

        public async Task Connect(string connectionURL)
        {
            await this.Disconnect();
            var uri = new Uri(connectionURL);
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            var options = new SocketIOOptions
            {
                EIO = SocketIO.Core.EngineIO.V3,
                Transport = TransportProtocol.WebSocket,
                // Fail initial connect promptly; reconnect ownership is selected on disconnect.
                Reconnection = false,
                Path = uri.AbsolutePath.TrimEnd('/') + "/socket.io",
                Query = query.AllKeys.Where(key => key != null).Select(key => new KeyValuePair<string, string>(key, query[key])).ToArray(),
            };
            var client = new SocketIOClient.SocketIO(uri.GetLeftPart(UriPartial.Authority), options);
            client.ClientWebSocketProvider = () => new SocketIOWebSocket(options.RemoteCertificateValidationCallback);
            client.OnConnected += this.SocketConnected;
            client.OnDisconnected += this.SocketDisconnected;
            client.OnError += this.SocketError;
            foreach (var listener in this.listeners) client.On(listener.Key, listener.Value);
            this.socket = client;
            try { await client.ConnectAsync(); }
            catch
            {
                if (ReferenceEquals(this.socket, client)) await this.Disconnect();
                throw;
            }
        }

        public async Task Disconnect()
        {
            var client = this.socket;
            this.socket = null;
            if (client == null) return;
            client.Options.Reconnection = false;
            client.OnConnected -= this.SocketConnected;
            client.OnDisconnected -= this.SocketDisconnected;
            client.OnError -= this.SocketError;
            try { if (client.Connected) await client.DisconnectAsync(); }
            catch (Exception ex) { Logger.Log(ex); }
            finally { client.Dispose(); }
        }

        public void Listen(string eventString, Action processEvent)
        {
            if (eventString == "disconnect") this.disconnectListener = _ => processEvent();
            this.Register(eventString, _ => processEvent());
        }

        public void Listen(string eventString, Action<object> processEvent)
        {
            if (eventString == "disconnect") this.disconnectListener = reason => processEvent(reason);
            this.Register(eventString, response =>
            {
                // Keep the existing Newtonsoft payload contract used by service listeners.
                processEvent(JToken.Parse(response.GetValue<JsonElement>().GetRawText()));
            });
        }

        private void Register(string eventString, Action<SocketIOResponse> callback)
        {
            Action<SocketIOResponse> guarded = response =>
            {
                try { callback(response); }
                catch (Exception ex) { Logger.Log(ex); }
            };
            this.listeners[eventString] = guarded;
            this.socket?.On(eventString, guarded);
        }

        public void Remove(string eventString)
        {
            if (eventString == "disconnect") this.disconnectListener = null;
            this.listeners.Remove(eventString);
            this.socket?.Off(eventString);
        }

        public async void Send(string eventString, object data)
        {
            try
            {
                var client = this.socket;
                if (client == null || !client.Connected) return;
                // JObject/JArray must be converted before the SDK's System.Text.Json serializer.
                using var document = JsonDocument.Parse(JSONSerializerHelper.SerializeToString(data));
                await client.EmitAsync(eventString, document.RootElement.Clone());
            }
            catch (Exception ex) { Logger.Log(ex); }
        }

        private void SocketConnected(object sender, EventArgs args) => this.OnConnected(this, EventArgs.Empty);
        private void SocketDisconnected(object sender, string reason)
        {
            if (sender is SocketIOClient.SocketIO client && ReferenceEquals(client, this.socket))
            {
                // Service handlers can refresh credentials and reconnect themselves. If there is
                // no owner, retain automatic reconnection, using our provider for every socket.
                client.Options.Reconnection = this.OnDisconnected == null && this.disconnectListener == null;
                this.OnDisconnected?.Invoke(this, EventArgs.Empty);
                try { this.disconnectListener?.Invoke(reason); }
                catch (Exception ex) { Logger.Log(ex); }
            }
        }
        private void SocketError(object sender, string error) => Logger.Log(LogLevel.Error, "Socket Error Received: " + error);
    }
}

using SocketIOClient.Transport;
using SocketIOClient.Transport.WebSockets;
using System;
using System.Net;
using System.Net.Security;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Base.Web
{
    public sealed class SocketIOWebSocket : IClientWebSocket
    {
        private readonly ClientWebSocket socket = new ClientWebSocket();

        public SocketIOWebSocket(RemoteCertificateValidationCallback certificateValidation = null)
        {
            this.socket.Options.RemoteCertificateValidationCallback = certificateValidation;
        }

        public SocketIOClient.Transport.WebSockets.WebSocketState State => (SocketIOClient.Transport.WebSockets.WebSocketState)this.socket.State;
        public Task ConnectAsync(Uri uri, CancellationToken cancellationToken) => WebSocketConnectionFactory.ConnectAsync(this.socket, uri, cancellationToken);
        public Task DisconnectAsync(CancellationToken cancellationToken) => this.socket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, cancellationToken);
        public Task SendAsync(byte[] bytes, TransportMessageType type, bool endOfMessage, CancellationToken cancellationToken) =>
            this.socket.SendAsync(new ArraySegment<byte>(bytes), type == TransportMessageType.Binary ? WebSocketMessageType.Binary : WebSocketMessageType.Text, endOfMessage, cancellationToken);

        public async Task<SocketIOClient.Transport.WebSockets.WebSocketReceiveResult> ReceiveAsync(int bufferSize, CancellationToken cancellationToken)
        {
            var buffer = new byte[bufferSize];
            var result = await this.socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);
            return new SocketIOClient.Transport.WebSockets.WebSocketReceiveResult
            {
                Buffer = buffer, Count = result.Count, EndOfMessage = result.EndOfMessage,
                MessageType = (TransportMessageType)result.MessageType,
            };
        }

        public void AddHeader(string key, string val) => this.socket.Options.SetRequestHeader(key, val);
        public void SetProxy(IWebProxy proxy) => this.socket.Options.Proxy = proxy;
        public void Dispose() => this.socket.Dispose();
    }
}

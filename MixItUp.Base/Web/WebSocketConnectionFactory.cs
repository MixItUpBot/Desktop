using System;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Base.Web
{
    public static class WebSocketConnectionFactory
    {
        // ClientWebSocket's default proxy is a private sentinel; preserve its meaning without
        // handing that sentinel to SocketsHttpHandler as an actual proxy implementation.
        private static readonly IWebProxy DefaultProxy = GetDefaultProxy();

        private static IWebProxy GetDefaultProxy()
        {
            using var socket = new ClientWebSocket();
            return socket.Options.Proxy;
        }

        public static async Task ConnectAsync(ClientWebSocket socket, Uri uri, CancellationToken cancellationToken)
        {
            using var handler = CreateHandler(socket.Options);
            using var invoker = new HttpMessageInvoker(handler, disposeHandler: false);
            await socket.ConnectAsync(uri, invoker, cancellationToken).ConfigureAwait(false);
            // An upgraded WebSocket owns its stream independently of the HTTP handler.
        }

        internal static SocketsHttpHandler CreateHandler(ClientWebSocketOptions options)
        {
            var handler = HttpConnectionFactory.CreateHandler();
            handler.PooledConnectionLifetime = TimeSpan.Zero;
            handler.UseProxy = options.Proxy != null;
            if (options.Proxy != null && !ReferenceEquals(options.Proxy, DefaultProxy))
            {
                handler.Proxy = options.Proxy;
            }
            handler.UseCookies = options.Cookies != null;
            if (options.Cookies != null)
            {
                handler.CookieContainer = options.Cookies;
            }
            handler.Credentials = options.UseDefaultCredentials ? CredentialCache.DefaultCredentials : options.Credentials;
            handler.SslOptions.RemoteCertificateValidationCallback = options.RemoteCertificateValidationCallback;
            if (options.ClientCertificates.Count > 0)
            {
                handler.SslOptions.ClientCertificates = new X509CertificateCollection(options.ClientCertificates);
            }

            // Transport options belong to the supplied invoker. Headers, subprotocols,
            // compression and keepalive remain on the ClientWebSocket.
            options.Proxy = null;
            options.Cookies = null;
            options.Credentials = null;
            options.UseDefaultCredentials = false;
            options.RemoteCertificateValidationCallback = null;
            options.ClientCertificates.Clear();
            return handler;
        }
    }
}

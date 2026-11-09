using Amazon.Runtime;
using MixItUp.Base.Web;
using System.Net.Http;

namespace MixItUp.WPF.Services
{
    internal sealed class AmazonHttpConnectionFactory : HttpClientFactory
    {
        public override HttpClient CreateHttpClient(IClientConfig clientConfig)
        {
            // Retain the AWS SDK's transport settings and let it own signing, retries and client caching.
            SocketsHttpHandler handler = HttpConnectionFactory.CreateHandler(clientConfig.AllowAutoRedirect);
            if (clientConfig.MaxConnectionsPerServer.HasValue)
            {
                handler.MaxConnectionsPerServer = clientConfig.MaxConnectionsPerServer.Value;
            }
            handler.Proxy = clientConfig.GetWebProxy();
            if (handler.Proxy != null && clientConfig.ProxyCredentials != null)
            {
                handler.Proxy.Credentials = clientConfig.ProxyCredentials;
            }
            var client = new HttpClient(handler);
            if (clientConfig.Timeout.HasValue)
            {
                client.Timeout = clientConfig.Timeout.Value;
            }
            return client;
        }
    }
}

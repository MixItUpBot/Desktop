using Google.Apis.Http;
using System.Net;
using System.Net.Http;

namespace MixItUp.Base.Web
{
    /// <summary>Uses Desktop's connection fallback for YouTube API and OAuth requests.</summary>
    internal sealed class GoogleHttpConnectionFactory : HttpClientFactory
    {
        protected override HttpMessageHandler CreateHandler(CreateHttpClientArgs args)
        {
            // The SDK's ConfigurableMessageHandler still owns authentication, retries and redirects.
            // YouTube uses JSON API responses, with the same gzip/deflate support as the SDK default.
            SocketsHttpHandler handler = HttpConnectionFactory.CreateHandler(allowAutoRedirect: false);
            handler.AutomaticDecompression = args.GZipEnabled ? DecompressionMethods.GZip | DecompressionMethods.Deflate : DecompressionMethods.None;
            return handler;
        }
    }
}

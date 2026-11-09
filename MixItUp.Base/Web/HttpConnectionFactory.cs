using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Base.Web
{
    /// <summary>
    /// Opens HTTP connections without letting an unreachable address family consume the request timeout.
    /// </summary>
    public static class HttpConnectionFactory
    {
        internal static readonly TimeSpan ConnectionAttemptDelay = TimeSpan.FromMilliseconds(250);

        public static HttpClient CreateClient() => new HttpClient(CreateHandler());

        public static SocketsHttpHandler CreateHandler(bool allowAutoRedirect = true)
        {
            return new SocketsHttpHandler
            {
                AllowAutoRedirect = allowAutoRedirect,
                // Leave proxy selection, TLS (including SNI/certificate checks), and pooling to HttpClient.
                ConnectCallback = async (context, cancellationToken) =>
                {
                    DnsEndPoint endpoint = context.DnsEndPoint;
                    IPAddress[] addresses = await ConnectionDnsResolver.ResolveAsync(endpoint.Host, endpoint.AddressFamily, cancellationToken).ConfigureAwait(false);
                    return await ConnectAsync(addresses, endpoint.Port, ConnectAddressAsync, ConnectionAttemptDelay, cancellationToken).ConfigureAwait(false);
                },
            };
        }

        internal static async Task<Stream> ConnectAsync(IEnumerable<IPAddress> addresses, int port,
            Func<IPAddress, int, CancellationToken, Task<Stream>> connect, TimeSpan attemptDelay, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IPAddress[] ordered = InterleaveAddresses(addresses);
            if (ordered.Length == 0)
            {
                throw new SocketException((int)SocketError.HostNotFound);
            }

            using CancellationTokenSource attemptsCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var pending = new List<Task<Stream>>();
            Exception lastError = null;
            Task nextAttempt = Task.CompletedTask;
            int index = 0;
            try
            {
                while (index < ordered.Length || pending.Count > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (index < ordered.Length && (pending.Count == 0 || nextAttempt.IsCompleted))
                    {
                        pending.Add(connect(ordered[index++], port, attemptsCancellation.Token));
                        nextAttempt = Task.Delay(attemptDelay, attemptsCancellation.Token);
                    }

                    var completions = new List<Task>(pending);
                    if (index < ordered.Length)
                    {
                        completions.Add(nextAttempt);
                    }
                    Task completed = await Task.WhenAny(completions).WaitAsync(cancellationToken).ConfigureAwait(false);
                    if (completed == nextAttempt)
                    {
                        continue;
                    }

                    var attempt = (Task<Stream>)completed;
                    pending.Remove(attempt);
                    try
                    {
                        Stream connection = await attempt.ConfigureAwait(false);
                        if (cancellationToken.IsCancellationRequested)
                        {
                            connection.Dispose();
                            cancellationToken.ThrowIfCancellationRequested();
                        }
                        return connection;
                    }
                    catch (Exception ex) when (ex is SocketException || ex is IOException || ex is OperationCanceledException)
                    {
                        lastError = ex;
                        // A definite failure need not wait out the stagger before trying another address.
                        nextAttempt = Task.CompletedTask;
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                ExceptionDispatchInfo.Capture(lastError).Throw();
                throw new InvalidOperationException("No connection attempt completed.");
            }
            finally
            {
                attemptsCancellation.Cancel();
                foreach (Task<Stream> attempt in pending)
                {
                    // A losing connect may finish at the same time as the winner or cancellation.
                    // Observe failures and close late successes without delaying the winning connection.
                    _ = DisposeUnusedConnectionAsync(attempt);
                }
            }
        }

        private static IPAddress[] InterleaveAddresses(IEnumerable<IPAddress> addresses)
        {
            IPAddress[] usable = addresses.Where(a => a.AddressFamily == AddressFamily.InterNetwork || a.AddressFamily == AddressFamily.InterNetworkV6).Distinct().ToArray();
            if (usable.Length == 0)
            {
                return usable;
            }

            // Preserve the OS's preferred family and each family's DNS order, but try the other family next.
            var preferred = new Queue<IPAddress>(usable.Where(a => a.AddressFamily == usable[0].AddressFamily));
            var alternate = new Queue<IPAddress>(usable.Where(a => a.AddressFamily != usable[0].AddressFamily));
            var ordered = new List<IPAddress>(usable.Length);
            while (preferred.Count > 0 || alternate.Count > 0)
            {
                if (preferred.Count > 0)
                {
                    ordered.Add(preferred.Dequeue());
                }
                if (alternate.Count > 0)
                {
                    ordered.Add(alternate.Dequeue());
                }
            }
            return ordered.ToArray();
        }

        private static async Task<Stream> ConnectAddressAsync(IPAddress address, int port, CancellationToken cancellationToken)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, port), cancellationToken).ConfigureAwait(false);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        private static async Task DisposeUnusedConnectionAsync(Task<Stream> attempt)
        {
            try
            {
                (await attempt.ConfigureAwait(false)).Dispose();
            }
            catch
            {
                // Losing connections have already been canceled; observe their errors.
            }
        }
    }
}

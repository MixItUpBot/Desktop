using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Base.Web
{
    internal static class ConnectionDnsResolver
    {
        internal static readonly TimeSpan FallbackDelay = TimeSpan.FromMilliseconds(500);

        public static Task<IPAddress[]> ResolveAsync(string host, AddressFamily family, CancellationToken token) =>
            ResolveAsync(host, family, token, Dns.GetHostAddressesAsync, GetFallbackResolvers, QueryIPv4Async, FallbackDelay);

        internal static async Task<IPAddress[]> ResolveAsync(string host, AddressFamily family, CancellationToken token,
            Func<string, AddressFamily, CancellationToken, Task<IPAddress[]>> systemResolve,
            Func<IPAddress[]> getResolvers, Func<string, IPAddress, CancellationToken, Task<IPAddress[]>> query,
            TimeSpan delay)
        {
            token.ThrowIfCancellationRequested();
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
            Task<IPAddress[]> system = systemResolve(host, family, cancellation.Token);
            var pending = new List<Task<IPAddress[]>> { system };
            try
            {
                // Keep Windows hosts, suffix search, VPN/NRPT and normal negative answers authoritative.
                if (family == AddressFamily.InterNetworkV6 || IPAddress.TryParse(host, out _) ||
                    !host.TrimEnd('.').Contains('.') || host.TrimEnd('.').EndsWith(".local", StringComparison.OrdinalIgnoreCase))
                {
                    return await system.ConfigureAwait(false);
                }

                await Task.WhenAny(system, Task.Delay(delay, cancellation.Token)).WaitAsync(token).ConfigureAwait(false);
                if (system.IsCompletedSuccessfully)
                {
                    return await system.ConfigureAwait(false);
                }
                if (system.IsFaulted && system.Exception.GetBaseException() is SocketException negative &&
                    (negative.SocketErrorCode == SocketError.HostNotFound || negative.SocketErrorCode == SocketError.NoData))
                {
                    return await system.ConfigureAwait(false);
                }
                token.ThrowIfCancellationRequested();

                foreach (var resolver in getResolvers())
                {
                    pending.Add(query(host, resolver, cancellation.Token));
                }
                Exception lastError = null;
                while (pending.Count > 0)
                {
                    var completed = await Task.WhenAny(pending).WaitAsync(token).ConfigureAwait(false);
                    pending.Remove(completed);
                    try
                    {
                        var addresses = await completed.ConfigureAwait(false);
                        if (addresses.Length > 0 || completed == system)
                        {
                            return addresses;
                        }
                    }
                    catch (Exception ex) when (ex is SocketException || ex is IOException || ex is OperationCanceledException)
                    {
                        // A late authoritative Windows NXDOMAIN must not be bypassed either.
                        if (completed == system && ex is SocketException se &&
                            (se.SocketErrorCode == SocketError.HostNotFound || se.SocketErrorCode == SocketError.NoData))
                        {
                            throw;
                        }
                        lastError = ex;
                    }
                }
                token.ThrowIfCancellationRequested();
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(lastError ?? new SocketException((int)SocketError.HostNotFound)).Throw();
                throw new InvalidOperationException();
            }
            finally
            {
                cancellation.Cancel();
                foreach (var task in pending)
                {
                    _ = ObserveAsync(task);
                }
            }
        }

        private static async Task ObserveAsync(Task task)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch
            {
                // Observe errors from requests canceled after another resolver finished.
            }
        }

        private static async Task<IPAddress[]> QueryIPv4Async(string host, IPAddress resolver, CancellationToken token)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            var reply = await ConfiguredDnsQuery.Query(new System.Globalization.IdnMapping().GetAscii(host.TrimEnd('.')), 1, resolver, timeout.Token).ConfigureAwait(false);
            return reply.Addresses.Select(IPAddress.Parse).ToArray();
        }

        private static IPAddress[] GetFallbackResolvers()
        {
            // Never pick a new provider or change Windows settings. Ambiguous/multi-network and
            // managed/encrypted DNS configurations stay with the Windows resolver.
            if (!OperatingSystem.IsWindows())
            {
                return Array.Empty<IPAddress>();
            }
            try
            {
                if (HasDnsPolicy())
                {
                    return Array.Empty<IPAddress>();
                }
                var active = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback).ToArray();
                if (active.Any(n => n.NetworkInterfaceType == NetworkInterfaceType.Tunnel || n.NetworkInterfaceType == NetworkInterfaceType.Ppp))
                {
                    return Array.Empty<IPAddress>();
                }
                var candidates = active.Where(n => n.GetIPProperties().DnsAddresses.Count > 0 &&
                    n.GetIPProperties().GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any))).ToArray();
                if (candidates.Length != 1)
                {
                    return Array.Empty<IPAddress>();
                }
                var adapter = candidates[0];
                // DNS-bearing virtual/VPN interfaces without a default gateway can carry split DNS.
                if (active.Any(n => n.Id != adapter.Id && n.GetIPProperties().DnsAddresses.Count > 0))
                {
                    return Array.Empty<IPAddress>();
                }
                using var encryption = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Dnscache\InterfaceSpecificParameters\" + adapter.Id + @"\DohInterfaceSettings");
                if (encryption != null && (encryption.SubKeyCount > 0 || encryption.ValueCount > 0))
                {
                    return Array.Empty<IPAddress>();
                }
                return adapter.GetIPProperties().DnsAddresses.Where(a => a.AddressFamily == AddressFamily.InterNetwork).Distinct().ToArray();
            }
            catch (Exception ex) when (ex is NetworkInformationException || ex is SecurityException || ex is UnauthorizedAccessException || ex is IOException)
            {
                return Array.Empty<IPAddress>();
            }
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static bool HasDnsPolicy()
        {
            foreach (string path in new[]
            {
                @"SOFTWARE\Policies\Microsoft\Windows NT\DNSClient",
                @"SYSTEM\CurrentControlSet\Services\Dnscache\Parameters\DnsPolicyConfig",
            })
            {
                using var machine = Registry.LocalMachine.OpenSubKey(path);
                using var user = Registry.CurrentUser.OpenSubKey(path);
                if (machine != null && (machine.SubKeyCount > 0 || machine.ValueCount > 0) ||
                    user != null && (user.SubKeyCount > 0 || user.ValueCount > 0))
                {
                    return true;
                }
            }
            using var parameters = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Dnscache\Parameters");
            return Convert.ToInt32(parameters?.GetValue("EnableAutoDoh", 0)) != 0;
        }
    }
}

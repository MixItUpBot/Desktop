#nullable enable
using System;

namespace Velum.Studio;

/// <summary>
/// How to reach Studio. Leave everything as it is to connect to the first
/// Studio running on this computer; name an instance when several run; give
/// a paired device for Studio on another computer.
/// </summary>
public sealed class VelumClientOptions
{
    /// <summary>Your app's name: Studio records the changes you make under it.</summary>
    public string ClientName { get; set; } = "Velum SDK";

    /// <summary>The Studio instance to connect to, by the name Settings › Remote control gives it (several Studios can run at once).</summary>
    public string? InstanceName { get; set; }

    /// <summary>The Studio instance to connect to, by its port.</summary>
    public int? Port { get; set; }

    /// <summary>The discovery file to read; Studio's own place on this system when null (see <see cref="StudioDiscovery.DefaultPath"/>).</summary>
    public string? DiscoveryPath { get; set; }

    /// <summary>A paired device's credentials, for Studio on another computer (from <see cref="VelumStudioClient.PairAsync"/>).</summary>
    public PairedDevice? Device { get; set; }

    /// <summary>An explicit address (<c>ws://127.0.0.1:28710/</c>), instead of the discovery file.</summary>
    public Uri? Url { get; set; }

    /// <summary>The token for <see cref="Url"/>.</summary>
    public string? Token { get; set; }

    /// <summary>Resolve a local instance's token again for each connection attempt.</summary>
    public Func<string?>? TokenProvider { get; set; }

    /// <summary>The certificate fingerprint to pin for a <c>wss://</c> <see cref="Url"/> (64 hex digits).</summary>
    public string? Fingerprint { get; set; }

    /// <summary>Connect again when the connection drops or Studio restarts, and follow the same topics again. On by default.</summary>
    public bool AutoReconnect { get; set; } = true;

    /// <summary>The first wait before reconnecting; it doubles up to <see cref="MaxReconnectDelay"/>.</summary>
    public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromMilliseconds(250);

    /// <summary>The longest wait between tries to reconnect.</summary>
    public TimeSpan MaxReconnectDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>How long connecting may take.</summary>
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>How long to wait for Studio's answer to a request. Going live can take a while, so the default is generous.</summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(60);
}

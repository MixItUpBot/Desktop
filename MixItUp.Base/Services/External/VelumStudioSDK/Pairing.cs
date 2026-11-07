#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace Velum.Studio;

/// <summary>
/// The link in the QR code Studio shows to pair a device:
/// <c>velum-remote://pair?v=1&amp;host=…&amp;port=…&amp;code=…&amp;fp=…&amp;name=…</c>.
/// </summary>
/// <param name="Host">Studio's address on the network.</param>
/// <param name="Port">The port Studio listens on for devices.</param>
/// <param name="Code">The one-time pairing code.</param>
/// <param name="Fingerprint">The SHA-256 fingerprint of Studio's certificate, 64 hex digits.</param>
/// <param name="InstanceName">The Studio instance's name.</param>
public sealed record PairingLink(string Host, int Port, string Code, string Fingerprint, string InstanceName)
{
    /// <summary>Read a pairing link.</summary>
    /// <exception cref="FormatException">It isn't one.</exception>
    public static PairingLink Parse(string link) =>
        TryParse(link, out var p) ? p : throw new FormatException("not a Velum Studio pairing link (velum-remote://pair?…)");

    /// <summary>Read a pairing link, if it is one.</summary>
    public static bool TryParse(string? link, out PairingLink result)
    {
        result = new PairingLink("", 0, "", "", "");
        const string prefix = "velum-remote://pair?";
        if (link is null || !link.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        var q = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in link[prefix.Length..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            if (eq > 0)
            {
                q[pair[..eq]] = Uri.UnescapeDataString(pair[(eq + 1)..].Replace('+', ' '));
            }
        }
        if (!q.TryGetValue("host", out var host) || !q.TryGetValue("port", out var port) || !int.TryParse(port, NumberStyles.None, CultureInfo.InvariantCulture, out var p)
            || !q.TryGetValue("code", out var code) || !q.TryGetValue("fp", out var fp) || !Pairing.IsFingerprint(fp))
        {
            return false;
        }
        result = new PairingLink(host, p, code, fp.ToLowerInvariant(), q.TryGetValue("name", out var n) ? n : "Velum Studio");
        return true;
    }
}

/// <summary>
/// A device paired with a Studio on the network: everything needed to
/// connect from then on. Store it (<see cref="ToJson"/>) somewhere private:
/// the token is this device's own, and Studio can revoke it.
/// </summary>
public sealed record PairedDevice
{
    /// <summary>Studio's address.</summary>
    public string Host { get; init; } = "";

    /// <summary>Studio's port for devices.</summary>
    public int Port { get; init; }

    /// <summary>The certificate fingerprint to pin.</summary>
    public string Fingerprint { get; init; } = "";

    /// <summary>This device's token.</summary>
    public string Token { get; init; } = "";

    /// <summary>This device's id in Studio.</summary>
    public string DeviceId { get; init; } = "";

    /// <summary>The name Studio lists this device under.</summary>
    public string Name { get; init; } = "";

    /// <summary>The Studio instance's name.</summary>
    public string InstanceName { get; init; } = "";

    /// <summary>This device as JSON, to keep.</summary>
    public string ToJson()
    {
        using var stream = new System.IO.MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            w.WriteString("host", Host);
            w.WriteNumber("port", Port);
            w.WriteString("fingerprint", Fingerprint);
            w.WriteString("token", Token);
            w.WriteString("device_id", DeviceId);
            w.WriteString("name", Name);
            w.WriteString("instance_name", InstanceName);
            w.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>A device kept with <see cref="ToJson"/>.</summary>
    public static PairedDevice FromJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;
        string S(string k) => r.TryGetProperty(k, out var v) ? v.GetString() ?? "" : "";
        return new PairedDevice
        {
            Host = S("host"),
            Port = r.TryGetProperty("port", out var p) ? p.GetInt32() : 0,
            Fingerprint = S("fingerprint"),
            Token = S("token"),
            DeviceId = S("device_id"),
            Name = S("name"),
            InstanceName = S("instance_name"),
        };
    }
}

/// <summary>Certificate pinning for devices on the network.</summary>
public static class Pairing
{
    /// <summary>A certificate's SHA-256 fingerprint as 64 lowercase hex digits, as Studio shows it.</summary>
    public static string Fingerprint(X509Certificate certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        return Convert.ToHexString(SHA256.HashData(certificate.GetRawCertData())).ToLowerInvariant();
    }

    /// <summary>Whether <paramref name="certificate"/> is the one <paramref name="fingerprint"/> names.</summary>
    public static bool Matches(X509Certificate certificate, string fingerprint)
    {
        var mine = Convert.FromHexString(Fingerprint(certificate));
        return IsFingerprint(fingerprint) && CryptographicOperations.FixedTimeEquals(mine, Convert.FromHexString(fingerprint));
    }

    /// <summary>Whether <paramref name="s"/> is 64 hex digits.</summary>
    public static bool IsFingerprint(string? s)
    {
        if (s is null || s.Length != 64)
        {
            return false;
        }
        foreach (var c in s)
        {
            if (!Uri.IsHexDigit(c))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>A host as it goes in a URI: an IPv6 address in brackets.</summary>
    internal static string HostForUri(string host) => host.Contains(':', StringComparison.Ordinal) && !host.StartsWith('[') ? $"[{host}]" : host;
}

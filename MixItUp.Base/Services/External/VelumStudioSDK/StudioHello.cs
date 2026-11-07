#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Velum.Studio;

/// <summary>What Studio says when a connection opens.</summary>
public sealed record StudioHello
{
    internal static readonly StudioHello Empty = new();

    /// <summary>The protocol, <c>velum/1</c>.</summary>
    public string Protocol { get; init; } = "";

    /// <summary>The instance's name.</summary>
    public string InstanceName { get; init; } = "";

    /// <summary>Studio's version.</summary>
    public string Version { get; init; } = "";

    /// <summary>The catalog's version: compare with <see cref="StudioCatalog.Version"/> to see whether Studio has actions this SDK doesn't.</summary>
    public int CatalogVersion { get; init; }

    /// <summary><c>local</c> (this computer), <c>device</c> (a paired device) or <c>pairing</c> (a device that hasn't paired yet).</summary>
    public string Auth { get; init; } = "";

    /// <summary>Every topic Studio has.</summary>
    public IReadOnlyList<string> Topics { get; init; } = [];

    /// <summary>What this Studio can do beyond the first protocol: <c>typed_methods</c>, <c>typed_events</c>.</summary>
    public IReadOnlyList<string> Features { get; init; } = [];

    /// <summary>Whether Studio sends each event as its own notification (every Studio this SDK fully supports does).</summary>
    public bool HasTypedEvents => Features.Contains("typed_events");

    internal static StudioHello From(JsonElement p)
    {
        static string S(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
        static IReadOnlyList<string> L(JsonElement e, string k) =>
            e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray().Select(x => x.GetString() ?? "").ToList() : [];
        return new StudioHello
        {
            Protocol = S(p, "protocol"),
            InstanceName = S(p, "instance_name"),
            Version = S(p, "version"),
            CatalogVersion = p.TryGetProperty("catalog_version", out var cv) && cv.TryGetInt32(out var n) ? n : 0,
            Auth = S(p, "auth"),
            Topics = L(p, "topics"),
            Features = L(p, "features"),
        };
    }
}

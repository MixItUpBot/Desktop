#nullable enable
using System;
using System.Text.Json;

namespace Velum.Studio;

/// <summary>
/// Studio answered a request with an error. Each code has its own subclass
/// (<see cref="VelumNotFoundException"/>, <see cref="VelumConfirmRequiredException"/>…),
/// so a <c>catch</c> can pick the ones it handles. The message is plain
/// English, ready to show.
/// </summary>
public partial class VelumException : Exception
{
    /// <summary>An error Studio sent.</summary>
    public VelumException(ErrorCode code, string message, JsonElement? data = null) : base(message)
    {
        Code = code;
        ErrorData = data;
    }

    /// <summary>The error's code (a number this SDK doesn't name is still kept, as is).</summary>
    public ErrorCode Code { get; }

    /// <summary>The code as a number.</summary>
    public int RawCode => (int)Code;

    /// <summary>What else Studio said about it, such as a refused change's reason.</summary>
    public JsonElement? ErrorData { get; }
}

public sealed partial class VelumRateLimitedException
{
    /// <summary>When to try again, as Studio says.</summary>
    public TimeSpan? RetryAfter =>
        ErrorData is { ValueKind: JsonValueKind.Object } d && d.TryGetProperty("retry_after_ms", out var ms) && ms.TryGetInt64(out var n) ? TimeSpan.FromMilliseconds(n) : null;
}

/// <summary>Studio couldn't be reached, or the connection closed before an answer came.</summary>
public class VelumConnectionException : Exception
{
    /// <summary>A connection problem.</summary>
    public VelumConnectionException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}

/// <summary>Studio sent something this SDK couldn't read.</summary>
public sealed class VelumProtocolException : VelumConnectionException
{
    /// <summary>A message that didn't read.</summary>
    public VelumProtocolException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}

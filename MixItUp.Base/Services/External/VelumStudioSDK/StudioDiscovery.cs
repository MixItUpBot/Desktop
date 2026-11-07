#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Velum.Studio;

/// <summary>One running Studio, as its discovery entry describes it.</summary>
/// <param name="Port">The port it listens on, on 127.0.0.1.</param>
/// <param name="Token">The token to connect with. Keep it private.</param>
/// <param name="Pid">Its process id.</param>
/// <param name="InstanceName">Its name ("Velum Studio", "Velum Studio (2)"…).</param>
/// <param name="Version">Studio's version.</param>
public sealed record StudioInstance(int Port, string Token, int Pid, string InstanceName, string Version)
{
    /// <inheritdoc/>
    public override string ToString() => $"{InstanceName} {Version} on port {Port} (pid {Pid})";
}

/// <summary>The systems Studio runs on, for <see cref="StudioDiscovery.PathFor"/>.</summary>
public enum StudioSystem
{
    /// <summary>Windows.</summary>
    Windows,
    /// <summary>macOS.</summary>
    MacOS,
    /// <summary>Linux.</summary>
    Linux,
}

/// <summary>
/// Finds running Studios through the discovery file each one writes
/// (<c>control.json</c> in Studio's folder): its port, its token and its name.
/// </summary>
public static class StudioDiscovery
{
    /// <summary>The discovery file's name.</summary>
    public const string FileName = "control.json";

    /// <summary>The system this process runs on.</summary>
    public static StudioSystem CurrentSystem =>
        OperatingSystem.IsWindows() ? StudioSystem.Windows : OperatingSystem.IsMacOS() ? StudioSystem.MacOS : StudioSystem.Linux;

    /// <summary>Where Studio keeps its discovery file on this computer.</summary>
    public static string DefaultPath => PathFor(CurrentSystem, Environment.GetEnvironmentVariable, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    /// <summary>
    /// Where the discovery file is on <paramref name="system"/>, given its
    /// environment and home folder:
    /// <c>%APPDATA%\CLEARLYlogic\Velum Studio\control.json</c> on Windows,
    /// <c>~/Library/Application Support/com.legendremedia.velumstudio/control.json</c> on macOS,
    /// <c>$XDG_CONFIG_HOME/velum-studio/control.json</c> (else <c>~/.config/…</c>) on Linux.
    /// </summary>
    public static string PathFor(StudioSystem system, Func<string, string?> environment, string home)
    {
        ArgumentNullException.ThrowIfNull(environment);
        switch (system)
        {
            case StudioSystem.Windows:
                var appData = environment("APPDATA") is { Length: > 0 } a ? a : Path.Combine(home, "AppData", "Roaming");
                return Path.Combine(appData, "CLEARLYlogic", "Velum Studio", FileName);
            case StudioSystem.MacOS:
                return Path.Combine(home, "Library", "Application Support", "com.legendremedia.velumstudio", FileName);
            default:
                var config = environment("XDG_CONFIG_HOME") is { Length: > 0 } x && Path.IsPathRooted(x) ? x : Path.Combine(home, ".config");
                return Path.Combine(config, "velum-studio", FileName);
        }
    }

    /// <summary>Every entry in the discovery file, running or not; empty when there's no file (no Studio running).</summary>
    public static IReadOnlyList<StudioInstance> Read(string? path = null)
    {
        path ??= DefaultPath;
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            return [];
        }
        return Parse(text);
    }

    /// <summary>The entries of a discovery file's text.</summary>
    public static IReadOnlyList<StudioInstance> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var list = new List<StudioInstance>();
        if (!doc.RootElement.TryGetProperty("instances", out var instances) || instances.ValueKind != JsonValueKind.Array)
        {
            return list;
        }
        foreach (var e in instances.EnumerateArray())
        {
            if (!e.TryGetProperty("port", out var port) || !port.TryGetInt32(out var p) || !e.TryGetProperty("token", out var token))
            {
                continue;
            }
            var pid = e.TryGetProperty("pid", out var pidProp) && pidProp.TryGetInt32(out var n) ? n : 0;
            var name = e.TryGetProperty("instanceName", out var nm) ? nm.GetString() ?? "Velum Studio" : "Velum Studio";
            var version = e.TryGetProperty("version", out var v) ? v.GetString() ?? "" : "";
            list.Add(new StudioInstance(p, token.GetString() ?? "", pid, name, version));
        }
        return list;
    }

    /// <summary>
    /// The Studios running now, in the file's order. An entry whose process
    /// is gone (a Studio that crashed) is skipped.
    /// </summary>
    public static IReadOnlyList<StudioInstance> FindRunning(string? path = null) => Read(path).Where(i => IsRunning(i.Pid)).ToList();

    /// <summary>Whether a process with this id is running.</summary>
    public static bool IsRunning(int pid)
    {
        if (pid <= 0)
        {
            return false;
        }
        try
        {
            using var p = Process.GetProcessById(pid);
            return !p.HasExited;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            return false;
        }
    }
}

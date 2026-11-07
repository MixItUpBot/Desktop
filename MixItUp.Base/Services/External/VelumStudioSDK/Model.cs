#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Velum.Studio;

/// <summary>
/// Something that happened in Studio. Every event this SDK knows is its own
/// record deriving from this one (<see cref="SceneChangedEvent"/>,
/// <see cref="ChatMessageEvent"/>…): switch on <see cref="Type"/> or
/// pattern-match the record.
/// </summary>
public abstract record StudioEvent
{
    /// <summary>Which event it is.</summary>
    [JsonIgnore]
    public abstract EventType Type { get; }
}

/// <summary>What the catalog says about an action, for building pickers and forms.</summary>
/// <param name="Type">The action.</param>
/// <param name="Id">Its id, stable forever (<c>scene.switch</c>).</param>
/// <param name="Name">Its name, ready to show.</param>
/// <param name="Description">What it does, ready to show.</param>
/// <param name="Group">The heading a picker lists it under.</param>
/// <param name="Destructive">It ends something people see: guard the control that runs it.</param>
/// <param name="Controllers">What suits it: <c>key</c>, <c>dial</c>, <c>display</c>.</param>
/// <param name="Params">Its parameters, in order.</param>
/// <param name="ParamsType">Its parameters' record, or null when it takes none.</param>
/// <param name="ResultType">Its result's type.</param>
/// <param name="StateTopic">The topic its live state is in, if it has one.</param>
public sealed record ActionInfo(
    ActionType Type,
    string Id,
    string Name,
    string Description,
    string Group,
    bool Destructive,
    IReadOnlyList<string> Controllers,
    IReadOnlyList<ParamInfo> Params,
    Type? ParamsType,
    Type ResultType,
    TopicType? StateTopic);

/// <summary>One of an action's parameters, as the catalog lists it.</summary>
/// <param name="Name">Its name on the wire.</param>
/// <param name="Kind">The catalog's type: <c>scene</c>, <c>layer</c>, <c>choice</c>, <c>number</c>…</param>
/// <param name="Required">It must be given.</param>
/// <param name="Description">What it is, ready to show.</param>
/// <param name="DefaultJson">What Studio uses when it's left out, as JSON.</param>
/// <param name="Options">A choice's words.</param>
/// <param name="Min">A number's least value.</param>
/// <param name="Max">A number's greatest value.</param>
/// <param name="Unit">A number's unit.</param>
/// <param name="Choices">The topic that lists its possible values (scenes, sources…).</param>
public sealed record ParamInfo(
    string Name,
    string Kind,
    bool Required,
    string Description,
    string? DefaultJson,
    IReadOnlyList<string> Options,
    double? Min,
    double? Max,
    string? Unit,
    TopicType? Choices);

/// <summary>What the catalog says about an event.</summary>
/// <param name="Type">The event.</param>
/// <param name="Id">Its id, stable forever (<c>scene.changed</c>).</param>
/// <param name="Name">Its name, ready to show.</param>
/// <param name="Description">What it means.</param>
/// <param name="Topic">The topic to subscribe to for it.</param>
/// <param name="DataType">Its record.</param>
public sealed record EventInfo(EventType Type, string Id, string Name, string Description, TopicType Topic, Type DataType);

/// <summary>A topic's new value.</summary>
public sealed class TopicChangedEventArgs(TopicType topic, object value) : EventArgs
{
    /// <summary>Which topic.</summary>
    public TopicType Topic { get; } = topic;

    /// <summary>Its whole new value (<see cref="ScenesState"/> for <see cref="TopicType.Scenes"/>, and so on).</summary>
    public object Value { get; } = value;
}

/// <summary>
/// The latest value of each state topic, kept up to date by Studio's pushes
/// once subscribed (<see cref="VelumStudioClient.SubscribeAsync(TopicType[])"/>),
/// with a typed event per topic and <see cref="Changed"/> for all of them.
/// </summary>
public sealed partial class StudioState
{
    private readonly Dictionary<TopicType, string> _last = new();

    /// <summary>Any topic changed.</summary>
    public event EventHandler<TopicChangedEventArgs>? Changed;

    internal void Apply(string topic, JsonElement value, VelumStudioClient client)
    {
        if (!TopicTypes.TryParse(topic, out var t))
        {
            return;
        }
        // The same value again (a fetch right after the first push) isn't a change.
        var raw = value.GetRawText();
        lock (_last)
        {
            if (_last.TryGetValue(t, out var before) && before == raw)
            {
                return;
            }
            _last[t] = raw;
        }
        object? v;
        try
        {
            v = Store(t, value);
        }
        catch (JsonException e)
        {
            client.ReportHandler(new VelumProtocolException($"couldn't read Studio's {topic} topic: {e.Message}", e));
            return;
        }
        if (v is null)
        {
            return;
        }
        try
        {
            RaiseTyped(t, v);
        }
        catch (Exception e)
        {
            client.ReportHandler(e);
        }
        try
        {
            Changed?.Invoke(this, new TopicChangedEventArgs(t, v));
        }
        catch (Exception e)
        {
            client.ReportHandler(e);
        }
    }

    private partial object? Store(TopicType topic, JsonElement value);

    private partial void RaiseTyped(TopicType topic, object value);
}

/// <summary>The source-generated JSON metadata for every type, so nothing is read by reflection.</summary>
internal static class VelumJson
{
    public static JsonTypeInfo<T> Info<T>() =>
        (JsonTypeInfo<T>)(VelumJsonContext.Default.GetTypeInfo(typeof(T)) ?? throw new InvalidOperationException($"{typeof(T).Name} isn't in the SDK's JSON context"));
}

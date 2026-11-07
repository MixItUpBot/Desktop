#nullable enable
using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Velum.Studio;

/// <summary>
/// A setting's value: a number, true or false, or text (a color as
/// <c>#rrggbb</c>, a choice's word, a file's path). Effects' settings hold
/// these; <see cref="FilterSetParams.Value"/> takes one.
/// </summary>
[JsonConverter(typeof(StudioValueJsonConverter))]
public readonly struct StudioValue : IEquatable<StudioValue>
{
    private readonly double _number;
    private readonly string? _text;

    private StudioValue(JsonValueKind kind, double number, string? text)
    {
        Kind = kind;
        _number = number;
        _text = text;
    }

    /// <summary><see cref="JsonValueKind.Number"/>, <see cref="JsonValueKind.True"/>, <see cref="JsonValueKind.False"/> or <see cref="JsonValueKind.String"/>.</summary>
    public JsonValueKind Kind { get; }

    /// <summary>Whether it's a number.</summary>
    public bool IsNumber => Kind == JsonValueKind.Number;

    /// <summary>Whether it's true or false.</summary>
    public bool IsBool => Kind is JsonValueKind.True or JsonValueKind.False;

    /// <summary>Whether it's text.</summary>
    public bool IsText => Kind == JsonValueKind.String;

    /// <summary>The number.</summary>
    /// <exception cref="InvalidOperationException">It isn't one.</exception>
    public double AsNumber() => IsNumber ? _number : throw new InvalidOperationException($"the value is {this}, not a number");

    /// <summary>True or false.</summary>
    /// <exception cref="InvalidOperationException">It isn't either.</exception>
    public bool AsBool() => IsBool ? Kind == JsonValueKind.True : throw new InvalidOperationException($"the value is {this}, not true or false");

    /// <summary>The text.</summary>
    /// <exception cref="InvalidOperationException">It isn't text.</exception>
    public string AsText() => IsText ? _text ?? "" : throw new InvalidOperationException($"the value is {this}, not text");

    /// <summary>A number.</summary>
    public static implicit operator StudioValue(double value) => new(JsonValueKind.Number, value, null);

    /// <summary>True or false.</summary>
    public static implicit operator StudioValue(bool value) => new(value ? JsonValueKind.True : JsonValueKind.False, 0, null);

    /// <summary>Text.</summary>
    public static implicit operator StudioValue(string value) => new(JsonValueKind.String, 0, value ?? throw new ArgumentNullException(nameof(value)));

    /// <inheritdoc/>
    public bool Equals(StudioValue other) => Kind == other.Kind && _number.Equals(other._number) && _text == other._text;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is StudioValue v && Equals(v);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Kind, _number, _text);

    /// <summary>Equal values.</summary>
    public static bool operator ==(StudioValue a, StudioValue b) => a.Equals(b);

    /// <summary>Different values.</summary>
    public static bool operator !=(StudioValue a, StudioValue b) => !a.Equals(b);

    /// <inheritdoc/>
    public override string ToString() => Kind switch
    {
        JsonValueKind.Number => _number.ToString(CultureInfo.InvariantCulture),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.String => _text ?? "",
        _ => "",
    };

    internal void Write(Utf8JsonWriter w)
    {
        switch (Kind)
        {
            case JsonValueKind.Number: w.WriteNumberValue(_number); break;
            case JsonValueKind.True: w.WriteBooleanValue(true); break;
            case JsonValueKind.False: w.WriteBooleanValue(false); break;
            default: w.WriteStringValue(_text ?? ""); break;
        }
    }
}

internal sealed class StudioValueJsonConverter : JsonConverter<StudioValue>
{
    public override StudioValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.TokenType switch
    {
        JsonTokenType.Number => reader.GetDouble(),
        JsonTokenType.True => true,
        JsonTokenType.False => false,
        JsonTokenType.String => reader.GetString() ?? "",
        _ => throw new JsonException("a setting's value is a number, true or false, or text"),
    };

    public override void Write(Utf8JsonWriter writer, StudioValue value, JsonSerializerOptions options) => value.Write(writer);
}

/// <summary>
/// Text or a whole number: a layer is named by its id (or the name of what
/// it places) or by its per-scene number. Converts from either.
/// </summary>
[JsonConverter(typeof(StringOrIntegerJsonConverter))]
public readonly struct StringOrInteger : IEquatable<StringOrInteger>
{
    private StringOrInteger(string? text, long number)
    {
        Text = text;
        Number = number;
    }

    /// <summary>The text, when it's text.</summary>
    public string? Text { get; }

    /// <summary>The number, when it isn't text.</summary>
    public long Number { get; }

    /// <summary>Whether it's text.</summary>
    public bool IsText => Text is not null;

    /// <summary>Text: an id or a name.</summary>
    public static implicit operator StringOrInteger(string text) => new(text ?? throw new ArgumentNullException(nameof(text)), 0);

    /// <summary>A whole number.</summary>
    public static implicit operator StringOrInteger(long number) => new(null, number);

    /// <summary>A whole number.</summary>
    public static implicit operator StringOrInteger(int number) => new(null, number);

    /// <inheritdoc/>
    public bool Equals(StringOrInteger other) => Text == other.Text && Number == other.Number;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is StringOrInteger v && Equals(v);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Text, Number);

    /// <summary>Equal values.</summary>
    public static bool operator ==(StringOrInteger a, StringOrInteger b) => a.Equals(b);

    /// <summary>Different values.</summary>
    public static bool operator !=(StringOrInteger a, StringOrInteger b) => !a.Equals(b);

    /// <inheritdoc/>
    public override string ToString() => Text ?? Number.ToString(CultureInfo.InvariantCulture);
}

internal sealed class StringOrIntegerJsonConverter : JsonConverter<StringOrInteger>
{
    public override StringOrInteger Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.TokenType switch
    {
        JsonTokenType.String => reader.GetString() ?? "",
        JsonTokenType.Number => reader.GetInt64(),
        _ => throw new JsonException("expected text or a whole number"),
    };

    public override void Write(Utf8JsonWriter writer, StringOrInteger value, JsonSerializerOptions options)
    {
        if (value.Text is { } t)
        {
            writer.WriteStringValue(t);
        }
        else
        {
            writer.WriteNumberValue(value.Number);
        }
    }
}

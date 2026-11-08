using System.Text.Json;
using System.Text.Json.Serialization;
using Koto.Domain;

namespace Koto.Application;

/// <summary>
/// Serializes <see cref="Envelope{T}"/> so that <c>result</c> is <c>null</c> on failure (and for
/// <see cref="Unit"/>), regardless of the result type being a struct. Property names follow the
/// host's naming policy; reading is case-insensitive.
/// </summary>
public sealed class EnvelopeJsonConverterFactory : JsonConverterFactory
{
    /// <inheritdoc/>
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Envelope<>);

    /// <inheritdoc/>
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(
            typeof(EnvelopeJsonConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()[0]))!;
}

internal sealed class EnvelopeJsonConverter<T> : JsonConverter<Envelope<T>>
{
    private const string Success = "Success", Result = "Result", Errors = "Errors",
        CorrelationId = "CorrelationId", Timestamp = "Timestamp";

    public override void Write(Utf8JsonWriter writer, Envelope<T> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteBoolean(Name(Success, options), value.Success);

        writer.WritePropertyName(Name(Result, options));
        if (!value.Success || value.Result is null || value.Result is Unit)
            writer.WriteNullValue();
        else
            JsonSerializer.Serialize(writer, value.Result, options);

        writer.WritePropertyName(Name(Errors, options));
        JsonSerializer.Serialize(writer, value.Errors, options);

        if (value.CorrelationId is null)
            writer.WriteNull(Name(CorrelationId, options));
        else
            writer.WriteString(Name(CorrelationId, options), value.CorrelationId);

        writer.WritePropertyName(Name(Timestamp, options));
        JsonSerializer.Serialize(writer, value.Timestamp, options);
        writer.WriteEndObject();
    }

    public override Envelope<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException("Expected an envelope object.");

        var success = false;
        T? result = default;
        IReadOnlyList<EnvelopeError>? errors = null;
        string? correlationId = null;
        DateTimeOffset timestamp = default;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var name = reader.GetString();
            reader.Read();
            if (Is(name, Success)) success = reader.GetBoolean();
            else if (Is(name, Result)) result = reader.TokenType == JsonTokenType.Null ? default : JsonSerializer.Deserialize<T>(ref reader, options);
            else if (Is(name, Errors)) errors = JsonSerializer.Deserialize<List<EnvelopeError>>(ref reader, options);
            else if (Is(name, CorrelationId)) correlationId = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
            else if (Is(name, Timestamp)) timestamp = JsonSerializer.Deserialize<DateTimeOffset>(ref reader, options);
            else reader.Skip();
        }

        return new Envelope<T>(success, result, errors ?? [], correlationId, timestamp);
    }

    private static string Name(string property, JsonSerializerOptions options) =>
        options.PropertyNamingPolicy?.ConvertName(property) ?? property;

    private static bool Is(string? name, string property) =>
        string.Equals(name, property, StringComparison.OrdinalIgnoreCase);
}

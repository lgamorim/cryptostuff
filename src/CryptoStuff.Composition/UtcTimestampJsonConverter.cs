using System.Text.Json;
using System.Text.Json.Serialization;
using CryptoStuff.Core;

namespace CryptoStuff.Composition;

/// <summary>
/// Writes a <see cref="DateTimeOffset"/> as UTC text ending in <c>Z</c>, and
/// reads one only under the rule <see cref="TimestampFormatValidator"/>
/// applies to request timestamps: text without an offset names no instant, so
/// it is refused rather than read as the server's local time.
/// </summary>
internal sealed class UtcTimestampJsonConverter : JsonConverter<DateTimeOffset>
{
    /// <inheritdoc />
    // A token other than a string makes GetString throw, which the serializer
    // reports as a JsonException like the one below.
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        TimestampFormatValidator.TryParse(reader.GetString(), out var timestamp)
            ? timestamp
            : throw new JsonException("Expected an ISO 8601 timestamp with a UTC offset, such as 2024-01-31T14:30:00Z.");

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(UtcTimestamp.Format(value));
}

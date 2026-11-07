using System.Text.Json;
using System.Text.Json.Serialization;

namespace CryptoStuff.Core;

/// <summary>Writes a <see cref="DateTimeOffset"/> as UTC text ending in <c>Z</c>.</summary>
public sealed class UtcTimestampJsonConverter : JsonConverter<DateTimeOffset>
{
    /// <inheritdoc />
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetDateTimeOffset();

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(UtcTimestamp.Format(value));
}

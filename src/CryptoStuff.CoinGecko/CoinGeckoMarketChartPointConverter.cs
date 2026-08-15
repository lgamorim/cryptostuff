using System.Text.Json;
using System.Text.Json.Serialization;

namespace CryptoStuff.CoinGecko;

/// <summary>
/// Reads and writes a <see cref="CoinGeckoMarketChartPoint"/> as CoinGecko's
/// two-element <c>[timestamp, value]</c> JSON array.
/// </summary>
public sealed class CoinGeckoMarketChartPointConverter : JsonConverter<CoinGeckoMarketChartPoint>
{
    /// <summary>
    /// Overridden so <see cref="Read"/> is invoked for a JSON `null` token
    /// instead of it being silently assigned as a null element of an
    /// otherwise non-nullable series.
    /// </summary>
    public override bool HandleNull => true;

    /// <inheritdoc />
    public override CoinGeckoMarketChartPoint Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            throw new JsonException("Expected a 2-element [timestamp, value] array for a market chart point, but found null.");
        }

        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() != 2)
        {
            throw new JsonException(
                "Expected a 2-element [timestamp, value] array for a market chart point, but found " +
                (root.ValueKind == JsonValueKind.Array ? $"{root.GetArrayLength()} element(s)." : $"a {root.ValueKind} token."));
        }

        var timestampElement = root[0];
        var valueElement = root[1];

        if (timestampElement.ValueKind != JsonValueKind.Number || valueElement.ValueKind != JsonValueKind.Number)
        {
            throw new JsonException("Expected both market chart point elements to be numeric.");
        }

        if (!timestampElement.TryGetInt64(out var timestamp) || !valueElement.TryGetDecimal(out var value))
        {
            throw new JsonException("Expected the market chart point's timestamp and value to be representable as a long and a decimal, respectively.");
        }

        return new CoinGeckoMarketChartPoint(timestamp, value);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, CoinGeckoMarketChartPoint? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartArray();
        writer.WriteNumberValue(value.UnixTimestampMilliseconds);
        writer.WriteNumberValue(value.Value);
        writer.WriteEndArray();
    }
}

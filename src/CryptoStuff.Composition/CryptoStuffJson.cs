using System.Text.Json;
using System.Text.Json.Serialization;

namespace CryptoStuff.Composition;

/// <summary>
/// The one JSON shape both hosts write and read. Enumerations travel as the
/// lowercase tokens <see cref="Core.EnumTokens"/> writes and Core's parsers
/// accept (<c>average-cost</c>, <c>buy</c>), never as numbers. Timestamps
/// travel as UTC text ending in <c>Z</c>, and text without an offset is
/// refused rather than read as local time.
/// </summary>
public static class CryptoStuffJson
{
    /// <summary>
    /// Adds the hosts' converters to <paramref name="options"/>, leaving its
    /// other settings as they are, and returns the same instance.
    /// </summary>
    public static JsonSerializerOptions Configure(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower));
        options.Converters.Add(new UtcTimestampJsonConverter());
        return options;
    }
}

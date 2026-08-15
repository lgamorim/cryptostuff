using System.Text.Json.Serialization;

namespace CryptoStuff.CoinGecko;

/// <summary>
/// A coin's image at three sizes, independently nullable — the caller falls
/// back from large to small to thumbnail.
/// </summary>
public sealed record CoinGeckoImage
{
    /// <summary>The thumbnail-size image URL.</summary>
    [JsonPropertyName("thumb")]
    public string? Thumb { get; init; }

    /// <summary>The small-size image URL.</summary>
    [JsonPropertyName("small")]
    public string? Small { get; init; }

    /// <summary>The large-size image URL.</summary>
    [JsonPropertyName("large")]
    public string? Large { get; init; }
}

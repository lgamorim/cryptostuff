using System.Text.Json.Serialization;

namespace CryptoStuff.CoinGecko;

/// <summary>
/// A repository's code churn over the last 4 weeks. Both fields are
/// independently nullable — CoinGecko can return either field as null even
/// when the object itself is present.
/// </summary>
public sealed record CoinGeckoCodeChurn
{
    /// <summary>Lines added over the last 4 weeks.</summary>
    [JsonPropertyName("additions")]
    public int? Additions { get; init; }

    /// <summary>Lines deleted over the last 4 weeks.</summary>
    [JsonPropertyName("deletions")]
    public int? Deletions { get; init; }
}

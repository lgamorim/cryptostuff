namespace CryptoStuff.Core;

/// <summary>
/// A request for a coin's historical price, market cap, and volume series —
/// the "Historical market series" capability (CLI <c>history</c> command).
/// </summary>
public sealed record CoinMarketChartQuery
{
    /// <summary>The coin id to fetch the series for.</summary>
    public required string CoinId { get; init; }

    /// <summary>The vs_currency code the series values are denominated in.</summary>
    public required string VsCurrency { get; init; }

    /// <summary>The number of days of history to fetch.</summary>
    public required int Days { get; init; }
}

namespace CryptoStuff.Core;

/// <summary>A coin's historical price, market cap, and volume series.</summary>
public sealed record CoinMarketChartView
{
    /// <summary>The coin's price series.</summary>
    public required IReadOnlyList<CoinMarketChartPoint> Prices { get; init; }

    /// <summary>The coin's market capitalization series.</summary>
    public required IReadOnlyList<CoinMarketChartPoint> MarketCaps { get; init; }

    /// <summary>The coin's trading volume series.</summary>
    public required IReadOnlyList<CoinMarketChartPoint> TotalVolumes { get; init; }
}

/// <summary>One point in a market chart series.</summary>
public sealed record CoinMarketChartPoint
{
    /// <summary>The point's date, formatted <c>yyyy-MM-dd</c> under the invariant culture.</summary>
    public required string Date { get; init; }

    /// <summary>The point's value (price, market cap, or volume, depending on the series).</summary>
    public required decimal Value { get; init; }
}

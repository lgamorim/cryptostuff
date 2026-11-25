namespace CryptoStuff.Core.Portfolios;

/// <summary>A portfolio's holdings valued at live prices.</summary>
public sealed record PortfolioValuationView
{
    /// <summary>The valued portfolio's id.</summary>
    public required int PortfolioId { get; init; }

    /// <summary>The lowercased vs_currency every figure is in.</summary>
    public required string VsCurrency { get; init; }

    /// <summary>The cost-basis method the cost bases and realized profit and loss follow.</summary>
    public required CostBasisMethod CostBasisMethod { get; init; }

    /// <summary>
    /// One entry per coin the portfolio has ever traded, ordered by coin id.
    /// A coin whose position is fully closed has a quantity of zero and only
    /// its realized profit and loss.
    /// </summary>
    public required IReadOnlyList<CoinValuationView> Coins { get; init; }

    /// <summary>
    /// The ids of the coins still held that the market data provider gave no price for in the
    /// portfolio's vs_currency, ordered by coin id; empty when every one was
    /// priced. Their rows have no market value or unrealized profit and loss,
    /// and the totals leave them out.
    /// </summary>
    public required IReadOnlyList<string> UnpricedCoins { get; init; }

    /// <summary>The sum of every priced coin's figures, closed coins included; see <see cref="UnpricedCoins"/>.</summary>
    public required PortfolioValuationTotals Totals { get; init; }
}

/// <summary>One coin's holding, valued in a <see cref="PortfolioValuationView"/>.</summary>
public sealed record CoinValuationView
{
    /// <summary>The lowercased coin id.</summary>
    public required string CoinId { get; init; }

    /// <summary>How many units are still held.</summary>
    public required decimal Quantity { get; init; }

    /// <summary>The units held times the live price of one; null when the coin is held but the market data provider gave no price for it.</summary>
    public required decimal? MarketValue { get; init; }

    /// <summary>
    /// The market value as a percentage, from 0 to 100, of the priced coins' total market
    /// value, unrounded; 0 when the market value is, even when the total is too, and null
    /// when the market value is.
    /// </summary>
    public required decimal? AllocationPercent { get; init; }

    /// <summary>What the units still held cost, fees on their buys included.</summary>
    public required decimal CostBasis { get; init; }

    /// <summary>The market value less the cost basis; null when the market value is.</summary>
    public required decimal? UnrealizedProfitAndLoss { get; init; }

    /// <summary>What the coin's sells have made over the cost of the units they sold.</summary>
    public required decimal RealizedProfitAndLoss { get; init; }
}

/// <summary>The figures a valuation sums across its priced coins.</summary>
public sealed record PortfolioValuationTotals
{
    /// <summary>What the units still held are worth at the live prices.</summary>
    public required decimal MarketValue { get; init; }

    /// <summary>What the units still held cost.</summary>
    public required decimal CostBasis { get; init; }

    /// <summary>The market value less the cost basis.</summary>
    public required decimal UnrealizedProfitAndLoss { get; init; }

    /// <summary>What the sells have made over the cost of the units they sold.</summary>
    public required decimal RealizedProfitAndLoss { get; init; }
}

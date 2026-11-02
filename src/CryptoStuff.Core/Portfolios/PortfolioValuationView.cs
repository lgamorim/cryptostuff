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

    /// <summary>The sum of every coin's figures.</summary>
    public required PortfolioValuationTotals Totals { get; init; }
}

/// <summary>One coin's holding, valued in a <see cref="PortfolioValuationView"/>.</summary>
public sealed record CoinValuationView
{
    /// <summary>The lowercased coin id.</summary>
    public required string CoinId { get; init; }

    /// <summary>How many units are still held.</summary>
    public required decimal Quantity { get; init; }

    /// <summary>The units held times the live price of one.</summary>
    public required decimal MarketValue { get; init; }

    /// <summary>What the units still held cost, fees on their buys included.</summary>
    public required decimal CostBasis { get; init; }

    /// <summary>The market value less the cost basis.</summary>
    public required decimal UnrealizedProfitAndLoss { get; init; }

    /// <summary>What the coin's sells have made over the cost of the units they sold.</summary>
    public required decimal RealizedProfitAndLoss { get; init; }
}

/// <summary>The figures a valuation sums across its coins.</summary>
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

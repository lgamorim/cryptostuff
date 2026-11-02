namespace CryptoStuff.Core.Portfolios;

/// <summary>
/// What one coin's transactions leave behind under a cost-basis method, in
/// the portfolio's vs_currency. Amounts are exact: nothing is rounded.
/// </summary>
public sealed record CoinPosition
{
    /// <summary>How many units are still held.</summary>
    public required decimal Quantity { get; init; }

    /// <summary>What the units still held cost, fees on their buys included.</summary>
    public required decimal CostBasis { get; init; }

    /// <summary>
    /// What the sells made over the cost of the units they sold: proceeds
    /// less sell fees, less the cost the method attributes to those units.
    /// Negative for a loss.
    /// </summary>
    public required decimal RealizedProfitAndLoss { get; init; }
}

using System.Numerics;

namespace CryptoStuff.Core.Portfolios;

/// <summary>
/// Highest in, first out: each buy is a lot, and a sell uses up the lots
/// that cost the most per unit first, releasing their cost — a pro-rata
/// share of it from a lot it only partly uses.
/// </summary>
public sealed class HifoMethod : ICostBasisMethod
{
    // The lot that cost the most per unit is taken first; of lots that cost
    // the same, the one bought first.
    private static readonly IComparer<BoughtLot> DearestFirst =
        Comparer<BoughtLot>.Create((x, y) =>
        {
            var dearer = CompareCostPerUnit(y, x);
            return dearer != 0 ? dearer : x.Position.CompareTo(y.Position);
        });

    /// <inheritdoc/>
    public CoinPosition CalculatePosition(IEnumerable<PortfolioTransaction> transactions) =>
        LotReplay.CalculatePosition(transactions, DearestFirst);

    // Compares x's cost per unit with y's exactly. Comparing x.Cost ÷
    // x.Quantity with y.Cost ÷ y.Quantity as decimals would round away
    // differences past a decimal's digits, so this compares
    // x.Cost × y.Quantity with y.Cost × x.Quantity on the decimals' digits
    // instead; quantities are always above zero.
    private static int CompareCostPerUnit(BoughtLot x, BoughtLot y)
    {
        var (xCost, xCostScale) = CostBasisReplay.Decompose(x.Cost);
        var (xQuantity, xQuantityScale) = CostBasisReplay.Decompose(x.Quantity);
        var (yCost, yCostScale) = CostBasisReplay.Decompose(y.Cost);
        var (yQuantity, yQuantityScale) = CostBasisReplay.Decompose(y.Quantity);

        // Each product is its digits over 10^(its two scales), so both are
        // brought over the same power of ten before their digits compare.
        var left = xCost * yQuantity * BigInteger.Pow(10, yCostScale + xQuantityScale);
        var right = yCost * xQuantity * BigInteger.Pow(10, xCostScale + yQuantityScale);
        return left.CompareTo(right);
    }
}

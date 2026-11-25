namespace CryptoStuff.Core.Portfolios;

/// <summary>
/// First in, first out: each buy is a lot, and a sell uses up the oldest
/// lots first, releasing their cost — a pro-rata share of it from a lot it
/// only partly uses.
/// </summary>
public sealed class FifoMethod : ICostBasisMethod
{
    // The lot bought first in ledger order is taken first.
    private static readonly IComparer<BoughtLot> OldestFirst =
        Comparer<BoughtLot>.Create((x, y) => x.Position.CompareTo(y.Position));

    /// <inheritdoc/>
    public CoinPosition CalculatePosition(IEnumerable<PortfolioTransaction> transactions) =>
        LotReplay.CalculatePosition(transactions, OldestFirst);
}

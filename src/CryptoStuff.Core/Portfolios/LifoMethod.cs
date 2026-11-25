namespace CryptoStuff.Core.Portfolios;

/// <summary>
/// Last in, first out: each buy is a lot, and a sell uses up the newest lots
/// it finds first — the latest in ledger order among those held when it is
/// replayed — releasing their cost, a pro-rata share of it from a lot it only
/// partly uses.
/// </summary>
public sealed class LifoMethod : ICostBasisMethod
{
    // The lot bought last in ledger order is taken first.
    private static readonly IComparer<BoughtLot> NewestFirst =
        Comparer<BoughtLot>.Create((x, y) => y.Position.CompareTo(x.Position));

    /// <inheritdoc/>
    public CoinPosition CalculatePosition(IEnumerable<PortfolioTransaction> transactions) =>
        LotReplay.CalculatePosition(transactions, NewestFirst);
}

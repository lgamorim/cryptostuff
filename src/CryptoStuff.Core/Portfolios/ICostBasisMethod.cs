namespace CryptoStuff.Core.Portfolios;

/// <summary>
/// How cost is attributed to the units a coin's sells take: one
/// implementation per <see cref="CostBasisMethod"/>, chosen through
/// <see cref="CostBasisMethods.For"/>.
/// </summary>
public interface ICostBasisMethod
{
    /// <summary>
    /// Replays one coin's transactions in ledger order — by when each
    /// happened, then by sequence — and returns the position they leave. A
    /// buy's fee adds to its cost; a sell's fee comes off its proceeds.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="transactions"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// The transactions are for more than one coin, or a sell would take the
    /// holding below zero, which a portfolio's ledger never records.
    /// </exception>
    CoinPosition CalculatePosition(IEnumerable<PortfolioTransaction> transactions);
}

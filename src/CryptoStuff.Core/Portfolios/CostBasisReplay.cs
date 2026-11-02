using System.Globalization;

namespace CryptoStuff.Core.Portfolios;

/// <summary>
/// What every cost-basis method needs before and during its replay: one
/// coin's transactions in ledger order, and a check that a sell never takes
/// more than is held.
/// </summary>
internal static class CostBasisReplay
{
    private const string TransactionsParameter = "transactions";

    /// <summary>Returns <paramref name="transactions"/> in ledger order — by when each happened, then by sequence.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="transactions"/> is null.</exception>
    /// <exception cref="ArgumentException">The transactions are for more than one coin.</exception>
    public static IReadOnlyList<PortfolioTransaction> InLedgerOrder(IEnumerable<PortfolioTransaction> transactions)
    {
        ArgumentNullException.ThrowIfNull(transactions);
        var ordered = Portfolio.InLedgerOrder(transactions).ToList();
        if (ordered.Select(t => t.CoinId).Distinct().Skip(1).Any())
        {
            throw new ArgumentException("The transactions must all be for one coin.", TransactionsParameter);
        }

        return ordered;
    }

    /// <summary>
    /// Throws when <paramref name="sell"/> takes more than the
    /// <paramref name="held"/> units, so a method never reports a negative
    /// holding for transactions a portfolio's ledger would have refused.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="sell"/> takes more than is held.</exception>
    public static void EnsureHeld(PortfolioTransaction sell, decimal held)
    {
        if (sell.Quantity > held)
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"Selling {sell.Quantity} {sell.CoinId} would take its holding below zero: only {held} would be held then."),
                TransactionsParameter);
        }
    }
}

using CryptoStuff.Core.Portfolios;

namespace CryptoStuff.Core.UnitTests.TestSupport;

/// <summary>
/// Builds the transactions a cost-basis method reduces. They are recorded
/// through a <see cref="Portfolio"/>, the only way to create them, so each
/// gets the sequence the ledger assigns.
/// </summary>
internal static class Ledger
{
    /// <summary>Records <paramref name="details"/>, in order, in a new portfolio and returns its transactions in ledger order.</summary>
    public static IReadOnlyList<PortfolioTransaction> Of(params TransactionDetails[] details)
    {
        var portfolio = Portfolio.Create("Ledger", "usd", CostBasisMethod.Fifo);
        foreach (var transaction in details)
        {
            var outcome = portfolio.AddTransaction(transaction);
            if (!outcome.IsValid)
            {
                throw new InvalidOperationException(outcome.ErrorMessage);
            }
        }

        return portfolio.Transactions;
    }

    /// <summary>Midnight UTC on the given day of January 2024.</summary>
    public static DateTimeOffset Day(int day) => new(2024, 1, day, 0, 0, 0, TimeSpan.Zero);

    public static TransactionDetails Buy(decimal quantity, decimal unitPrice, int day, decimal fee = 0m, string coinId = "bitcoin") =>
        new() { Kind = TransactionKind.Buy, CoinId = coinId, Quantity = quantity, UnitPrice = unitPrice, Fee = fee, OccurredAt = Day(day) };

    public static TransactionDetails Sell(decimal quantity, decimal unitPrice, int day, decimal fee = 0m, string coinId = "bitcoin") =>
        new() { Kind = TransactionKind.Sell, CoinId = coinId, Quantity = quantity, UnitPrice = unitPrice, Fee = fee, OccurredAt = Day(day) };
}

namespace CryptoStuff.Core.Portfolios;

/// <summary>
/// Average cost: every unit held costs the running average of what was paid
/// for the units held, so a sell releases its share of the cost basis and a
/// sell of everything releases all of it.
/// </summary>
public sealed class AverageCostMethod : ICostBasisMethod
{
    /// <inheritdoc/>
    public CoinPosition CalculatePosition(IEnumerable<PortfolioTransaction> transactions)
    {
        var held = default(ExactHolding);
        var costBasis = 0m;
        var realized = 0m;
        foreach (var transaction in CostBasisReplay.InLedgerOrder(transactions))
        {
            if (transaction.Kind == TransactionKind.Buy)
            {
                held = held.Add(transaction.Quantity);
                costBasis += (transaction.Quantity * transaction.UnitPrice) + transaction.Fee;
            }
            else
            {
                CostBasisReplay.EnsureHeld(transaction, held.Quantity);

                // Selling everything releases the whole cost basis. A share
                // of it would round where an average such as 733.33… went
                // past decimal's 28 digits, and leave a sliver of cost
                // behind on no units.
                var costSold = transaction.Quantity == held.Quantity
                    ? costBasis
                    : CostBasisReplay.ShareOf(costBasis, transaction.Quantity, held.Quantity);
                realized += (transaction.Quantity * transaction.UnitPrice) - transaction.Fee - costSold;
                held = held.Add(-transaction.Quantity);
                costBasis -= costSold;
            }
        }

        return new() { Quantity = held.Quantity, CostBasis = costBasis, RealizedProfitAndLoss = realized };
    }
}

namespace CryptoStuff.Core.Portfolios;

/// <summary>
/// First in, first out: each buy is a lot, and a sell uses up the oldest
/// lots first, releasing their cost — a pro-rata share of it from a lot it
/// only partly uses.
/// </summary>
public sealed class FifoMethod : ICostBasisMethod
{
    /// <inheritdoc/>
    public CoinPosition CalculatePosition(IEnumerable<PortfolioTransaction> transactions)
    {
        var lots = new Queue<Lot>();
        var realized = 0m;
        foreach (var transaction in CostBasisReplay.InLedgerOrder(transactions))
        {
            if (transaction.Kind == TransactionKind.Buy)
            {
                lots.Enqueue(new Lot { Quantity = transaction.Quantity, Cost = (transaction.Quantity * transaction.UnitPrice) + transaction.Fee });
            }
            else
            {
                CostBasisReplay.EnsureHeld(transaction, lots.Sum(l => l.Quantity));
                var costSold = Release(lots, transaction.Quantity);
                realized += (transaction.Quantity * transaction.UnitPrice) - transaction.Fee - costSold;
            }
        }

        return new() { Quantity = lots.Sum(l => l.Quantity), CostBasis = lots.Sum(l => l.Cost), RealizedProfitAndLoss = realized };
    }

    // Takes `quantity` units from the oldest lots and returns what they cost.
    // A lot used up entirely releases all of its cost, so its pro-rata shares
    // always add back up to exactly what it cost.
    private static decimal Release(Queue<Lot> lots, decimal quantity)
    {
        var unsold = quantity;
        var cost = 0m;
        while (unsold > 0m)
        {
            var oldest = lots.Peek();
            if (oldest.Quantity <= unsold)
            {
                lots.Dequeue();
                cost += oldest.Cost;
                unsold -= oldest.Quantity;
            }
            else
            {
                var share = oldest.Cost * unsold / oldest.Quantity;
                cost += share;
                oldest.Cost -= share;
                oldest.Quantity -= unsold;
                unsold = 0m;
            }
        }

        return cost;
    }

    // Units bought together and not yet sold, with what they cost. A sell
    // that takes only part of the oldest lot shrinks it in place.
    private sealed class Lot
    {
        public required decimal Quantity { get; set; }

        public required decimal Cost { get; set; }
    }
}

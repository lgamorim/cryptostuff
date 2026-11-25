namespace CryptoStuff.Core.Portfolios;

/// <summary>
/// The replay every lot-based cost-basis method shares: each buy is a lot,
/// and a sell uses up lots in the order the method's rule takes them,
/// releasing their cost — a pro-rata share of it from a lot it only partly
/// uses. The methods differ only in that rule.
/// </summary>
internal static class LotReplay
{
    /// <summary>
    /// Replays one coin's transactions in ledger order, as
    /// <see cref="ICostBasisMethod.CalculatePosition"/> documents, and returns
    /// the position they leave. Of the lots held when a sell is replayed, the
    /// one <paramref name="takenFirst"/> ranks lowest is used up first; the
    /// lots still held at the end are added up in the order they were bought.
    /// </summary>
    public static CoinPosition CalculatePosition(IEnumerable<PortfolioTransaction> transactions, IComparer<BoughtLot> takenFirst)
    {
        var lots = new PriorityQueue<Lot, BoughtLot>(takenFirst);
        var held = default(ExactHolding);
        var realized = 0m;
        var buys = 0;
        foreach (var transaction in CostBasisReplay.InLedgerOrder(transactions))
        {
            if (transaction.Kind == TransactionKind.Buy)
            {
                held = held.Add(transaction.Quantity);
                var bought = new BoughtLot(buys++, transaction.Quantity, (transaction.Quantity * transaction.UnitPrice) + transaction.Fee);
                lots.Enqueue(new Lot { Quantity = bought.Quantity, Cost = bought.Cost }, bought);
            }
            else
            {
                CostBasisReplay.EnsureHeld(transaction, held.Quantity);
                held = held.Add(-transaction.Quantity);
                var costSold = Release(lots, transaction.Quantity);
                realized += (transaction.Quantity * transaction.UnitPrice) - transaction.Fee - costSold;
            }
        }

        // Summed in the order the lots were bought, whatever the rule: a total
        // too long for a decimal rounds at each addition, so this keeps the
        // figures from depending on how the queue stores the lots, and with
        // nothing sold every method reports the same cost basis.
        var remaining = lots.UnorderedItems.OrderBy(item => item.Priority.Position).Select(item => item.Element).ToList();
        return new() { Quantity = remaining.Sum(l => l.Quantity), CostBasis = remaining.Sum(l => l.Cost), RealizedProfitAndLoss = realized };
    }

    // Takes `quantity` units from the lots taken first and returns what they
    // cost. A lot used up entirely releases all of its cost, so its pro-rata
    // shares always add back up to exactly what it cost.
    private static decimal Release(PriorityQueue<Lot, BoughtLot> lots, decimal quantity)
    {
        var unsold = quantity;
        var cost = 0m;
        while (unsold > 0m)
        {
            // Never empty: the holding is kept exact, so the lots hold every
            // unit the sell takes.
            var next = lots.Peek();
            if (next.Quantity <= unsold)
            {
                lots.Dequeue();
                cost += next.Cost;
                unsold -= next.Quantity;
            }
            else
            {
                var share = CostBasisReplay.ShareOf(next.Cost, unsold, next.Quantity);
                cost += share;
                next.Cost -= share;
                next.Quantity -= unsold;
                unsold = 0m;
            }
        }

        return cost;
    }

    // Units bought together and not yet sold, with what they cost. A sell
    // that takes only part of a lot shrinks it in place; its rank, which
    // comes from the lot as bought, stays the same.
    private sealed class Lot
    {
        public required decimal Quantity { get; set; }

        public required decimal Cost { get; set; }
    }
}

namespace CryptoStuff.Core.Portfolios;

/// <summary>
/// A lot as it was bought, which is all a lot-based cost-basis method's rule
/// ranks it by: its place among the coin's buys in ledger order, the units
/// bought, and what they cost with the buy's fee. None of it changes as
/// sells use the lot up.
/// </summary>
/// <param name="Position">The buy's place among the coin's buys in ledger order, from zero.</param>
/// <param name="Quantity">The units bought; always above zero.</param>
/// <param name="Cost">What the units cost, the buy's fee included.</param>
internal readonly record struct BoughtLot(int Position, decimal Quantity, decimal Cost);

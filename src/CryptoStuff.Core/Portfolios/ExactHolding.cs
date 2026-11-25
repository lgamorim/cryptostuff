using System.Numerics;

namespace CryptoStuff.Core.Portfolios;

/// <summary>
/// A coin's holding, kept exact while a cost-basis method replays its
/// transactions. The holding is counted to the most decimal places any of
/// its quantities needs, trailing zeros aside, and written to that many
/// places it always fits a decimal. Every smaller quantity a method derives
/// from it — a lot, a lot's remainder, the units a sell still has to take —
/// then fits too, so a lot-based method's lots always add up to the holding.
/// Counting the places a quantity needs, not the ones it is written with,
/// keeps the outcome the same whether a quantity reads 1 or, back from
/// storage, 1.0.
/// </summary>
internal readonly struct ExactHolding
{
    private readonly int _places;

    private ExactHolding(decimal quantity, int places)
    {
        Quantity = quantity;
        _places = places;
    }

    /// <summary>The units held; zero for the default holding.</summary>
    public decimal Quantity { get; }

    /// <summary>
    /// Returns the holding after <paramref name="change"/>: the units a buy
    /// adds, or the negated units a sell of no more than is held takes.
    /// </summary>
    /// <exception cref="OverflowException">
    /// The holding, written to the places its quantities need, would need
    /// more significant digits than a decimal holds.
    /// </exception>
    public ExactHolding Add(decimal change)
    {
        var (changeDigits, changeScale) = CostBasisReplay.Decompose(change);
        var places = Math.Max(_places, WithoutTrailingZeros(changeDigits, changeScale).Scale);

        var (quantityDigits, quantityScale) = CostBasisReplay.Decompose(Quantity);
        var scale = Math.Max(quantityScale, changeScale);
        var sum = (quantityDigits * BigInteger.Pow(10, scale - quantityScale)) + (changeDigits * BigInteger.Pow(10, scale - changeScale));

        // The sum needs no more places than its quantities, so it is exact
        // at `places`.
        var (sumDigits, sumScale) = WithoutTrailingZeros(sum, scale);
        if (sumDigits * BigInteger.Pow(10, places - sumScale) > CostBasisReplay.MaxDigits)
        {
            throw new OverflowException("The holding would need more significant digits than a decimal holds.");
        }

        return new(Quantity + change, places);
    }

    private static (BigInteger Digits, int Scale) WithoutTrailingZeros(BigInteger digits, int scale)
    {
        while (scale > 0 && (digits % 10).IsZero)
        {
            digits /= 10;
            scale--;
        }

        return (digits, scale);
    }
}

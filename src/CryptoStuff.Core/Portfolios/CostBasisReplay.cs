using System.Globalization;
using System.Numerics;

namespace CryptoStuff.Core.Portfolios;

/// <summary>
/// What every cost-basis method needs before and during its replay: one
/// coin's transactions in ledger order, a check that a sell never takes
/// more than is held, and the exact decimal arithmetic a share and an
/// <see cref="ExactHolding"/> are worked out with.
/// </summary>
internal static class CostBasisReplay
{
    private const string TransactionsParameter = "transactions";

    // The most digits after the point a decimal can have.
    private const int MaxScale = 28;

    // A decimal's digits are a 96-bit unsigned integer.
    public static readonly BigInteger MaxDigits = (BigInteger.One << 96) - 1;

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
    /// Returns the share of <paramref name="amount"/>, which is not negative,
    /// that <paramref name="part"/> of <paramref name="whole"/> units carries,
    /// where <paramref name="part"/> is less than <paramref name="whole"/>. The
    /// share is worked out exactly and rounded once, half to even, to the
    /// most digits a decimal can hold, so it never exceeds
    /// <paramref name="amount"/> and fits a decimal whenever
    /// <paramref name="amount"/> does. Multiplying the decimals before
    /// dividing could overflow, or round away digits, on the way; dividing
    /// first loses digits whenever the amount per unit is below one.
    /// </summary>
    public static decimal ShareOf(decimal amount, decimal part, decimal whole)
    {
        var (amountDigits, amountScale) = Decompose(amount);
        var (partDigits, partScale) = Decompose(part);
        var (wholeDigits, wholeScale) = Decompose(whole);

        // A decimal is its digits over a power of ten, so the share is
        // (amount × part × 10^wholeScale) ÷ (whole × 10^(amountScale + partScale)).
        var numerator = amountDigits * partDigits * BigInteger.Pow(10, wholeScale);
        var denominator = wholeDigits * BigInteger.Pow(10, amountScale + partScale);

        // Decimal division keeps an exact quotient's trailing zeros down to
        // this scale and a rounded one's not at all; doing the same shows
        // every share as decimal's own operators would have shown it.
        var preferredScale = Math.Clamp(amountScale + partScale - wholeScale, 0, MaxScale);
        return RoundToDecimal(numerator, denominator, preferredScale);
    }

    /// <summary>
    /// The one oversell rule, shared by a portfolio's ledger and every
    /// cost-basis method: returns why <paramref name="sell"/> cannot be made
    /// from the <paramref name="held"/> units, naming its coin and date, or
    /// null when they cover it.
    /// </summary>
    public static string? OversellDetail(PortfolioTransaction sell, decimal held) =>
        sell.Quantity > held
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"Selling {sell.Quantity} {sell.CoinId} on {UtcTimestamp.Format(sell.OccurredAt)} would take its holding below zero: only {held} would be held then.")
            : null;

    /// <summary>
    /// Throws when <paramref name="sell"/> takes more than the
    /// <paramref name="held"/> units, so a method never reports a negative
    /// holding for transactions a portfolio's ledger would have refused.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="sell"/> takes more than is held.</exception>
    public static void EnsureHeld(PortfolioTransaction sell, decimal held)
    {
        if (OversellDetail(sell, held) is { } oversold)
        {
            throw new ArgumentException(oversold, TransactionsParameter);
        }
    }

    /// <summary>Splits <paramref name="value"/> into its digits, signed, and the decimal places they are written with.</summary>
    public static (BigInteger Digits, int Scale) Decompose(decimal value)
    {
        Span<int> bits = stackalloc int[4];
        decimal.GetBits(value, bits);
        var digits = ((BigInteger)(uint)bits[2] << 64) | ((BigInteger)(uint)bits[1] << 32) | (uint)bits[0];
        return (value < 0m ? -digits : digits, value.Scale);
    }

    // Rounds numerator ÷ denominator, both positive, half to even at the finest scale whose
    // digits fit a decimal's 96 bits, then drops trailing zeros: down to
    // preferredScale when the quotient is exact, and all of them when it
    // was rounded.
    private static decimal RoundToDecimal(BigInteger numerator, BigInteger denominator, int preferredScale)
    {
        for (var scale = MaxScale; scale >= 0; scale--)
        {
            var quotient = BigInteger.DivRem(numerator * BigInteger.Pow(10, scale), denominator, out var remainder);
            var twiceRemainder = remainder * 2;
            if (twiceRemainder > denominator || (twiceRemainder == denominator && !quotient.IsEven))
            {
                quotient++;
            }

            if (quotient > MaxDigits)
            {
                continue;
            }

            var keptScale = remainder.IsZero ? preferredScale : 0;
            while (scale > keptScale && (quotient % 10).IsZero)
            {
                quotient /= 10;
                scale--;
            }

            return new decimal(
                (int)(uint)(quotient & uint.MaxValue),
                (int)(uint)((quotient >> 32) & uint.MaxValue),
                (int)(uint)(quotient >> 64),
                false,
                (byte)scale);
        }

        throw new OverflowException("The share is too large for a decimal.");
    }
}

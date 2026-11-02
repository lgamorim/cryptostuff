namespace CryptoStuff.Core.Portfolios;

/// <summary>One buy or sell in a portfolio's ledger.</summary>
public sealed class PortfolioTransaction
{
    private PortfolioTransaction(int sequence, TransactionKind kind, string coinId, decimal quantity, decimal unitPrice, decimal fee, DateTimeOffset occurredAt)
    {
        Sequence = sequence;
        Kind = kind;
        CoinId = coinId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        Fee = fee;
        OccurredAt = occurredAt;
    }

    /// <summary>The transaction's id; zero until its portfolio is saved.</summary>
    public int Id { get; private set; }

    /// <summary>
    /// The transaction's position in the order its portfolio recorded it,
    /// counting from 1. It orders transactions that happened at the same
    /// instant, and never changes when the transaction is replaced.
    /// </summary>
    public int Sequence { get; private set; }

    /// <summary>Whether the transaction buys or sells.</summary>
    public TransactionKind Kind { get; private set; }

    /// <summary>The coin bought or sold.</summary>
    public string CoinId { get; private set; }

    /// <summary>How many units were bought or sold.</summary>
    public decimal Quantity { get; private set; }

    /// <summary>The price of one unit, in the portfolio's vs_currency.</summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>The fee paid, in the portfolio's vs_currency.</summary>
    public decimal Fee { get; private set; }

    /// <summary>When the transaction happened.</summary>
    public DateTimeOffset OccurredAt { get; private set; }

    internal static PortfolioTransaction Create(int sequence, TransactionDetails details)
    {
        EnsureValid(details);
        return new(sequence, details.Kind, NormalizeCoinId(details.CoinId), details.Quantity, details.UnitPrice, details.Fee, details.OccurredAt.ToUniversalTime());
    }

    /// <summary>The form in which a coin id is stored and compared.</summary>
    internal static string NormalizeCoinId(string coinId) => coinId.ToLowerInvariant();

    /// <summary>
    /// Takes every detail of <paramref name="replacement"/>, a transaction
    /// already built by <see cref="Create"/>, keeping this one's identity and
    /// <see cref="Sequence"/>.
    /// </summary>
    internal void ReplaceWith(PortfolioTransaction replacement)
    {
        Kind = replacement.Kind;
        CoinId = replacement.CoinId;
        Quantity = replacement.Quantity;
        UnitPrice = replacement.UnitPrice;
        Fee = replacement.Fee;
        OccurredAt = replacement.OccurredAt;
    }

    /// <summary>Throws when <paramref name="details"/> breaks a rule <see cref="TransactionDetails"/> documents.</summary>
    /// <exception cref="ArgumentException"><paramref name="details"/> breaks a rule.</exception>
    internal static void EnsureValid(TransactionDetails details)
    {
        if (!Enum.IsDefined(details.Kind))
        {
            throw new ArgumentOutOfRangeException(nameof(details), details.Kind, "The transaction kind is not defined.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(details.CoinId, nameof(details));
        Ensure(PositiveDecimalValidator.Validate(details.Quantity, nameof(details.Quantity)));
        Ensure(NonNegativeDecimalValidator.Validate(details.UnitPrice, nameof(details.UnitPrice)));
        Ensure(NonNegativeDecimalValidator.Validate(details.Fee, nameof(details.Fee)));
    }

    private static void Ensure(ValidationOutcome outcome)
    {
        if (!outcome.IsValid)
        {
            throw new ArgumentException(outcome.ErrorMessage, "details");
        }
    }
}

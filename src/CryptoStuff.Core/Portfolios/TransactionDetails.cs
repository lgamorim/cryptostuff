namespace CryptoStuff.Core.Portfolios;

/// <summary>What a caller supplies to record a portfolio transaction, or to replace one.</summary>
public sealed record TransactionDetails
{
    /// <summary>Whether the transaction buys or sells.</summary>
    public required TransactionKind Kind { get; init; }

    /// <summary>The CoinGecko coin id, in any case; it is stored lowercased.</summary>
    public required string CoinId { get; init; }

    /// <summary>How many units were bought or sold; must satisfy <see cref="PositiveDecimalValidator"/>.</summary>
    public required decimal Quantity { get; init; }

    /// <summary>The price of one unit, in the portfolio's vs_currency; must satisfy <see cref="NonNegativeDecimalValidator"/>.</summary>
    public required decimal UnitPrice { get; init; }

    /// <summary>The fee paid, in the portfolio's vs_currency; must satisfy <see cref="NonNegativeDecimalValidator"/>. Zero when omitted.</summary>
    public decimal Fee { get; init; }

    /// <summary>When the transaction happened, at any offset; it is stored as UTC.</summary>
    public required DateTimeOffset OccurredAt { get; init; }
}

namespace CryptoStuff.Core;

/// <summary>An amount denominated in a specific currency.</summary>
public sealed record CurrencyAmount
{
    /// <summary>The lowercase vs_currency code (e.g. "usd").</summary>
    public required string Currency { get; init; }

    /// <summary>The amount in <see cref="Currency"/>.</summary>
    public required decimal Amount { get; init; }
}

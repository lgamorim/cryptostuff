namespace CryptoStuff.Core.Portfolios;

/// <summary>Parses the text tokens a <see cref="TransactionKind"/> travels as, so both hosts accept identical input.</summary>
public static class TransactionKindParser
{
    /// <summary>
    /// Checks that <paramref name="token"/> parses under <see cref="TryParse"/>.
    /// <paramref name="fieldName"/> is used to build the failure message.
    /// </summary>
    public static ValidationOutcome Validate(string? token, string fieldName) =>
        TryParse(token, out _)
            ? ValidationOutcome.Success()
            : ValidationOutcome.Failure($"{fieldName} must be 'buy' or 'sell'.");

    /// <summary>
    /// Parses <paramref name="token"/>, <c>buy</c> or <c>sell</c> in any case,
    /// as a transaction kind. Surrounding whitespace and numbers are not
    /// accepted.
    /// </summary>
    public static bool TryParse(string? token, out TransactionKind kind)
    {
        switch (token?.ToLowerInvariant())
        {
            case "buy":
                kind = TransactionKind.Buy;
                return true;
            case "sell":
                kind = TransactionKind.Sell;
                return true;
            default:
                kind = default;
                return false;
        }
    }
}

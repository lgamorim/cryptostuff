namespace CryptoStuff.Core.Alerts;

/// <summary>Parses the text tokens an <see cref="AlertDirection"/> travels as, so both hosts accept identical input.</summary>
public static class AlertDirectionParser
{
    /// <summary>
    /// Checks that <paramref name="token"/> parses under <see cref="TryParse"/>.
    /// <paramref name="fieldName"/> is used to build the failure message.
    /// </summary>
    public static ValidationOutcome Validate(string? token, string fieldName) =>
        TryParse(token, out _)
            ? ValidationOutcome.Success()
            : ValidationOutcome.Failure($"{fieldName} must be 'above' or 'below'.");

    /// <summary>
    /// Parses <paramref name="token"/>, <c>above</c> or <c>below</c> in any
    /// case, as an alert direction. Surrounding whitespace and numbers are not
    /// accepted.
    /// </summary>
    public static bool TryParse(string? token, out AlertDirection direction)
    {
        switch (token?.ToLowerInvariant())
        {
            case "above":
                direction = AlertDirection.Above;
                return true;
            case "below":
                direction = AlertDirection.Below;
                return true;
            default:
                direction = default;
                return false;
        }
    }
}

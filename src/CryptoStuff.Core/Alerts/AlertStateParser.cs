namespace CryptoStuff.Core.Alerts;

/// <summary>Parses the text tokens an <see cref="AlertState"/> travels as, so both hosts accept identical input.</summary>
public static class AlertStateParser
{
    /// <summary>
    /// Checks that <paramref name="token"/> parses under <see cref="TryParse"/>.
    /// <paramref name="fieldName"/> is used to build the failure message.
    /// </summary>
    public static ValidationOutcome Validate(string? token, string fieldName) =>
        TryParse(token, out _)
            ? ValidationOutcome.Success()
            : ValidationOutcome.Failure($"{fieldName} must be 'armed' or 'triggered'.");

    /// <summary>
    /// Parses <paramref name="token"/>, <c>armed</c> or <c>triggered</c> in any
    /// case, as an alert state. Surrounding whitespace and numbers are not
    /// accepted.
    /// </summary>
    public static bool TryParse(string? token, out AlertState state)
    {
        switch (token?.ToLowerInvariant())
        {
            case "armed":
                state = AlertState.Armed;
                return true;
            case "triggered":
                state = AlertState.Triggered;
                return true;
            default:
                state = default;
                return false;
        }
    }
}

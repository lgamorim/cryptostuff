namespace CryptoStuff.Core;

/// <summary>Validates that a day-count input is a positive number of days.</summary>
public static class PositiveDayCountValidator
{
    /// <summary>
    /// Checks that <paramref name="days"/> is strictly greater than zero.
    /// <paramref name="fieldName"/> is used to build the failure message.
    /// </summary>
    public static ValidationOutcome Validate(int days, string fieldName) =>
        days > 0
            ? ValidationOutcome.Success()
            : ValidationOutcome.Failure($"{fieldName} must be a positive number of days.");
}

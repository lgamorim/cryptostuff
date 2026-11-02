namespace CryptoStuff.Core;

/// <summary>Validates that a decimal input, such as a quantity, is strictly positive.</summary>
public static class PositiveDecimalValidator
{
    /// <summary>
    /// Checks that <paramref name="value"/> is strictly greater than zero.
    /// <paramref name="fieldName"/> is used to build the failure message.
    /// </summary>
    public static ValidationOutcome Validate(decimal value, string fieldName) =>
        value > 0m
            ? ValidationOutcome.Success()
            : ValidationOutcome.Failure($"{fieldName} must be greater than zero.");
}

namespace CryptoStuff.Core;

/// <summary>Validates that a decimal input, such as a price or a fee, is not negative.</summary>
public static class NonNegativeDecimalValidator
{
    /// <summary>
    /// Checks that <paramref name="value"/> is zero or greater.
    /// <paramref name="fieldName"/> is used to build the failure message.
    /// </summary>
    public static ValidationOutcome Validate(decimal value, string fieldName) =>
        value >= 0m
            ? ValidationOutcome.Success()
            : ValidationOutcome.Failure($"{fieldName} must be zero or greater.");
}

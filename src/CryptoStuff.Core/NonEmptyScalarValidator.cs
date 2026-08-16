namespace CryptoStuff.Core;

/// <summary>Validates that a scalar string input was actually supplied.</summary>
public static class NonEmptyScalarValidator
{
    /// <summary>
    /// Checks that <paramref name="value"/> is neither null, empty, nor
    /// whitespace-only. <paramref name="fieldName"/> is used to build the
    /// failure message.
    /// </summary>
    public static ValidationOutcome Validate(string? value, string fieldName) =>
        string.IsNullOrWhiteSpace(value)
            ? ValidationOutcome.Failure($"{fieldName} is required.")
            : ValidationOutcome.Success();
}

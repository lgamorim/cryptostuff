namespace CryptoStuff.Core;

/// <summary>Validates that a collection input contains at least one element.</summary>
public static class NonEmptyCollectionValidator
{
    /// <summary>
    /// Checks that <paramref name="values"/> is neither null nor empty. Does
    /// not inspect individual elements — element-level checks (e.g. that no
    /// entry is itself blank) are <see cref="NonEmptyScalarValidator"/>'s
    /// responsibility, composed by the caller per element. <paramref name="fieldName"/>
    /// is used to build the failure message.
    /// </summary>
    public static ValidationOutcome Validate(IReadOnlyCollection<string>? values, string fieldName) =>
        values is null || values.Count == 0
            ? ValidationOutcome.Failure($"{fieldName} must contain at least one value.")
            : ValidationOutcome.Success();
}

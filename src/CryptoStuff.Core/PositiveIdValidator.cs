namespace CryptoStuff.Core;

/// <summary>Validates that a stored-resource id input could identify a record.</summary>
public static class PositiveIdValidator
{
    /// <summary>
    /// Checks that <paramref name="id"/> is strictly greater than zero, the
    /// range the database's autoincrement keys are drawn from.
    /// <paramref name="fieldName"/> is used to build the failure message.
    /// </summary>
    public static ValidationOutcome Validate(int id, string fieldName) =>
        id > 0
            ? ValidationOutcome.Success()
            : ValidationOutcome.Failure($"{fieldName} must be a positive whole number.");
}

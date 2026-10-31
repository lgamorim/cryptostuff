namespace CryptoStuff.Core;

/// <summary>Validates a user-chosen name for a stored resource, such as a watchlist.</summary>
public static class NameValidator
{
    /// <summary>The longest name accepted, in UTF-16 code units.</summary>
    public const int MaxLength = 100;

    /// <summary>
    /// Checks that <paramref name="value"/> is neither null, empty, nor
    /// whitespace-only, and is at most <see cref="MaxLength"/> characters.
    /// <paramref name="fieldName"/> is used to build the failure message.
    /// </summary>
    public static ValidationOutcome Validate(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ValidationOutcome.Failure($"{fieldName} is required.");
        }

        return value.Length > MaxLength
            ? ValidationOutcome.Failure($"{fieldName} must be at most {MaxLength} characters.")
            : ValidationOutcome.Success();
    }
}

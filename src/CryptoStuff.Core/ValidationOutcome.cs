namespace CryptoStuff.Core;

/// <summary>
/// The outcome of a validation check: either valid, or invalid with a
/// human-readable reason.
/// </summary>
public sealed record ValidationOutcome
{
    /// <summary>Whether the checked input satisfied the rule.</summary>
    public bool IsValid { get; }

    /// <summary>Why the input failed the rule. Null when <see cref="IsValid"/> is true.</summary>
    public string? ErrorMessage { get; }

    private ValidationOutcome(bool isValid, string? errorMessage)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }

    /// <summary>Creates a result indicating the input satisfied the rule.</summary>
    public static ValidationOutcome Success() => new(isValid: true, errorMessage: null);

    /// <summary>Creates a result indicating the input violated the rule, carrying why.</summary>
    public static ValidationOutcome Failure(string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        return new(isValid: false, errorMessage: errorMessage);
    }
}

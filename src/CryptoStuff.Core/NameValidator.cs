namespace CryptoStuff.Core;

/// <summary>
/// Validates a user-chosen name for a stored resource, such as a watchlist,
/// and gives the form it is stored and compared in.
/// </summary>
public static class NameValidator
{
    /// <summary>The longest name accepted, in UTF-16 code units, once trimmed.</summary>
    public const int MaxLength = 100;

    /// <summary>
    /// Checks that <paramref name="value"/>, once its leading and trailing
    /// whitespace is trimmed, is not empty, holds no control characters (which
    /// could rewrite a terminal that prints the name), and is at most
    /// <see cref="MaxLength"/> characters. <paramref name="fieldName"/> is used
    /// to build the failure message.
    /// </summary>
    public static ValidationOutcome Validate(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ValidationOutcome.Failure($"{fieldName} is required.");
        }

        var name = value.Trim();
        if (name.Any(char.IsControl))
        {
            return ValidationOutcome.Failure($"{fieldName} must not contain control characters.");
        }

        return name.Length > MaxLength
            ? ValidationOutcome.Failure($"{fieldName} must be at most {MaxLength} characters.")
            : ValidationOutcome.Success();
    }

    /// <summary>
    /// The form a name is stored and compared in: <paramref name="name"/>
    /// without its leading and trailing whitespace, so <c>"Main "</c> and
    /// <c>"Main"</c> are the same name.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="name"/> fails <see cref="Validate"/>.</exception>
    public static string Normalize(string name)
    {
        var outcome = Validate(name, nameof(name));
        if (!outcome.IsValid)
        {
            throw new ArgumentException(outcome.ErrorMessage, nameof(name));
        }

        return name.Trim();
    }
}

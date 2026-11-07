using System.Globalization;

namespace CryptoStuff.Core;

/// <summary>Validates that a timestamp input is a well-formed ISO 8601 date and time with a UTC offset.</summary>
public static class TimestampFormatValidator
{
    // Optional fractional seconds (up to the 7 digits DateTimeOffset holds),
    // then a literal Z or a numeric offset. The general K specifier is avoided
    // because it also matches no offset at all.
    private static readonly string[] Formats = [UtcTimestamp.Pattern, "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz"];

    /// <summary>
    /// Checks that <paramref name="value"/> parses under <see cref="TryParse"/>.
    /// <paramref name="fieldName"/> is used to build the failure message.
    /// </summary>
    public static ValidationOutcome Validate(string? value, string fieldName) =>
        TryParse(value, out _)
            ? ValidationOutcome.Success()
            : ValidationOutcome.Failure($"{fieldName} must be an ISO 8601 timestamp with a UTC offset, such as 2024-01-31T14:30:00Z.");

    /// <summary>
    /// Parses <paramref name="value"/> as <c>yyyy-MM-ddTHH:mm:ss</c>, with
    /// optional fractional seconds, followed by <c>Z</c> or a <c>±hh:mm</c>
    /// offset, under the invariant culture. On success,
    /// <paramref name="timestamp"/> is the same instant with a zero offset.
    /// </summary>
    public static bool TryParse(string? value, out DateTimeOffset timestamp)
    {
        if (!DateTimeOffset.TryParseExact(value, Formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            timestamp = default;
            return false;
        }

        timestamp = parsed.ToUniversalTime();
        return true;
    }
}

using System.Globalization;

namespace CryptoStuff.Core;

/// <summary>Validates that a date input is well-formed in CoinGecko's <c>dd-MM-yyyy</c> format.</summary>
public static class DateFormatValidator
{
    private const string Format = "dd-MM-yyyy";

    /// <summary>
    /// Checks that <paramref name="value"/> parses exactly as <c>dd-MM-yyyy</c>
    /// under the invariant culture, rejecting malformed strings and
    /// impossible calendar dates alike. <paramref name="fieldName"/> is used
    /// to build the failure message.
    /// </summary>
    public static ValidationOutcome Validate(string? value, string fieldName) =>
        DateOnly.TryParseExact(value, Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
            ? ValidationOutcome.Success()
            : ValidationOutcome.Failure($"{fieldName} must be a valid date in dd-MM-yyyy format.");
}

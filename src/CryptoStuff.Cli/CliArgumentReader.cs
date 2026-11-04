using System.Globalization;
using CryptoStuff.Core;
using CryptoStuff.Core.Portfolios;

namespace CryptoStuff.Cli;

/// <summary>
/// Turns a command's raw positional arguments into typed values, reporting
/// the first problem as the message the CLI prints.
/// </summary>
public static class CliArgumentReader
{
    // A CLI positional argument is never null, and string.Split always returns
    // at least one element (an empty string splits to [""]), so a genuinely
    // empty list can never reach this method - only per-element blank entries
    // can. NonEmptyCollectionValidator is therefore not invoked here; the
    // per-element NonEmptyScalarValidator check below already rejects a blank
    // or all-blank argument.
    /// <summary>Splits a comma-separated argument into trimmed entries, rejecting any blank one.</summary>
    public static bool TryParseIdentifierList(string raw, string fieldName, out IReadOnlyList<string> values, out string errorMessage)
    {
        var split = raw.Split(',').Select(value => value.Trim()).ToArray();

        foreach (var value in split)
        {
            if (!NonEmptyScalarValidator.Validate(value, fieldName).IsValid)
            {
                values = [];
                errorMessage = $"{fieldName} contains a blank entry.";
                return false;
            }
        }

        values = split;
        errorMessage = string.Empty;
        return true;
    }

    /// <summary>Checks that <paramref name="value"/> is not blank.</summary>
    public static bool TryValidateScalar(string value, string fieldName, out string errorMessage)
    {
        var outcome = NonEmptyScalarValidator.Validate(value, fieldName);
        errorMessage = outcome.ErrorMessage ?? string.Empty;
        return outcome.IsValid;
    }

    /// <summary>Checks that <paramref name="value"/> is a valid user-chosen resource name.</summary>
    public static bool TryValidateName(string value, string fieldName, out string errorMessage)
    {
        var outcome = NameValidator.Validate(value, fieldName);
        errorMessage = outcome.ErrorMessage ?? string.Empty;
        return outcome.IsValid;
    }

    /// <summary>Checks that <paramref name="value"/> is a non-blank, well-formed <c>dd-MM-yyyy</c> date.</summary>
    public static bool TryValidateDate(string value, string fieldName, out string errorMessage)
    {
        var scalarOutcome = NonEmptyScalarValidator.Validate(value, fieldName);
        if (!scalarOutcome.IsValid)
        {
            // ValidationOutcome guarantees ErrorMessage is non-null when IsValid is false.
            errorMessage = scalarOutcome.ErrorMessage!;
            return false;
        }

        var dateOutcome = DateFormatValidator.Validate(value, fieldName);
        errorMessage = dateOutcome.ErrorMessage ?? string.Empty;
        return dateOutcome.IsValid;
    }

    /// <summary>Parses <paramref name="raw"/> as a positive whole-number resource id.</summary>
    public static bool TryParseId(string raw, string fieldName, out int id, out string errorMessage)
    {
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
        {
            errorMessage = $"{fieldName} must be a whole number.";
            return false;
        }

        var outcome = PositiveIdValidator.Validate(id, fieldName);
        errorMessage = outcome.ErrorMessage ?? string.Empty;
        return outcome.IsValid;
    }

    /// <summary>Parses <paramref name="raw"/> as a positive whole number of days.</summary>
    public static bool TryValidateDays(string raw, out int days, out string errorMessage)
    {
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out days))
        {
            errorMessage = "Days must be a whole number.";
            return false;
        }

        var outcome = PositiveDayCountValidator.Validate(days, "Days");
        errorMessage = outcome.ErrorMessage ?? string.Empty;
        return outcome.IsValid;
    }

    /// <summary>Parses <paramref name="raw"/> as the <c>average-cost</c> or <c>fifo</c> token of a cost-basis method.</summary>
    public static bool TryParseCostBasisMethod(string raw, string fieldName, out CostBasisMethod method, out string errorMessage)
    {
        var outcome = CostBasisMethodParser.Validate(raw, fieldName);
        errorMessage = outcome.ErrorMessage ?? string.Empty;
        method = default;
        return outcome.IsValid && CostBasisMethodParser.TryParse(raw, out method);
    }

    /// <summary>
    /// Parses <paramref name="raw"/> as a plain decimal number, such as
    /// <c>0.5</c>, under the invariant culture: a point for the decimal mark,
    /// no thousands separators, exponent, or whitespace, so a comma is never
    /// read as either.
    /// </summary>
    public static bool TryParseDecimal(string raw, string fieldName, out decimal value, out string errorMessage)
    {
        errorMessage = $"{fieldName} must be a number.";
        return decimal.TryParse(raw, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>Parses <paramref name="raw"/> as a decimal number greater than zero, such as a quantity.</summary>
    public static bool TryParsePositiveDecimal(string raw, string fieldName, out decimal value, out string errorMessage)
    {
        if (!TryParseDecimal(raw, fieldName, out value, out errorMessage))
        {
            return false;
        }

        var outcome = PositiveDecimalValidator.Validate(value, fieldName);
        errorMessage = outcome.ErrorMessage ?? string.Empty;
        return outcome.IsValid;
    }

    /// <summary>Parses <paramref name="raw"/> as a decimal number that is zero or greater, such as a price or a fee.</summary>
    public static bool TryParseNonNegativeDecimal(string raw, string fieldName, out decimal value, out string errorMessage)
    {
        if (!TryParseDecimal(raw, fieldName, out value, out errorMessage))
        {
            return false;
        }

        var outcome = NonNegativeDecimalValidator.Validate(value, fieldName);
        errorMessage = outcome.ErrorMessage ?? string.Empty;
        return outcome.IsValid;
    }

    /// <summary>Parses <paramref name="raw"/> as the <c>buy</c> or <c>sell</c> token of a transaction kind.</summary>
    public static bool TryParseTransactionKind(string raw, string fieldName, out TransactionKind kind, out string errorMessage)
    {
        var outcome = TransactionKindParser.Validate(raw, fieldName);
        errorMessage = outcome.ErrorMessage ?? string.Empty;
        kind = default;
        return outcome.IsValid && TransactionKindParser.TryParse(raw, out kind);
    }

    /// <summary>Parses <paramref name="raw"/> as an ISO 8601 timestamp with a UTC offset, returned as the same instant in UTC.</summary>
    public static bool TryParseTimestamp(string raw, string fieldName, out DateTimeOffset timestamp, out string errorMessage)
    {
        var outcome = TimestampFormatValidator.Validate(raw, fieldName);
        errorMessage = outcome.ErrorMessage ?? string.Empty;
        timestamp = default;
        return outcome.IsValid && TimestampFormatValidator.TryParse(raw, out timestamp);
    }
}

using System.Globalization;
using CryptoStuff.Core;
using CryptoStuff.Core.Alerts;
using CryptoStuff.Core.Portfolios;

namespace CryptoStuff.Cli;

/// <summary>
/// Turns a command's raw positional arguments into typed values, reporting
/// the first problem as the message the CLI prints.
/// </summary>
public static class CliArgumentReader
{
    private const NumberStyles DecimalStyles = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent;

    private const string NumberCharacters = "0123456789+-.eE";

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
    public static bool TryParseCostBasisMethod(string raw, string fieldName, out CostBasisMethod method, out string errorMessage) =>
        TryParseText(raw, fieldName, CostBasisMethodParser.TryParse, CostBasisMethodParser.Validate, out method, out errorMessage);

    /// <summary>
    /// Parses <paramref name="raw"/> as a decimal number, such as <c>0.5</c>
    /// or <c>5e-1</c>, under the invariant culture: a point for the decimal
    /// mark, an optional exponent as JSON allows one, and no thousands
    /// separators or whitespace, so a comma is never read as either. A
    /// well-formed number beyond what a <see cref="decimal"/> holds is
    /// reported as too large or too small rather than as not a number.
    /// </summary>
    public static bool TryParseDecimal(string raw, string fieldName, out decimal value, out string errorMessage)
    {
        if (decimal.TryParse(raw, DecimalStyles, CultureInfo.InvariantCulture, out value))
        {
            errorMessage = string.Empty;
            return true;
        }

        // double reads the same grammar over a far wider range, so a number it
        // reads that decimal does not is out of range. Its infinity and NaN
        // symbols are letters other than an exponent's, and are not numbers.
        var outOfRange = raw.AsSpan().IndexOfAnyExcept(NumberCharacters) < 0
            && double.TryParse(raw, DecimalStyles, CultureInfo.InvariantCulture, out _);
        errorMessage = !outOfRange
            ? $"{fieldName} must be a number."
            : raw.StartsWith('-')
                ? string.Create(CultureInfo.InvariantCulture, $"{fieldName} is too small; the smallest number accepted is {decimal.MinValue}.")
                : string.Create(CultureInfo.InvariantCulture, $"{fieldName} is too large; the largest number accepted is {decimal.MaxValue}.");
        return false;
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
    public static bool TryParseTransactionKind(string raw, string fieldName, out TransactionKind kind, out string errorMessage) =>
        TryParseText(raw, fieldName, TransactionKindParser.TryParse, TransactionKindParser.Validate, out kind, out errorMessage);

    /// <summary>Parses <paramref name="raw"/> as the <c>above</c> or <c>below</c> token of an alert direction.</summary>
    public static bool TryParseAlertDirection(string raw, string fieldName, out AlertDirection direction, out string errorMessage) =>
        TryParseText(raw, fieldName, AlertDirectionParser.TryParse, AlertDirectionParser.Validate, out direction, out errorMessage);

    /// <summary>Parses <paramref name="raw"/> as an ISO 8601 timestamp with a UTC offset, returned as the same instant in UTC.</summary>
    public static bool TryParseTimestamp(string raw, string fieldName, out DateTimeOffset timestamp, out string errorMessage) =>
        TryParseText(raw, fieldName, TimestampFormatValidator.TryParse, TimestampFormatValidator.Validate, out timestamp, out errorMessage);

    private delegate bool TextParser<TValue>(string? text, out TValue value);

    // Parses once. Only text the parser rejects is validated, for its
    // message: each Core parser's Validate fails exactly where its TryParse does.
    private static bool TryParseText<TValue>(
        string raw,
        string fieldName,
        TextParser<TValue> tryParse,
        Func<string?, string, ValidationOutcome> validate,
        out TValue value,
        out string errorMessage)
    {
        if (tryParse(raw, out value))
        {
            errorMessage = string.Empty;
            return true;
        }

        // ValidationOutcome guarantees ErrorMessage is non-null when IsValid is false.
        errorMessage = validate(raw, fieldName).ErrorMessage!;
        return false;
    }
}

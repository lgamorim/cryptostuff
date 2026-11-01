using System.Globalization;
using CryptoStuff.Core;

namespace CryptoStuff.Api;

/// <summary>
/// Turns raw route and query values into typed ones, reporting the first
/// problem as the <c>400</c> validation-problem result the API returns.
/// </summary>
internal static class RequestValidation
{
    // A query/path string value is never null-and-empty at once, and string.Split always
    // returns at least one element (an empty string splits to [""]), so a genuinely empty
    // list can never reach the per-element loop below - only a blank entry can. This mirrors
    // CliRunner.TryParseIdentifierList's identical reasoning for the same reason:
    // NonEmptyCollectionValidator's zero-element branch is unreachable from this call site.
    public static (IReadOnlyList<string> Values, IResult? Problem) ParseIdentifierList(string? raw, string fieldName)
    {
        var scalarProblem = ValidateScalar(raw, fieldName);
        if (scalarProblem is not null)
        {
            return ([], scalarProblem);
        }

        // ValidateScalar returning null guarantees raw is non-null and non-whitespace.
        var split = raw!.Split(',').Select(value => value.Trim()).ToArray();

        foreach (var value in split)
        {
            if (!NonEmptyScalarValidator.Validate(value, fieldName).IsValid)
            {
                return ([], ValidationProblem(fieldName, $"{fieldName} contains a blank entry."));
            }
        }

        return (split, null);
    }

    public static IResult? ValidateScalar(string? value, string fieldName)
    {
        var outcome = NonEmptyScalarValidator.Validate(value, fieldName);

        // ValidationOutcome guarantees ErrorMessage is non-null when IsValid is false.
        return outcome.IsValid ? null : ValidationProblem(fieldName, outcome.ErrorMessage!);
    }

    public static IResult? ValidateName(string? value, string fieldName)
    {
        var outcome = NameValidator.Validate(value, fieldName);

        // ValidationOutcome guarantees ErrorMessage is non-null when IsValid is false.
        return outcome.IsValid ? null : ValidationProblem(fieldName, outcome.ErrorMessage!);
    }

    public static IResult? ValidateId(int id, string fieldName)
    {
        var outcome = PositiveIdValidator.Validate(id, fieldName);

        // ValidationOutcome guarantees ErrorMessage is non-null when IsValid is false.
        return outcome.IsValid ? null : ValidationProblem(fieldName, outcome.ErrorMessage!);
    }

    public static IResult? ValidateDate(string? value, string fieldName)
    {
        var scalarProblem = ValidateScalar(value, fieldName);
        if (scalarProblem is not null)
        {
            return scalarProblem;
        }

        var dateOutcome = DateFormatValidator.Validate(value, fieldName);

        // ValidationOutcome guarantees ErrorMessage is non-null when IsValid is false.
        return dateOutcome.IsValid ? null : ValidationProblem(fieldName, dateOutcome.ErrorMessage!);
    }

    public static (int Days, IResult? Problem) ParseDays(string? raw)
    {
        var scalarProblem = ValidateScalar(raw, "days");
        if (scalarProblem is not null)
        {
            return (0, scalarProblem);
        }

        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var days))
        {
            return (0, ValidationProblem("days", "days must be a whole number."));
        }

        var outcome = PositiveDayCountValidator.Validate(days, "days");

        // ValidationOutcome guarantees ErrorMessage is non-null when IsValid is false.
        return outcome.IsValid ? (days, null) : (0, ValidationProblem("days", outcome.ErrorMessage!));
    }

    public static IResult ValidationProblem(string fieldName, string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [fieldName] = [message] });
}

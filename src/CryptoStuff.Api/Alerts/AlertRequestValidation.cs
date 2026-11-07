using CryptoStuff.Core.Alerts;

namespace CryptoStuff.Api.Alerts;

/// <summary>
/// Turns the alert routes' raw body values into typed ones through Core's
/// validators and token parsers, so the API accepts exactly what the CLI does,
/// reporting the problem as the <c>400</c> validation-problem result.
/// </summary>
internal static class AlertRequestValidation
{
    public static (AlertDirection Direction, IResult? Problem) ParseDirection(string? raw, string fieldName)
    {
        if (AlertDirectionParser.TryParse(raw, out var direction))
        {
            return (direction, null);
        }

        // ValidationOutcome guarantees ErrorMessage is non-null when IsValid is false.
        return (default, RequestValidation.ValidationProblem(fieldName, AlertDirectionParser.Validate(raw, fieldName).ErrorMessage!));
    }
}

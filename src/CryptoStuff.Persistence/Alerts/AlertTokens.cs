using System.Text.Json;
using CryptoStuff.Core.Alerts;

namespace CryptoStuff.Persistence.Alerts;

/// <summary>
/// Converts the alert enumerations to and from the lowercase text tokens they
/// travel as everywhere else, which Core's tests pin to
/// <see cref="JsonNamingPolicy.KebabCaseLower"/>.
/// </summary>
internal static class AlertTokens
{
    public static string ToToken(AlertDirection direction) => JsonNamingPolicy.KebabCaseLower.ConvertName(direction.ToString());

    public static string ToToken(AlertState state) => JsonNamingPolicy.KebabCaseLower.ConvertName(state.ToString());

    public static AlertDirection ToDirection(string token) =>
        AlertDirectionParser.TryParse(token, out var direction)
            ? direction
            : throw new InvalidDataException($"'{token}' is not a stored alert direction.");

    public static AlertState ToState(string token) =>
        AlertStateParser.TryParse(token, out var state)
            ? state
            : throw new InvalidDataException($"'{token}' is not a stored alert state.");
}

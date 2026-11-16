using System.Text.Json;

namespace CryptoStuff.Core;

/// <summary>
/// The one way an enumeration value is written as the lowercase text token it
/// travels and is stored as: its name in kebab case, so
/// <c>CostBasisMethod.AverageCost</c> is <c>average-cost</c>, as the hosts'
/// JSON writes it. Each enumeration's token parser reads these tokens back.
/// </summary>
public static class EnumTokens
{
    /// <summary>Writes <paramref name="value"/> as its token.</summary>
    public static string ToToken<TEnum>(TEnum value)
        where TEnum : struct, Enum =>
        JsonNamingPolicy.KebabCaseLower.ConvertName(value.ToString());
}

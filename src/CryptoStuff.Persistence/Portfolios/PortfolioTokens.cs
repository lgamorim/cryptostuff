using System.Text.Json;
using CryptoStuff.Core.Portfolios;

namespace CryptoStuff.Persistence.Portfolios;

/// <summary>
/// Converts the portfolio enumerations to and from the lowercase text tokens
/// they travel as everywhere else, which Core's tests pin to
/// <see cref="JsonNamingPolicy.KebabCaseLower"/>.
/// </summary>
internal static class PortfolioTokens
{
    public static string ToToken(CostBasisMethod method) => JsonNamingPolicy.KebabCaseLower.ConvertName(method.ToString());

    public static string ToToken(TransactionKind kind) => JsonNamingPolicy.KebabCaseLower.ConvertName(kind.ToString());

    public static CostBasisMethod ToCostBasisMethod(string token) =>
        CostBasisMethodParser.TryParse(token, out var method)
            ? method
            : throw new InvalidDataException($"'{token}' is not a stored cost-basis method.");

    public static TransactionKind ToTransactionKind(string token) =>
        TransactionKindParser.TryParse(token, out var kind)
            ? kind
            : throw new InvalidDataException($"'{token}' is not a stored transaction kind.");
}

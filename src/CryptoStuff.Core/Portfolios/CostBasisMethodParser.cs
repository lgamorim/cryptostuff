namespace CryptoStuff.Core.Portfolios;

/// <summary>Parses the text tokens a <see cref="CostBasisMethod"/> travels as, so both hosts accept identical input.</summary>
public static class CostBasisMethodParser
{
    /// <summary>
    /// Checks that <paramref name="token"/> parses under <see cref="TryParse"/>.
    /// <paramref name="fieldName"/> is used to build the failure message.
    /// </summary>
    public static ValidationOutcome Validate(string? token, string fieldName) =>
        TryParse(token, out _)
            ? ValidationOutcome.Success()
            : ValidationOutcome.Failure($"{fieldName} must be 'average-cost', 'fifo', 'lifo' or 'hifo'.");

    /// <summary>
    /// Parses <paramref name="token"/>, <c>average-cost</c>, <c>fifo</c>,
    /// <c>lifo</c> or <c>hifo</c> in any case, as a cost-basis method. Surrounding whitespace, enum member
    /// names, and numbers are not accepted.
    /// </summary>
    public static bool TryParse(string? token, out CostBasisMethod method)
    {
        switch (token?.ToLowerInvariant())
        {
            case "average-cost":
                method = CostBasisMethod.AverageCost;
                return true;
            case "fifo":
                method = CostBasisMethod.Fifo;
                return true;
            case "lifo":
                method = CostBasisMethod.Lifo;
                return true;
            case "hifo":
                method = CostBasisMethod.Hifo;
                return true;
            default:
                method = default;
                return false;
        }
    }
}

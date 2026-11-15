using System.Globalization;
using Microsoft.Net.Http.Headers;

namespace CryptoStuff.Api.Portfolios;

/// <summary>
/// The strong entity tag a portfolio travels with: its version, quoted, so a
/// client can send it back in <c>If-Match</c> to make a write conditional.
/// </summary>
internal static class PortfolioETag
{
    // Versions start at 1, so no portfolio is ever at 0: expecting it makes
    // the write fail its precondition, as a tag that matches nothing must.
    private const int UnmatchableVersion = 0;

    /// <summary>Formats <paramref name="version"/> as a strong entity tag, such as <c>"3"</c>.</summary>
    public static string Format(int version) => $"\"{version.ToString(CultureInfo.InvariantCulture)}\"";

    /// <summary>
    /// Reads the versions an <c>If-Match</c> header accepts: null when the
    /// header is absent or <c>*</c>, which any current version matches;
    /// otherwise every version a listed tag <see cref="Format"/> issued names,
    /// or a version no portfolio is at when none does; and a <c>400</c>
    /// validation problem when the value is neither <c>*</c> nor a list of
    /// entity tags.
    /// </summary>
    public static (IReadOnlyCollection<int>? ExpectedVersions, IResult? Problem) ParseIfMatch(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return (null, null);
        }

        // RFC 9110 allows * alone or a list of entity tags, never both.
        if (!EntityTagHeaderValue.TryParseStrictList([raw], out var tags)
            || (tags.Count > 1 && tags.Any(tag => tag.Equals(EntityTagHeaderValue.Any))))
        {
            return (null, RequestValidation.ValidationProblem(
                HeaderNames.IfMatch, "If-Match must be * or a list of entity tags, such as \"3\" or \"3\", \"4\"."));
        }

        if (tags[0].Equals(EntityTagHeaderValue.Any))
        {
            return (null, null);
        }

        var versions = tags.Select(TaggedVersion).OfType<int>().Distinct().ToList();
        return (versions.Count > 0 ? versions : [UnmatchableVersion], null);
    }

    // If-Match compares strongly: only the exact tag Format issued for a
    // version names it, never a weak tag or one this API never issues.
    private static int? TaggedVersion(EntityTagHeaderValue tag) =>
        !tag.IsWeak
        && int.TryParse(tag.Tag.AsSpan()[1..^1], NumberStyles.None, CultureInfo.InvariantCulture, out var version)
        && tag.Tag == Format(version)
            ? version
            : null;
}

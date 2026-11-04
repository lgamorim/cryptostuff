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
    /// Reads the version an <c>If-Match</c> header expects: null when the
    /// header is absent or <c>*</c>, which any current version matches; the
    /// tagged version for a tag <see cref="Format"/> issued; a version no
    /// portfolio is at for any other entity tag; and a <c>400</c> validation
    /// problem when the value is not one entity tag.
    /// </summary>
    public static (int? ExpectedVersion, IResult? Problem) ParseIfMatch(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return (null, null);
        }

        if (!EntityTagHeaderValue.TryParse(raw, out var tag))
        {
            return (null, RequestValidation.ValidationProblem(HeaderNames.IfMatch, "If-Match must be * or one entity tag, such as \"3\"."));
        }

        if (tag.Equals(EntityTagHeaderValue.Any))
        {
            return (null, null);
        }

        // If-Match compares strongly: only the exact tag Format issued for a
        // version matches it, never a weak tag or one this API never issues.
        var opaque = tag.Tag.AsSpan()[1..^1];
        return !tag.IsWeak
            && int.TryParse(opaque, NumberStyles.None, CultureInfo.InvariantCulture, out var version)
            && tag.Tag == Format(version)
                ? (version, null)
                : (UnmatchableVersion, null);
    }
}

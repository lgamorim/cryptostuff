using System.Globalization;

namespace CryptoStuff.Core;

/// <summary>
/// The one way every host and message writes a point in time: UTC, ISO 8601,
/// ending in <c>Z</c>, such as <c>2024-01-31T14:30:00Z</c>, with a fraction of
/// a second only when there is one.
/// </summary>
public static class UtcTimestamp
{
    /// <summary>The custom format string of a UTC timestamp.</summary>
    public const string Pattern = "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'";

    /// <summary>Formats <paramref name="timestamp"/>, converted to UTC, as <see cref="Pattern"/> under the invariant culture.</summary>
    public static string Format(DateTimeOffset timestamp) =>
        timestamp.ToUniversalTime().ToString(Pattern, CultureInfo.InvariantCulture);
}

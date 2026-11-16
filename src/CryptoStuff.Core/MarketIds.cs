namespace CryptoStuff.Core;

/// <summary>
/// The one form in which coin ids and vs_currencies are stored and compared,
/// so a coin or currency typed in any case, or with stray spaces, is the same
/// one everywhere it is kept.
/// </summary>
internal static class MarketIds
{
    /// <summary>Trims and lowercases <paramref name="id"/>, which callers have already checked is not blank.</summary>
    public static string Normalize(string id) => id.Trim().ToLowerInvariant();
}

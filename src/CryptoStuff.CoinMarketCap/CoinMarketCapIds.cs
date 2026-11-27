using System.Collections.Frozen;
using CryptoStuff.Core;

namespace CryptoStuff.CoinMarketCap;

/// <summary>
/// Translates the application's coin and platform ids, which are CoinGecko's,
/// to CoinMarketCap's slugs. Ids compare ordinally, as CoinGecko's own adapter
/// matches them.
/// </summary>
internal static class CoinMarketCapIds
{
    /// <summary>
    /// The coins whose CoinMarketCap slug differs from their CoinGecko id. Any
    /// other id is its own slug.
    /// </summary>
    /// <remarks>
    /// Seeded on 2026-11-27 from CoinGecko's top 100 coins by market cap, matched
    /// by name and symbol, and by contract address where a name was ambiguous,
    /// against the slugs coinmarketcap.com uses. None of these slugs was then a
    /// CoinGecko id of another coin. A coin CoinMarketCap does not list keeps
    /// its id and comes back missing. CoinMarketCap renames a slug when a coin
    /// rebrands, as Toncoin's became <c>gram</c>.
    /// </remarks>
    public static readonly IReadOnlyDictionary<string, string> CoinSlugOverrides = new Dictionary<string, string>
    {
        ["binancecoin"] = "bnb",
        ["ripple"] = "xrp",
        ["leo-token"] = "unus-sed-leo",
        ["whitebit"] = "whitebit-token",
        ["dai"] = "multi-collateral-dai",
        ["near"] = "near-protocol",
        ["avalanche-2"] = "avalanche",
        ["usd1-wlfi"] = "usd1",
        ["the-open-network"] = "gram",
        ["global-dollar"] = "global-dollar-usdg",
        ["hedera-hashgraph"] = "hedera",
        ["quant-network"] = "quant",
        ["crypto-com-chain"] = "cronos",
        // CoinMarketCap's own "bitway" is a different coin, BITWAY (WAY).
        ["bitway"] = "bitway-btw",
        ["htx-dao"] = "htx",
        ["aster-2"] = "aster",
        ["world-liberty-financial"] = "world-liberty-financial-wlfi",
        ["polkadot"] = "polkadot-new",
        ["bitget-token"] = "bitget-token-new",
        ["worldcoin-wld"] = "worldcoin-org",
        ["kucoin-shares"] = "kucoin-token",
        ["jupiter-exchange-solana"] = "jupiter-ag",
        ["midnight-3"] = "midnight-network",
        ["render-token"] = "render",
        ["gatechain-token"] = "gatetoken",
        ["injective-protocol"] = "injective",
        ["pi-network"] = "pi",
        ["pancakeswap-token"] = "pancakeswap",
        ["xdce-crowd-sale"] = "xdc-network",
        ["true-usd"] = "trueusd",
        ["stable-2"] = "stable",
        ["blockstack"] = "stacks",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    // Declared after CoinSlugOverrides, which static initialization reads first.
    private static readonly FrozenSet<string> OverrideSlugs = CoinSlugOverrides.Values.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// The CoinGecko platform ids that token queries accept, each with the slug
    /// of the coin CoinMarketCap names as that platform's in a contract
    /// address's <c>platform.coin</c>. Checked on 2026-11-27 against the
    /// platforms coinmarketcap.com lists for USDC.
    /// </summary>
    /// <remarks>
    /// A slug names the platform's coin, not the platform, and CoinMarketCap
    /// gives some platforms the same coin: <c>bnb</c> is the coin of both BNB
    /// Smart Chain and opBNB. Matching a contract address by this slug alone
    /// cannot tell such platforms apart.
    /// </remarks>
    public static readonly IReadOnlyDictionary<string, string> PlatformSlugs = new Dictionary<string, string>
    {
        ["ethereum"] = "ethereum",
        ["binance-smart-chain"] = "bnb",
        ["polygon-pos"] = "polygon-ecosystem-token",
        ["arbitrum-one"] = "arbitrum",
        ["optimistic-ethereum"] = "optimism-ethereum",
        ["base"] = "base",
        ["avalanche"] = "avalanche",
        ["solana"] = "solana",
        ["tron"] = "tron",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// Gets the slug to ask CoinMarketCap for <paramref name="coinId"/>, or
    /// <see langword="null"/> when the id is another coin's override slug: the
    /// table shows that slug is the coin of a different CoinGecko id, so this
    /// id is no coin CoinGecko knows, and asking for it would price it as that
    /// other coin.
    /// </summary>
    public static string? SlugOf(string coinId) =>
        CoinSlugOverrides.TryGetValue(coinId, out var slug) ? slug
        : OverrideSlugs.Contains(coinId) ? null
        : coinId;

    /// <summary>
    /// Groups the requested coin ids by the slug each is asked for as, keyed in
    /// request order, so a slug in a response resolves to the ids that were
    /// requested. An id with no slug is in no group, as a coin CoinMarketCap
    /// left out of its answer is; each id appears once however often it was
    /// requested.
    /// </summary>
    public static ILookup<string, string> RequestedCoinIdsBySlug(IEnumerable<string> coinIds) =>
        coinIds.Distinct(StringComparer.Ordinal)
            .SelectMany(id => SlugOf(id) is { } slug ? new[] { (Slug: slug, Id: id) } : [])
            .ToLookup(pair => pair.Slug, pair => pair.Id, StringComparer.Ordinal);

    /// <summary>
    /// Gets CoinMarketCap's slug for <paramref name="platformId"/>, or
    /// <see cref="ServiceErrorCode.NotFound"/> when the table does not list it.
    /// </summary>
    public static ServiceResult<string> PlatformSlugOf(string platformId) =>
        PlatformSlugs.TryGetValue(platformId, out var slug)
            ? ServiceResult<string>.Success(slug)
            : ServiceResult<string>.Failure(ServiceErrorCode.NotFound);
}

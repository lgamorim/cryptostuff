namespace CryptoStuff.CoinMarketCap;

/// <summary>
/// Endpoint-specific calls composed from <see cref="ICoinMarketCapClient.GetAsync{TValue}"/>,
/// per that interface's documented extension point. Identifiers and currencies
/// are passed through as given: translating application ids to slugs, and
/// currencies to CoinMarketCap's upper-case symbols, is the caller's job.
/// </summary>
public static class CoinMarketCapClientExtensions
{
    /// <summary>
    /// Calls <c>v3/cryptocurrency/quotes/latest</c> for the given slugs and
    /// currencies, with <c>skip_invalid=true</c>, so a slug CoinMarketCap does
    /// not know is left out of the result instead of failing the call. The
    /// result lists each coin found with its quote in each currency.
    /// </summary>
    public static Task<CoinMarketCapResponse<IReadOnlyList<CoinMarketCapLatestQuote>>> GetLatestQuotesAsync(
        this ICoinMarketCapClient client,
        IEnumerable<string> slugs,
        IEnumerable<string> currencies,
        CancellationToken cancellationToken) =>
        client.GetAsync<IReadOnlyList<CoinMarketCapLatestQuote>>(
            $"v3/cryptocurrency/quotes/latest?slug={CoinMarketCapUrlEncoder.JoinAndEncode(slugs)}&convert={CoinMarketCapUrlEncoder.JoinAndEncode(currencies)}&skip_invalid=true",
            cancellationToken);

    /// <summary>
    /// Calls <c>v2/cryptocurrency/info</c> for the given slugs. The result
    /// holds each coin's metadata, keyed as CoinMarketCap keys it, which may
    /// be the coin's numeric id rather than its slug: match each value by its
    /// <see cref="CoinMarketCapCoinInfo.Slug"/>.
    /// </summary>
    public static Task<CoinMarketCapResponse<IReadOnlyDictionary<string, CoinMarketCapCoinInfo>>> GetInfoBySlugAsync(
        this ICoinMarketCapClient client,
        IEnumerable<string> slugs,
        CancellationToken cancellationToken) =>
        client.GetAsync<IReadOnlyDictionary<string, CoinMarketCapCoinInfo>>(
            $"v2/cryptocurrency/info?slug={CoinMarketCapUrlEncoder.JoinAndEncode(slugs)}",
            cancellationToken);

    /// <summary>
    /// Calls <c>v2/cryptocurrency/info</c> for the coin deployed at the given
    /// contract address. The result holds that coin's metadata, keyed as
    /// CoinMarketCap keys it.
    /// </summary>
    public static Task<CoinMarketCapResponse<IReadOnlyDictionary<string, CoinMarketCapCoinInfo>>> GetInfoByAddressAsync(
        this ICoinMarketCapClient client,
        string contractAddress,
        CancellationToken cancellationToken) =>
        client.GetAsync<IReadOnlyDictionary<string, CoinMarketCapCoinInfo>>(
            $"v2/cryptocurrency/info?address={Uri.EscapeDataString(contractAddress)}",
            cancellationToken);

    /// <summary>
    /// Calls <c>v3/cryptocurrency/quotes/historical</c> for one daily quote
    /// series of the coin with CoinMarketCap's numeric <paramref name="id"/>,
    /// in one currency, between two instants sent as Unix seconds. It also
    /// sends a <c>count</c> of one point per day in that range, end day
    /// included, since the endpoint documents a default of 10 points and does
    /// not say a range overrides it. It takes
    /// the numeric id, not a slug, because the endpoint documents no
    /// <c>slug</c> parameter; the id is on every coin
    /// <see cref="GetLatestQuotesAsync"/> and the info calls return. The
    /// result holds the coin's series keyed by that id.
    /// </summary>
    public static Task<CoinMarketCapResponse<IReadOnlyDictionary<string, CoinMarketCapHistoricalCoin>>> GetHistoricalQuotesAsync(
        this ICoinMarketCapClient client,
        int id,
        string currency,
        DateTimeOffset timeStart,
        DateTimeOffset timeEnd,
        CancellationToken cancellationToken) =>
        client.GetAsync<IReadOnlyDictionary<string, CoinMarketCapHistoricalCoin>>(
            $"v3/cryptocurrency/quotes/historical?id={id}&convert={Uri.EscapeDataString(currency)}&time_start={timeStart.ToUnixTimeSeconds()}&time_end={timeEnd.ToUnixTimeSeconds()}&count={DailyPointCount(timeStart, timeEnd)}&interval=daily&skip_invalid=true",
            cancellationToken);

    private static int DailyPointCount(DateTimeOffset timeStart, DateTimeOffset timeEnd) =>
        (int)Math.Floor((timeEnd - timeStart).TotalDays) + 1;
}

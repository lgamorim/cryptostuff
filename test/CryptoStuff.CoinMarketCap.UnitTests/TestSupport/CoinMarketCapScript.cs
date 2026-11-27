using System.Net;

namespace CryptoStuff.CoinMarketCap.UnitTests.TestSupport;

/// <summary>Builds the responses a <see cref="FakeCoinMarketCapClient"/> script answers with.</summary>
internal static class CoinMarketCapScript
{
    /// <summary>A successful <c>quotes/latest</c> answer holding <paramref name="coins"/>.</summary>
    public static CoinMarketCapResponse<IReadOnlyList<CoinMarketCapLatestQuote>> Latest(params CoinMarketCapLatestQuote[] coins) =>
        CoinMarketCapResponse<IReadOnlyList<CoinMarketCapLatestQuote>>.Success(coins, HttpStatusCode.OK);

    /// <summary>A coin in a <c>quotes/latest</c> answer, quoted as <paramref name="quotes"/>.</summary>
    public static CoinMarketCapLatestQuote Coin(string slug, int id, params CoinMarketCapCurrencyQuote[] quotes) =>
        new() { Id = id, Slug = slug, Quote = quotes };

    /// <summary>One currency's quote for a coin.</summary>
    public static CoinMarketCapCurrencyQuote Quote(string symbol, decimal? price, decimal? marketCap = null, decimal? volume = null) =>
        new() { Symbol = symbol, Price = price, MarketCap = marketCap, Volume24h = volume };

    /// <summary>A failed answer of any response type.</summary>
    public static CoinMarketCapResponse<TValue> Failure<TValue>(HttpStatusCode statusCode, string? errorMessage = null) =>
        CoinMarketCapResponse<TValue>.Failure(statusCode, (int)statusCode, errorMessage);

    /// <summary>A failed <c>quotes/latest</c> answer.</summary>
    public static CoinMarketCapResponse<IReadOnlyList<CoinMarketCapLatestQuote>> LatestFailure(HttpStatusCode statusCode, string? errorMessage = null) =>
        Failure<IReadOnlyList<CoinMarketCapLatestQuote>>(statusCode, errorMessage);

    /// <summary>CoinMarketCap's answer to a currency it does not convert to.</summary>
    public static CoinMarketCapResponse<IReadOnlyList<CoinMarketCapLatestQuote>> RejectedCurrency(string currency) =>
        LatestFailure(HttpStatusCode.BadRequest, $"Invalid value for \"convert\": \"{currency}\"");

    /// <summary>A successful <c>info</c> answer holding <paramref name="coins"/>, keyed by numeric id.</summary>
    public static CoinMarketCapResponse<IReadOnlyDictionary<string, CoinMarketCapCoinInfo>> Info(params CoinMarketCapCoinInfo[] coins) =>
        CoinMarketCapResponse<IReadOnlyDictionary<string, CoinMarketCapCoinInfo>>.Success(
            new Dictionary<string, CoinMarketCapCoinInfo>(coins.Select((coin, index) => KeyValuePair.Create(InfoKey(coin, index), coin))),
            HttpStatusCode.OK);

    // A null coin, which upstream JSON can carry, still needs a key of its own.
    private static string InfoKey(CoinMarketCapCoinInfo? coin, int index) =>
        coin is null ? $"null-{index}" : coin.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>A coin in an <c>info</c> answer.</summary>
    public static CoinMarketCapCoinInfo InfoCoin(
        string slug,
        int id,
        IReadOnlyList<CoinMarketCapContractAddress>? contracts = null,
        string name = "Name",
        string symbol = "SYM",
        string? description = null,
        string? logo = null) =>
        new() { Id = id, Slug = slug, Name = name, Symbol = symbol, Description = description, Logo = logo, ContractAddresses = contracts };

    /// <summary>A contract address on the platform whose coin's slug is <paramref name="platformSlug"/>.</summary>
    public static CoinMarketCapContractAddress Contract(string address, string platformSlug) =>
        new() { Address = address, Platform = new CoinMarketCapContractPlatform { Coin = new CoinMarketCapPlatformCoin { Slug = platformSlug } } };

    /// <summary>A failed <c>info</c> answer.</summary>
    public static CoinMarketCapResponse<IReadOnlyDictionary<string, CoinMarketCapCoinInfo>> InfoFailure(HttpStatusCode statusCode, string? errorMessage = null) =>
        Failure<IReadOnlyDictionary<string, CoinMarketCapCoinInfo>>(statusCode, errorMessage);

    /// <summary>A successful <c>quotes/historical</c> answer for the coin with numeric id <paramref name="id"/>.</summary>
    public static CoinMarketCapResponse<IReadOnlyDictionary<string, CoinMarketCapHistoricalCoin>> Historical(int id, params CoinMarketCapHistoricalQuote[] points) =>
        CoinMarketCapResponse<IReadOnlyDictionary<string, CoinMarketCapHistoricalCoin>>.Success(
            new Dictionary<string, CoinMarketCapHistoricalCoin>
            {
                [id.ToString(System.Globalization.CultureInfo.InvariantCulture)] = new() { Quotes = points },
            },
            HttpStatusCode.OK);

    /// <summary>One point in a <c>quotes/historical</c> answer, quoted in <paramref name="currency"/>.</summary>
    public static CoinMarketCapHistoricalQuote Point(string timestamp, string currency, decimal? price, decimal? marketCap, decimal? volume) =>
        new()
        {
            Timestamp = DateTimeOffset.Parse(timestamp, System.Globalization.CultureInfo.InvariantCulture),
            Quote = new Dictionary<string, CoinMarketCapHistoricalCurrencyQuote>
            {
                [currency] = new() { Price = price, MarketCap = marketCap, Volume24h = volume },
            },
        };

    /// <summary>Whether <paramref name="requestUri"/> calls <c>quotes/historical</c>.</summary>
    public static bool IsHistorical(string requestUri) => requestUri.StartsWith("v3/cryptocurrency/quotes/historical?", StringComparison.Ordinal);

    /// <summary>Whether <paramref name="requestUri"/> calls <c>info</c>.</summary>
    public static bool IsInfo(string requestUri) => requestUri.StartsWith("v2/cryptocurrency/info?", StringComparison.Ordinal);

    /// <summary>The value of <paramref name="parameter"/> in <paramref name="requestUri"/>'s query, still percent-encoded.</summary>
    public static string QueryValue(string requestUri, string parameter)
    {
        var query = requestUri[(requestUri.IndexOf('?') + 1)..];
        return query.Split('&')
            .Select(pair => pair.Split('=', 2))
            .Single(pair => pair[0] == parameter)[1];
    }

    /// <summary>A script answering each <c>quotes/latest</c> call by its <c>convert</c> currency.</summary>
    public static Func<string, object> LatestByCurrency(IReadOnlyDictionary<string, object> answers) =>
        requestUri => answers[QueryValue(requestUri, "convert")];
}

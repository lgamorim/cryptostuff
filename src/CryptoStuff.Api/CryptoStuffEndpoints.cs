using CryptoStuff.Core;

namespace CryptoStuff.Api;

/// <summary>
/// The five cryptocurrency-data routes over <see cref="ICryptocurrencyService"/>.
/// Handlers are public static methods with plain parameters so
/// <c>CryptoStuff.Api.UnitTests</c> can call them directly, without a running host.
/// </summary>
public static class CryptoStuffEndpoints
{
    /// <summary>Maps all five routes onto <paramref name="app"/>.</summary>
    internal static void MapCryptoStuffEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/prices", GetPricesAsync).WithProblemMetadata<CoinPriceView>();
        app.MapGet("/token-prices", GetTokenPricesAsync).WithProblemMetadata<TokenPriceView>();
        app.MapGet("/historical-market-data", GetHistoricalMarketDataAsync).WithProblemMetadata<CoinMarketChartView>();
        app.MapGet("/coins/{coin}", GetCoinAsync).WithProblemMetadata<CoinView>();
        app.MapGet("/coins/{coin}/developer-data", GetDeveloperDataAsync).WithProblemMetadata<CoinDeveloperDataView>();
    }

    /// <summary><c>GET /prices</c> — live prices for the given coins in the given currencies.</summary>
    public static async Task<IResult> GetPricesAsync(
        string? coins, string? currencies, ICryptocurrencyService service, CancellationToken cancellationToken)
    {
        var (coinIds, coinsProblem) = RequestValidation.ParseIdentifierList(coins, "coins");
        if (coinsProblem is not null)
        {
            return coinsProblem;
        }

        var (vsCurrencies, currenciesProblem) = RequestValidation.ParseIdentifierList(currencies, "currencies");
        if (currenciesProblem is not null)
        {
            return currenciesProblem;
        }

        var query = new CoinPriceQuery { CoinIds = coinIds, VsCurrencies = vsCurrencies };
        var result = await service.GetPricesAsync(query, cancellationToken);
        return ServiceResultHttp.Ok(result);
    }

    /// <summary><c>GET /token-prices</c> — live prices for the given token contract addresses.</summary>
    public static async Task<IResult> GetTokenPricesAsync(
        string? platform, string? addresses, string? currencies, ICryptocurrencyService service, CancellationToken cancellationToken)
    {
        var platformProblem = RequestValidation.ValidateScalar(platform, "platform");
        if (platformProblem is not null)
        {
            return platformProblem;
        }

        var (contractAddresses, addressesProblem) = RequestValidation.ParseIdentifierList(addresses, "addresses");
        if (addressesProblem is not null)
        {
            return addressesProblem;
        }

        var (vsCurrencies, currenciesProblem) = RequestValidation.ParseIdentifierList(currencies, "currencies");
        if (currenciesProblem is not null)
        {
            return currenciesProblem;
        }

        // ValidateScalar returning null guarantees platform is non-null and non-whitespace.
        var query = new TokenPriceQuery { Platform = platform!, ContractAddresses = contractAddresses, VsCurrencies = vsCurrencies };
        var result = await service.GetTokenPricesAsync(query, cancellationToken);
        return ServiceResultHttp.Ok(result);
    }

    /// <summary><c>GET /historical-market-data</c> — a coin's historical price, market cap, and volume series.</summary>
    public static async Task<IResult> GetHistoricalMarketDataAsync(
        string? coin, string? currency, string? days, ICryptocurrencyService service, CancellationToken cancellationToken)
    {
        var coinProblem = RequestValidation.ValidateScalar(coin, "coin");
        if (coinProblem is not null)
        {
            return coinProblem;
        }

        var currencyProblem = RequestValidation.ValidateScalar(currency, "currency");
        if (currencyProblem is not null)
        {
            return currencyProblem;
        }

        var (parsedDays, daysProblem) = RequestValidation.ParseDays(days);
        if (daysProblem is not null)
        {
            return daysProblem;
        }

        // ValidateScalar returning null guarantees coin and currency are non-null and non-whitespace.
        var query = new CoinMarketChartQuery { CoinId = coin!, VsCurrency = currency!, Days = parsedDays };
        var result = await service.GetMarketChartAsync(query, cancellationToken);
        return ServiceResultHttp.Ok(result);
    }

    /// <summary><c>GET /coins/{coin}</c> — a coin's detail.</summary>
    public static async Task<IResult> GetCoinAsync(string coin, ICryptocurrencyService service, CancellationToken cancellationToken)
    {
        var coinProblem = RequestValidation.ValidateScalar(coin, "coin");
        if (coinProblem is not null)
        {
            return coinProblem;
        }

        var query = new CoinQuery { CoinId = coin };
        var result = await service.GetCoinAsync(query, cancellationToken);
        return ServiceResultHttp.Ok(result);
    }

    /// <summary><c>GET /coins/{coin}/developer-data</c> — a coin's developer repository activity as of a date.</summary>
    public static async Task<IResult> GetDeveloperDataAsync(
        string coin, string? date, ICryptocurrencyService service, CancellationToken cancellationToken)
    {
        var coinProblem = RequestValidation.ValidateScalar(coin, "coin");
        if (coinProblem is not null)
        {
            return coinProblem;
        }

        var dateProblem = RequestValidation.ValidateDate(date, "date");
        if (dateProblem is not null)
        {
            return dateProblem;
        }

        // ValidateDate returning null guarantees date is non-null, non-whitespace, and well-formed.
        var query = new CoinDeveloperDataQuery { CoinId = coin, Date = date! };
        var result = await service.GetDeveloperDataAsync(query, cancellationToken);
        return ServiceResultHttp.Ok(result);
    }
}

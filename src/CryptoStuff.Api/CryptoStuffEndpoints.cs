using System.Globalization;
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
        var (coinIds, coinsProblem) = ParseIdentifierList(coins, "coins");
        if (coinsProblem is not null)
        {
            return coinsProblem;
        }

        var (vsCurrencies, currenciesProblem) = ParseIdentifierList(currencies, "currencies");
        if (currenciesProblem is not null)
        {
            return currenciesProblem;
        }

        var query = new CoinPriceQuery { CoinIds = coinIds, VsCurrencies = vsCurrencies };
        var result = await service.GetPricesAsync(query, cancellationToken);
        return ToResult(result);
    }

    /// <summary><c>GET /token-prices</c> — live prices for the given token contract addresses.</summary>
    public static async Task<IResult> GetTokenPricesAsync(
        string? platform, string? addresses, string? currencies, ICryptocurrencyService service, CancellationToken cancellationToken)
    {
        var platformProblem = ValidateScalar(platform, "platform");
        if (platformProblem is not null)
        {
            return platformProblem;
        }

        var (contractAddresses, addressesProblem) = ParseIdentifierList(addresses, "addresses");
        if (addressesProblem is not null)
        {
            return addressesProblem;
        }

        var (vsCurrencies, currenciesProblem) = ParseIdentifierList(currencies, "currencies");
        if (currenciesProblem is not null)
        {
            return currenciesProblem;
        }

        // ValidateScalar returning null guarantees platform is non-null and non-whitespace.
        var query = new TokenPriceQuery { Platform = platform!, ContractAddresses = contractAddresses, VsCurrencies = vsCurrencies };
        var result = await service.GetTokenPricesAsync(query, cancellationToken);
        return ToResult(result);
    }

    /// <summary><c>GET /historical-market-data</c> — a coin's historical price, market cap, and volume series.</summary>
    public static async Task<IResult> GetHistoricalMarketDataAsync(
        string? coin, string? currency, string? days, ICryptocurrencyService service, CancellationToken cancellationToken)
    {
        var coinProblem = ValidateScalar(coin, "coin");
        if (coinProblem is not null)
        {
            return coinProblem;
        }

        var currencyProblem = ValidateScalar(currency, "currency");
        if (currencyProblem is not null)
        {
            return currencyProblem;
        }

        var (parsedDays, daysProblem) = ParseDays(days);
        if (daysProblem is not null)
        {
            return daysProblem;
        }

        // ValidateScalar returning null guarantees coin and currency are non-null and non-whitespace.
        var query = new CoinMarketChartQuery { CoinId = coin!, VsCurrency = currency!, Days = parsedDays };
        var result = await service.GetMarketChartAsync(query, cancellationToken);
        return ToResult(result);
    }

    /// <summary><c>GET /coins/{coin}</c> — a coin's detail.</summary>
    public static async Task<IResult> GetCoinAsync(string coin, ICryptocurrencyService service, CancellationToken cancellationToken)
    {
        var coinProblem = ValidateScalar(coin, "coin");
        if (coinProblem is not null)
        {
            return coinProblem;
        }

        var query = new CoinQuery { CoinId = coin };
        var result = await service.GetCoinAsync(query, cancellationToken);
        return ToResult(result);
    }

    /// <summary><c>GET /coins/{coin}/developer-data</c> — a coin's developer repository activity as of a date.</summary>
    public static async Task<IResult> GetDeveloperDataAsync(
        string coin, string? date, ICryptocurrencyService service, CancellationToken cancellationToken)
    {
        var coinProblem = ValidateScalar(coin, "coin");
        if (coinProblem is not null)
        {
            return coinProblem;
        }

        var dateProblem = ValidateDate(date, "date");
        if (dateProblem is not null)
        {
            return dateProblem;
        }

        // ValidateDate returning null guarantees date is non-null, non-whitespace, and well-formed.
        var query = new CoinDeveloperDataQuery { CoinId = coin, Date = date! };
        var result = await service.GetDeveloperDataAsync(query, cancellationToken);
        return ToResult(result);
    }

    private static IResult ToResult<TView>(ServiceResult<TView> result) =>
        // ServiceResult<T> guarantees Value is non-null when IsSuccess is true, and ErrorCode is set when it's false.
        result.IsSuccess ? Results.Ok(result.Value) : ServiceErrorHttpMapper.ToProblem(result.ErrorCode!.Value);

    // A query/path string value is never null-and-empty at once, and string.Split always
    // returns at least one element (an empty string splits to [""]), so a genuinely empty
    // list can never reach the per-element loop below - only a blank entry can. This mirrors
    // CliRunner.TryParseIdentifierList's identical reasoning for the same reason:
    // NonEmptyCollectionValidator's zero-element branch is unreachable from this call site.
    private static (IReadOnlyList<string> Values, IResult? Problem) ParseIdentifierList(string? raw, string fieldName)
    {
        var scalarProblem = ValidateScalar(raw, fieldName);
        if (scalarProblem is not null)
        {
            return ([], scalarProblem);
        }

        // ValidateScalar returning null guarantees raw is non-null and non-whitespace.
        var split = raw!.Split(',').Select(value => value.Trim()).ToArray();

        foreach (var value in split)
        {
            if (!NonEmptyScalarValidator.Validate(value, fieldName).IsValid)
            {
                return ([], ValidationProblem(fieldName, $"{fieldName} contains a blank entry."));
            }
        }

        return (split, null);
    }

    private static IResult? ValidateScalar(string? value, string fieldName)
    {
        var outcome = NonEmptyScalarValidator.Validate(value, fieldName);

        // ValidationOutcome guarantees ErrorMessage is non-null when IsValid is false.
        return outcome.IsValid ? null : ValidationProblem(fieldName, outcome.ErrorMessage!);
    }

    private static IResult? ValidateDate(string? value, string fieldName)
    {
        var scalarProblem = ValidateScalar(value, fieldName);
        if (scalarProblem is not null)
        {
            return scalarProblem;
        }

        var dateOutcome = DateFormatValidator.Validate(value, fieldName);

        // ValidationOutcome guarantees ErrorMessage is non-null when IsValid is false.
        return dateOutcome.IsValid ? null : ValidationProblem(fieldName, dateOutcome.ErrorMessage!);
    }

    private static (int Days, IResult? Problem) ParseDays(string? raw)
    {
        var scalarProblem = ValidateScalar(raw, "days");
        if (scalarProblem is not null)
        {
            return (0, scalarProblem);
        }

        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var days))
        {
            return (0, ValidationProblem("days", "days must be a whole number."));
        }

        var outcome = PositiveDayCountValidator.Validate(days, "days");

        // ValidationOutcome guarantees ErrorMessage is non-null when IsValid is false.
        return outcome.IsValid ? (days, null) : (0, ValidationProblem("days", outcome.ErrorMessage!));
    }

    private static IResult ValidationProblem(string fieldName, string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [fieldName] = [message] });

    private static RouteHandlerBuilder WithProblemMetadata<TView>(this RouteHandlerBuilder builder) =>
        builder
            .Produces<TView>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
}

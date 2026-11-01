using CryptoStuff.Core;
using CryptoStuff.Core.Watchlists;

namespace CryptoStuff.Api.Watchlists;

/// <summary>
/// The <c>/watchlists</c> routes over <see cref="IWatchlistService"/>.
/// Handlers are public static methods with plain parameters so
/// <c>CryptoStuff.Api.UnitTests</c> can call them directly, without a running host.
/// </summary>
public static class WatchlistEndpoints
{
    /// <summary>Maps the watchlist routes onto <paramref name="app"/>, under <c>/watchlists</c>.</summary>
    internal static void MapWatchlistEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/watchlists");

        group.MapGet("/", ListAsync)
            .WithProblemMetadata<IReadOnlyList<WatchlistView>>(StatusCodes.Status200OK);
        group.MapPost("/", CreateAsync)
            .WithProblemMetadata<WatchlistView>(StatusCodes.Status201Created, StatusCodes.Status400BadRequest, StatusCodes.Status409Conflict);
        group.MapGet("/{id:int}", GetAsync)
            .WithProblemMetadata<WatchlistView>(StatusCodes.Status200OK, StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound);
        group.MapPut("/{id:int}", RenameAsync)
            .WithProblemMetadata<WatchlistView>(
                StatusCodes.Status200OK, StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict);
        group.MapDelete("/{id:int}", DeleteAsync)
            .WithProblemMetadata(
                StatusCodes.Status204NoContent, StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict);
        group.MapPut("/{id:int}/coins/{coin}", AddCoinAsync)
            .WithProblemMetadata<WatchlistView>(
                StatusCodes.Status200OK,
                StatusCodes.Status400BadRequest,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status429TooManyRequests,
                StatusCodes.Status502BadGateway,
                StatusCodes.Status504GatewayTimeout);
        group.MapDelete("/{id:int}/coins/{coin}", RemoveCoinAsync)
            .WithProblemMetadata<WatchlistView>(
                StatusCodes.Status200OK, StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict);
        group.MapGet("/{id:int}/prices", GetPricesAsync)
            .WithProblemMetadata<CoinPriceView>(
                StatusCodes.Status200OK,
                StatusCodes.Status400BadRequest,
                StatusCodes.Status404NotFound,
                StatusCodes.Status429TooManyRequests,
                StatusCodes.Status502BadGateway,
                StatusCodes.Status504GatewayTimeout);
    }

    /// <summary><c>GET /watchlists</c> — every watchlist.</summary>
    public static async Task<IResult> ListAsync(IWatchlistService service, CancellationToken cancellationToken)
    {
        var result = await service.ListAsync(cancellationToken);
        return ServiceResultHttp.Ok(result);
    }

    /// <summary><c>POST /watchlists</c> — creates an empty watchlist.</summary>
    public static async Task<IResult> CreateAsync(WatchlistRequest? request, IWatchlistService service, CancellationToken cancellationToken)
    {
        var nameProblem = RequestValidation.ValidateName(request?.Name, "name");
        if (nameProblem is not null)
        {
            return nameProblem;
        }

        // ValidateName returning null guarantees request and its Name are non-null and non-whitespace.
        var result = await service.CreateAsync(new CreateWatchlistCommand { Name = request!.Name! }, cancellationToken);
        return ServiceResultHttp.Created(result, view => $"/watchlists/{view.Id}");
    }

    /// <summary><c>PUT /watchlists/{id}</c> — replaces a watchlist's name.</summary>
    public static async Task<IResult> RenameAsync(int id, WatchlistRequest? request, IWatchlistService service, CancellationToken cancellationToken)
    {
        var idProblem = RequestValidation.ValidateId(id, "id");
        if (idProblem is not null)
        {
            return idProblem;
        }

        var nameProblem = RequestValidation.ValidateName(request?.Name, "name");
        if (nameProblem is not null)
        {
            return nameProblem;
        }

        // ValidateName returning null guarantees request and its Name are non-null and non-whitespace.
        var command = new RenameWatchlistCommand { WatchlistId = id, Name = request!.Name! };
        var result = await service.RenameAsync(command, cancellationToken);
        return ServiceResultHttp.Ok(result);
    }

    /// <summary><c>DELETE /watchlists/{id}</c> — deletes a watchlist.</summary>
    public static async Task<IResult> DeleteAsync(int id, IWatchlistService service, CancellationToken cancellationToken)
    {
        var idProblem = RequestValidation.ValidateId(id, "id");
        if (idProblem is not null)
        {
            return idProblem;
        }

        var result = await service.DeleteAsync(id, cancellationToken);
        return ServiceResultHttp.NoContent(result);
    }

    /// <summary><c>PUT /watchlists/{id}/coins/{coin}</c> — adds a coin; adding one already present changes nothing.</summary>
    public static Task<IResult> AddCoinAsync(int id, string coin, IWatchlistService service, CancellationToken cancellationToken) =>
        ChangeCoinAsync(service.AddCoinAsync, id, coin, cancellationToken);

    /// <summary><c>DELETE /watchlists/{id}/coins/{coin}</c> — removes a coin; removing one that is absent changes nothing.</summary>
    public static Task<IResult> RemoveCoinAsync(int id, string coin, IWatchlistService service, CancellationToken cancellationToken) =>
        ChangeCoinAsync(service.RemoveCoinAsync, id, coin, cancellationToken);

    /// <summary><c>GET /watchlists/{id}</c> — one watchlist.</summary>
    public static async Task<IResult> GetAsync(int id, IWatchlistService service, CancellationToken cancellationToken)
    {
        var idProblem = RequestValidation.ValidateId(id, "id");
        if (idProblem is not null)
        {
            return idProblem;
        }

        var result = await service.GetAsync(id, cancellationToken);
        return ServiceResultHttp.Ok(result);
    }

    /// <summary><c>GET /watchlists/{id}/prices</c> — live prices for the watchlist's coins in the given currency.</summary>
    public static async Task<IResult> GetPricesAsync(
        int id, string? currency, IWatchlistService service, CancellationToken cancellationToken)
    {
        var idProblem = RequestValidation.ValidateId(id, "id");
        if (idProblem is not null)
        {
            return idProblem;
        }

        var currencyProblem = RequestValidation.ValidateScalar(currency, "currency");
        if (currencyProblem is not null)
        {
            return currencyProblem;
        }

        // ValidateScalar returning null guarantees currency is non-null and non-whitespace.
        var query = new WatchlistPriceQuery { WatchlistId = id, VsCurrency = currency! };
        var result = await service.GetPricesAsync(query, cancellationToken);
        return ServiceResultHttp.Ok(result);
    }

    private static async Task<IResult> ChangeCoinAsync(
        Func<WatchlistCoinCommand, CancellationToken, Task<ServiceResult<WatchlistView>>> change,
        int id,
        string coin,
        CancellationToken cancellationToken)
    {
        var idProblem = RequestValidation.ValidateId(id, "id");
        if (idProblem is not null)
        {
            return idProblem;
        }

        var coinProblem = RequestValidation.ValidateScalar(coin, "coin");
        if (coinProblem is not null)
        {
            return coinProblem;
        }

        var result = await change(new WatchlistCoinCommand { WatchlistId = id, CoinId = coin }, cancellationToken);
        return ServiceResultHttp.Ok(result);
    }
}

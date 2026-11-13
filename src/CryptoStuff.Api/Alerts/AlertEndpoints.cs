using CryptoStuff.Core;
using CryptoStuff.Core.Alerts;

namespace CryptoStuff.Api.Alerts;

/// <summary>
/// The <c>/alerts</c> routes over <see cref="IAlertService"/> and <see cref="IAlertEvaluator"/>.
/// Handlers are public static methods with plain parameters so
/// <c>CryptoStuff.Api.UnitTests</c> can call them directly, without a running host.
/// </summary>
/// <remarks>
/// Every write is unconditional: ROADMAP gives the version token an <c>ETag</c>
/// and <c>If-Match</c> contract on the portfolio routes only, so these routes
/// pass no expected version, and a lost race is still a <c>409</c>.
/// </remarks>
public static class AlertEndpoints
{
    /// <summary>Maps the alert routes onto <paramref name="app"/>, under <c>/alerts</c>.</summary>
    internal static void MapAlertEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/alerts");

        group.MapGet("/", ListAsync)
            .WithProblemMetadata<IReadOnlyList<AlertView>>(StatusCodes.Status200OK);

        // Creating asks CoinGecko about the coin, so it can also fail upstream.
        group.MapPost("/", CreateAsync)
            .WithProblemMetadata<AlertView>(
                StatusCodes.Status201Created,
                StatusCodes.Status400BadRequest,
                StatusCodes.Status409Conflict,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status429TooManyRequests,
                StatusCodes.Status502BadGateway,
                StatusCodes.Status504GatewayTimeout);

        group.MapGet("/{id:int}", GetAsync)
            .WithProblemMetadata<AlertView>(StatusCodes.Status200OK, StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound);
        group.MapPut("/{id:int}", UpdateAsync)
            .WithProblemMetadata<AlertView>(
                StatusCodes.Status200OK, StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict);
        group.MapDelete("/{id:int}", DeleteAsync)
            .WithProblemMetadata(
                StatusCodes.Status204NoContent, StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict);
        group.MapPost("/{id:int}/rearm", RearmAsync)
            .WithProblemMetadata<AlertView>(
                StatusCodes.Status200OK, StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict);

        // Evaluating asks CoinGecko for every armed alert's price; that is its only way to fail.
        group.MapPost("/check", CheckAsync)
            .WithProblemMetadata<AlertEvaluationView>(
                StatusCodes.Status200OK,
                StatusCodes.Status429TooManyRequests,
                StatusCodes.Status502BadGateway,
                StatusCodes.Status504GatewayTimeout);
    }

    /// <summary><c>GET /alerts</c> — every alert.</summary>
    public static async Task<IResult> ListAsync(IAlertService service, CancellationToken cancellationToken)
    {
        var result = await service.ListAsync(cancellationToken);
        return ServiceResultHttp.Ok(result);
    }

    /// <summary><c>GET /alerts/{id}</c> — one alert.</summary>
    public static async Task<IResult> GetAsync(int id, IAlertService service, CancellationToken cancellationToken)
    {
        var idProblem = RequestValidation.ValidateId(id, "id");
        if (idProblem is not null)
        {
            return idProblem;
        }

        var result = await service.GetAsync(id, cancellationToken);
        return ServiceResultHttp.Ok(result);
    }

    /// <summary><c>POST /alerts</c> — creates an armed alert.</summary>
    public static async Task<IResult> CreateAsync(CreateAlertRequest? request, IAlertService service, CancellationToken cancellationToken)
    {
        var coinProblem = RequestValidation.ValidateScalar(request?.CoinId, "coinId");
        if (coinProblem is not null)
        {
            return coinProblem;
        }

        var currencyProblem = RequestValidation.ValidateScalar(request?.VsCurrency, "vsCurrency");
        if (currencyProblem is not null)
        {
            return currencyProblem;
        }

        var (direction, directionProblem) = AlertRequestValidation.ParseDirection(request?.Direction, "direction");
        if (directionProblem is not null)
        {
            return directionProblem;
        }

        var thresholdProblem = RequestValidation.ValidateAmount(request?.Threshold, "threshold", PositiveDecimalValidator.Validate);
        if (thresholdProblem is not null)
        {
            return thresholdProblem;
        }

        // The checks above returning null guarantee request, its CoinId, VsCurrency and Threshold are non-null.
        var command = new CreateAlertCommand
        {
            CoinId = request!.CoinId!,
            VsCurrency = request.VsCurrency!,
            Direction = direction,
            Threshold = request.Threshold!.Value,
        };
        var result = await service.CreateAsync(command, cancellationToken);
        return ServiceResultHttp.Created(result, view => $"/alerts/{view.Id}");
    }

    /// <summary><c>PUT /alerts/{id}</c> — replaces an alert's direction and threshold, which re-arms it.</summary>
    public static async Task<IResult> UpdateAsync(int id, UpdateAlertRequest? request, IAlertService service, CancellationToken cancellationToken)
    {
        var idProblem = RequestValidation.ValidateId(id, "id");
        if (idProblem is not null)
        {
            return idProblem;
        }

        var (direction, directionProblem) = AlertRequestValidation.ParseDirection(request?.Direction, "direction");
        if (directionProblem is not null)
        {
            return directionProblem;
        }

        var thresholdProblem = RequestValidation.ValidateAmount(request?.Threshold, "threshold", PositiveDecimalValidator.Validate);
        if (thresholdProblem is not null)
        {
            return thresholdProblem;
        }

        // ValidateAmount returning null guarantees request and its Threshold are non-null.
        var command = new UpdateAlertCommand { AlertId = id, Direction = direction, Threshold = request!.Threshold!.Value };
        var result = await service.UpdateAsync(command, cancellationToken);
        return ServiceResultHttp.Ok(result);
    }

    /// <summary><c>DELETE /alerts/{id}</c> — deletes an alert.</summary>
    public static async Task<IResult> DeleteAsync(int id, IAlertService service, CancellationToken cancellationToken)
    {
        var idProblem = RequestValidation.ValidateId(id, "id");
        if (idProblem is not null)
        {
            return idProblem;
        }

        var result = await service.DeleteAsync(id, expectedVersion: null, cancellationToken);
        return ServiceResultHttp.NoContent(result);
    }

    /// <summary><c>POST /alerts/{id}/rearm</c> — moves a triggered alert back to armed.</summary>
    public static async Task<IResult> RearmAsync(int id, IAlertService service, CancellationToken cancellationToken)
    {
        var idProblem = RequestValidation.ValidateId(id, "id");
        if (idProblem is not null)
        {
            return idProblem;
        }

        var result = await service.RearmAsync(id, expectedVersion: null, cancellationToken);
        return ServiceResultHttp.Ok(result);
    }

    /// <summary><c>POST /alerts/check</c> — evaluates the armed alerts once and reports the ones that triggered and the ones it could not price.</summary>
    public static async Task<IResult> CheckAsync(IAlertEvaluator evaluator, CancellationToken cancellationToken)
    {
        var result = await evaluator.EvaluateAsync(cancellationToken);
        return ServiceResultHttp.Ok(result);
    }
}

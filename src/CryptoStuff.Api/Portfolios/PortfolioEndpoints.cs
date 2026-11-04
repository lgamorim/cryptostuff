using CryptoStuff.Core;
using CryptoStuff.Core.Portfolios;
using Microsoft.AspNetCore.Mvc;

namespace CryptoStuff.Api.Portfolios;

/// <summary>
/// The <c>/portfolios</c> routes over <see cref="IPortfolioService"/>.
/// Handlers are public static methods with plain parameters so
/// <c>CryptoStuff.Api.UnitTests</c> can call them directly, without a running host.
/// </summary>
public static class PortfolioEndpoints
{
    // Adding or replacing a transaction asks CoinGecko about its coin, so
    // those two writes can also fail upstream.
    private static readonly int[] CheckedTransactionWriteProblems =
    [
        StatusCodes.Status400BadRequest,
        StatusCodes.Status404NotFound,
        StatusCodes.Status409Conflict,
        StatusCodes.Status412PreconditionFailed,
        StatusCodes.Status422UnprocessableEntity,
        StatusCodes.Status429TooManyRequests,
        StatusCodes.Status502BadGateway,
        StatusCodes.Status504GatewayTimeout,
    ];

    /// <summary>Maps the portfolio routes onto <paramref name="app"/>, under <c>/portfolios</c>.</summary>
    internal static void MapPortfolioEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/portfolios");

        group.MapGet("/", ListAsync)
            .WithProblemMetadata<IReadOnlyList<PortfolioView>>(StatusCodes.Status200OK);
        group.MapPost("/", CreateAsync)
            .WithProblemMetadata<PortfolioView>(StatusCodes.Status201Created, StatusCodes.Status400BadRequest, StatusCodes.Status409Conflict);
        group.MapGet("/{id:int}", GetAsync)
            .WithProblemMetadata<PortfolioView>(StatusCodes.Status200OK, StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound);
        group.MapPut("/{id:int}", UpdateAsync)
            .WithProblemMetadata<PortfolioView>(
                StatusCodes.Status200OK,
                StatusCodes.Status400BadRequest,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status412PreconditionFailed);
        group.MapDelete("/{id:int}", DeleteAsync)
            .WithProblemMetadata(
                StatusCodes.Status204NoContent,
                StatusCodes.Status400BadRequest,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status412PreconditionFailed);
        group.MapPost("/{id:int}/transactions", AddTransactionAsync)
            .WithProblemMetadata<PortfolioView>(StatusCodes.Status201Created, CheckedTransactionWriteProblems);
        group.MapPut("/{id:int}/transactions/{txId:int}", ReplaceTransactionAsync)
            .WithProblemMetadata<PortfolioView>(StatusCodes.Status200OK, CheckedTransactionWriteProblems);
        group.MapDelete("/{id:int}/transactions/{txId:int}", RemoveTransactionAsync)
            .WithProblemMetadata<PortfolioView>(
                StatusCodes.Status200OK,
                StatusCodes.Status400BadRequest,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status412PreconditionFailed,
                StatusCodes.Status422UnprocessableEntity);
        group.MapGet("/{id:int}/valuation", GetValuationAsync)
            .WithProblemMetadata<PortfolioValuationView>(
                StatusCodes.Status200OK,
                StatusCodes.Status400BadRequest,
                StatusCodes.Status404NotFound,
                StatusCodes.Status429TooManyRequests,
                StatusCodes.Status502BadGateway,
                StatusCodes.Status504GatewayTimeout);
    }

    /// <summary><c>GET /portfolios</c> — every portfolio.</summary>
    public static async Task<IResult> ListAsync(IPortfolioService service, CancellationToken cancellationToken)
    {
        var result = await service.ListAsync(cancellationToken);
        return ServiceResultHttp.Ok(result);
    }

    /// <summary><c>GET /portfolios/{id}</c> — one portfolio.</summary>
    public static async Task<IResult> GetAsync(int id, IPortfolioService service, HttpResponse response, CancellationToken cancellationToken)
    {
        var idProblem = RequestValidation.ValidateId(id, "id");
        if (idProblem is not null)
        {
            return idProblem;
        }

        var result = await service.GetAsync(id, cancellationToken);
        SetETagOnSuccess(response, result);
        return ServiceResultHttp.Ok(result);
    }

    /// <summary><c>POST /portfolios</c> — creates an empty portfolio.</summary>
    public static async Task<IResult> CreateAsync(
        CreatePortfolioRequest? request, IPortfolioService service, HttpResponse response, CancellationToken cancellationToken)
    {
        var nameProblem = RequestValidation.ValidateName(request?.Name, "name");
        if (nameProblem is not null)
        {
            return nameProblem;
        }

        var currencyProblem = RequestValidation.ValidateScalar(request?.VsCurrency, "vsCurrency");
        if (currencyProblem is not null)
        {
            return currencyProblem;
        }

        var (method, methodProblem) = PortfolioRequestValidation.ParseCostBasisMethod(request?.CostBasisMethod, "costBasisMethod");
        if (methodProblem is not null)
        {
            return methodProblem;
        }

        // ValidateName and ValidateScalar returning null guarantee request, its Name and its VsCurrency are non-null.
        var command = new CreatePortfolioCommand { Name = request!.Name!, VsCurrency = request.VsCurrency!, CostBasisMethod = method };
        var result = await service.CreateAsync(command, cancellationToken);
        SetETagOnSuccess(response, result);
        return ServiceResultHttp.Created(result, view => $"/portfolios/{view.Id}");
    }

    /// <summary><c>PUT /portfolios/{id}</c> — replaces a portfolio's name and cost-basis method.</summary>
    public static async Task<IResult> UpdateAsync(
        int id,
        UpdatePortfolioRequest? request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        IPortfolioService service,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        var idProblem = RequestValidation.ValidateId(id, "id");
        if (idProblem is not null)
        {
            return idProblem;
        }

        var (expectedVersion, ifMatchProblem) = PortfolioETag.ParseIfMatch(ifMatch);
        if (ifMatchProblem is not null)
        {
            return ifMatchProblem;
        }

        var nameProblem = RequestValidation.ValidateName(request?.Name, "name");
        if (nameProblem is not null)
        {
            return nameProblem;
        }

        var (method, methodProblem) = PortfolioRequestValidation.ParseCostBasisMethod(request?.CostBasisMethod, "costBasisMethod");
        if (methodProblem is not null)
        {
            return methodProblem;
        }

        // ValidateName returning null guarantees request and its Name are non-null and non-whitespace.
        var command = new UpdatePortfolioCommand
        {
            PortfolioId = id,
            Name = request!.Name!,
            CostBasisMethod = method,
            ExpectedVersion = expectedVersion,
        };
        var result = await service.UpdateAsync(command, cancellationToken);
        SetETagOnSuccess(response, result);
        return ServiceResultHttp.Ok(result);
    }

    /// <summary><c>DELETE /portfolios/{id}</c> — deletes a portfolio with all its transactions.</summary>
    public static async Task<IResult> DeleteAsync(
        int id, [FromHeader(Name = "If-Match")] string? ifMatch, IPortfolioService service, CancellationToken cancellationToken)
    {
        var idProblem = RequestValidation.ValidateId(id, "id");
        if (idProblem is not null)
        {
            return idProblem;
        }

        var (expectedVersion, ifMatchProblem) = PortfolioETag.ParseIfMatch(ifMatch);
        if (ifMatchProblem is not null)
        {
            return ifMatchProblem;
        }

        var result = await service.DeleteAsync(id, expectedVersion, cancellationToken);
        return ServiceResultHttp.NoContent(result);
    }

    /// <summary><c>POST /portfolios/{id}/transactions</c> — records a transaction; the response is the whole portfolio.</summary>
    public static async Task<IResult> AddTransactionAsync(
        int id,
        TransactionRequest? request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        IPortfolioService service,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        var idProblem = RequestValidation.ValidateId(id, "id");
        if (idProblem is not null)
        {
            return idProblem;
        }

        var (expectedVersion, ifMatchProblem) = PortfolioETag.ParseIfMatch(ifMatch);
        if (ifMatchProblem is not null)
        {
            return ifMatchProblem;
        }

        var (details, transactionProblem) = PortfolioRequestValidation.ParseTransaction(request);
        if (transactionProblem is not null)
        {
            return transactionProblem;
        }

        // ParseTransaction returns details whenever it returns no problem.
        var command = new AddTransactionCommand { PortfolioId = id, Transaction = details!, ExpectedVersion = expectedVersion };
        var result = await service.AddTransactionAsync(command, cancellationToken);
        SetETagOnSuccess(response, result);

        // Transaction ids are never reused, so the one just recorded has the highest.
        return ServiceResultHttp.Created(result, view => $"/portfolios/{view.Id}/transactions/{view.Transactions.Max(t => t.Id)}");
    }

    /// <summary><c>PUT /portfolios/{id}/transactions/{txId}</c> — replaces every detail of a transaction; the response is the whole portfolio.</summary>
    public static async Task<IResult> ReplaceTransactionAsync(
        int id,
        int txId,
        TransactionRequest? request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        IPortfolioService service,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        var idProblem = RequestValidation.ValidateId(id, "id") ?? RequestValidation.ValidateId(txId, "txId");
        if (idProblem is not null)
        {
            return idProblem;
        }

        var (expectedVersion, ifMatchProblem) = PortfolioETag.ParseIfMatch(ifMatch);
        if (ifMatchProblem is not null)
        {
            return ifMatchProblem;
        }

        var (details, transactionProblem) = PortfolioRequestValidation.ParseTransaction(request);
        if (transactionProblem is not null)
        {
            return transactionProblem;
        }

        // ParseTransaction returns details whenever it returns no problem.
        var command = new ReplaceTransactionCommand { PortfolioId = id, TransactionId = txId, Transaction = details!, ExpectedVersion = expectedVersion };
        var result = await service.ReplaceTransactionAsync(command, cancellationToken);
        SetETagOnSuccess(response, result);
        return ServiceResultHttp.Ok(result);
    }

    /// <summary><c>DELETE /portfolios/{id}/transactions/{txId}</c> — deletes a transaction; the response is the whole portfolio.</summary>
    public static async Task<IResult> RemoveTransactionAsync(
        int id,
        int txId,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        IPortfolioService service,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        var idProblem = RequestValidation.ValidateId(id, "id") ?? RequestValidation.ValidateId(txId, "txId");
        if (idProblem is not null)
        {
            return idProblem;
        }

        var (expectedVersion, ifMatchProblem) = PortfolioETag.ParseIfMatch(ifMatch);
        if (ifMatchProblem is not null)
        {
            return ifMatchProblem;
        }

        var command = new RemoveTransactionCommand { PortfolioId = id, TransactionId = txId, ExpectedVersion = expectedVersion };
        var result = await service.RemoveTransactionAsync(command, cancellationToken);
        SetETagOnSuccess(response, result);
        return ServiceResultHttp.Ok(result);
    }

    /// <summary><c>GET /portfolios/{id}/valuation</c> — the portfolio's holdings valued at live prices.</summary>
    public static async Task<IResult> GetValuationAsync(int id, IPortfolioService service, CancellationToken cancellationToken)
    {
        var idProblem = RequestValidation.ValidateId(id, "id");
        if (idProblem is not null)
        {
            return idProblem;
        }

        var result = await service.GetValuationAsync(id, cancellationToken);
        return ServiceResultHttp.Ok(result);
    }

    /// <summary>Tags a successful response with the portfolio's version, so the client can make its next write conditional.</summary>
    private static void SetETagOnSuccess(HttpResponse response, ServiceResult<PortfolioView> result)
    {
        if (result.IsSuccess)
        {
            // ServiceResult<T> guarantees Value is non-null when IsSuccess is true.
            response.Headers.ETag = PortfolioETag.Format(result.Value!.Version);
        }
    }
}

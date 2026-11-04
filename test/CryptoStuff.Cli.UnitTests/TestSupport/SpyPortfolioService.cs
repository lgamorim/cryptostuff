using CryptoStuff.Core;
using CryptoStuff.Core.Portfolios;

namespace CryptoStuff.Cli.UnitTests.TestSupport;

internal sealed class SpyPortfolioService : IPortfolioService
{
    private static readonly PortfolioView DefaultView = new()
    {
        Id = 1,
        Name = "main",
        VsCurrency = "usd",
        CostBasisMethod = CostBasisMethod.AverageCost,
        Version = 1,
        Transactions = [],
    };

    private static readonly PortfolioValuationView DefaultValuation = new()
    {
        PortfolioId = 1,
        VsCurrency = "usd",
        CostBasisMethod = CostBasisMethod.AverageCost,
        Coins = [],
        Totals = new PortfolioValuationTotals
        {
            MarketValue = 0m,
            CostBasis = 0m,
            UnrealizedProfitAndLoss = 0m,
            RealizedProfitAndLoss = 0m,
        },
    };

    public int CallCount { get; private set; }

    public int? CapturedId { get; private set; }

    public CreatePortfolioCommand? CapturedCreateCommand { get; private set; }

    public UpdatePortfolioCommand? CapturedUpdateCommand { get; private set; }

    public AddTransactionCommand? CapturedAddTransactionCommand { get; private set; }

    public ReplaceTransactionCommand? CapturedReplaceTransactionCommand { get; private set; }

    public RemoveTransactionCommand? CapturedRemoveTransactionCommand { get; private set; }

    public CancellationToken CapturedCancellationToken { get; private set; }

    public Exception? ExceptionToThrow { get; set; }

    public ServiceResult<IReadOnlyList<PortfolioView>> ListResult { get; set; } =
        ServiceResult<IReadOnlyList<PortfolioView>>.Success([]);

    public ServiceResult<PortfolioView> GetResult { get; set; } = ServiceResult<PortfolioView>.Success(DefaultView);

    public ServiceResult<PortfolioView> CreateResult { get; set; } = ServiceResult<PortfolioView>.Success(DefaultView);

    public ServiceResult<PortfolioView> UpdateResult { get; set; } = ServiceResult<PortfolioView>.Success(DefaultView);

    public ServiceResult DeleteResult { get; set; } = ServiceResult.Success();

    public ServiceResult<PortfolioView> AddTransactionResult { get; set; } = ServiceResult<PortfolioView>.Success(DefaultView);

    public ServiceResult<PortfolioView> ReplaceTransactionResult { get; set; } = ServiceResult<PortfolioView>.Success(DefaultView);

    public ServiceResult<PortfolioView> RemoveTransactionResult { get; set; } = ServiceResult<PortfolioView>.Success(DefaultView);

    public ServiceResult<PortfolioValuationView> ValuationResult { get; set; } =
        ServiceResult<PortfolioValuationView>.Success(DefaultValuation);

    public void FailAllWith(ServiceErrorCode errorCode, string? errorDetail = null)
    {
        ListResult = ServiceResult<IReadOnlyList<PortfolioView>>.Failure(errorCode, errorDetail);
        GetResult = ServiceResult<PortfolioView>.Failure(errorCode, errorDetail);
        CreateResult = ServiceResult<PortfolioView>.Failure(errorCode, errorDetail);
        UpdateResult = ServiceResult<PortfolioView>.Failure(errorCode, errorDetail);
        DeleteResult = ServiceResult.Failure(errorCode, errorDetail);
        AddTransactionResult = ServiceResult<PortfolioView>.Failure(errorCode, errorDetail);
        ReplaceTransactionResult = ServiceResult<PortfolioView>.Failure(errorCode, errorDetail);
        RemoveTransactionResult = ServiceResult<PortfolioView>.Failure(errorCode, errorDetail);
        ValuationResult = ServiceResult<PortfolioValuationView>.Failure(errorCode, errorDetail);
    }

    public Task<ServiceResult<IReadOnlyList<PortfolioView>>> ListAsync(CancellationToken cancellationToken)
    {
        Record(cancellationToken);
        return Task.FromResult(ListResult);
    }

    public Task<ServiceResult<PortfolioView>> GetAsync(int id, CancellationToken cancellationToken)
    {
        Record(cancellationToken);
        CapturedId = id;
        return Task.FromResult(GetResult);
    }

    public Task<ServiceResult<PortfolioView>> CreateAsync(CreatePortfolioCommand command, CancellationToken cancellationToken)
    {
        Record(cancellationToken);
        CapturedCreateCommand = command;
        return Task.FromResult(CreateResult);
    }

    public Task<ServiceResult<PortfolioView>> UpdateAsync(UpdatePortfolioCommand command, CancellationToken cancellationToken)
    {
        Record(cancellationToken);
        CapturedUpdateCommand = command;
        return Task.FromResult(UpdateResult);
    }

    public Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        Record(cancellationToken);
        CapturedId = id;
        return Task.FromResult(DeleteResult);
    }

    public Task<ServiceResult<PortfolioView>> AddTransactionAsync(AddTransactionCommand command, CancellationToken cancellationToken)
    {
        Record(cancellationToken);
        CapturedAddTransactionCommand = command;
        return Task.FromResult(AddTransactionResult);
    }

    public Task<ServiceResult<PortfolioView>> ReplaceTransactionAsync(ReplaceTransactionCommand command, CancellationToken cancellationToken)
    {
        Record(cancellationToken);
        CapturedReplaceTransactionCommand = command;
        return Task.FromResult(ReplaceTransactionResult);
    }

    public Task<ServiceResult<PortfolioView>> RemoveTransactionAsync(RemoveTransactionCommand command, CancellationToken cancellationToken)
    {
        Record(cancellationToken);
        CapturedRemoveTransactionCommand = command;
        return Task.FromResult(RemoveTransactionResult);
    }

    public Task<ServiceResult<PortfolioValuationView>> GetValuationAsync(int id, CancellationToken cancellationToken)
    {
        Record(cancellationToken);
        CapturedId = id;
        return Task.FromResult(ValuationResult);
    }

    private void Record(CancellationToken cancellationToken)
    {
        CallCount++;
        CapturedCancellationToken = cancellationToken;

        if (ExceptionToThrow is not null)
        {
            throw ExceptionToThrow;
        }
    }
}

using CryptoStuff.Core;
using CryptoStuff.Core.Watchlists;

namespace CryptoStuff.Cli.UnitTests.TestSupport;

internal sealed class SpyWatchlistService : IWatchlistService
{
    private static readonly WatchlistView DefaultView = new() { Id = 1, Name = "majors", CoinIds = [] };

    public int CallCount { get; private set; }

    public int? CapturedId { get; private set; }

    public CreateWatchlistCommand? CapturedCreateCommand { get; private set; }

    public RenameWatchlistCommand? CapturedRenameCommand { get; private set; }

    public WatchlistCoinCommand? CapturedAddCoinCommand { get; private set; }

    public WatchlistCoinCommand? CapturedRemoveCoinCommand { get; private set; }

    public WatchlistPriceQuery? CapturedPriceQuery { get; private set; }

    public CancellationToken CapturedCancellationToken { get; private set; }

    public Exception? ExceptionToThrow { get; set; }

    public ServiceResult<IReadOnlyList<WatchlistView>> ListResult { get; set; } =
        ServiceResult<IReadOnlyList<WatchlistView>>.Success([]);

    public ServiceResult<WatchlistView> GetResult { get; set; } = ServiceResult<WatchlistView>.Success(DefaultView);

    public ServiceResult<WatchlistView> CreateResult { get; set; } = ServiceResult<WatchlistView>.Success(DefaultView);

    public ServiceResult<WatchlistView> RenameResult { get; set; } = ServiceResult<WatchlistView>.Success(DefaultView);

    public ServiceResult DeleteResult { get; set; } = ServiceResult.Success();

    public ServiceResult<WatchlistView> AddCoinResult { get; set; } = ServiceResult<WatchlistView>.Success(DefaultView);

    public ServiceResult<WatchlistView> RemoveCoinResult { get; set; } = ServiceResult<WatchlistView>.Success(DefaultView);

    public ServiceResult<CoinPriceView> PricesResult { get; set; } =
        ServiceResult<CoinPriceView>.Success(new CoinPriceView { Coins = [] });

    public void FailAllWith(ServiceErrorCode errorCode, string? errorDetail = null)
    {
        ListResult = ServiceResult<IReadOnlyList<WatchlistView>>.Failure(errorCode, errorDetail);
        GetResult = ServiceResult<WatchlistView>.Failure(errorCode, errorDetail);
        CreateResult = ServiceResult<WatchlistView>.Failure(errorCode, errorDetail);
        RenameResult = ServiceResult<WatchlistView>.Failure(errorCode, errorDetail);
        DeleteResult = ServiceResult.Failure(errorCode, errorDetail);
        AddCoinResult = ServiceResult<WatchlistView>.Failure(errorCode, errorDetail);
        RemoveCoinResult = ServiceResult<WatchlistView>.Failure(errorCode, errorDetail);
        PricesResult = ServiceResult<CoinPriceView>.Failure(errorCode, errorDetail);
    }

    public Task<ServiceResult<IReadOnlyList<WatchlistView>>> ListAsync(CancellationToken cancellationToken)
    {
        Record(cancellationToken);
        return Task.FromResult(ListResult);
    }

    public Task<ServiceResult<WatchlistView>> GetAsync(int id, CancellationToken cancellationToken)
    {
        Record(cancellationToken);
        CapturedId = id;
        return Task.FromResult(GetResult);
    }

    public Task<ServiceResult<WatchlistView>> CreateAsync(CreateWatchlistCommand command, CancellationToken cancellationToken)
    {
        Record(cancellationToken);
        CapturedCreateCommand = command;
        return Task.FromResult(CreateResult);
    }

    public Task<ServiceResult<WatchlistView>> RenameAsync(RenameWatchlistCommand command, CancellationToken cancellationToken)
    {
        Record(cancellationToken);
        CapturedRenameCommand = command;
        return Task.FromResult(RenameResult);
    }

    public Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        Record(cancellationToken);
        CapturedId = id;
        return Task.FromResult(DeleteResult);
    }

    public Task<ServiceResult<WatchlistView>> AddCoinAsync(WatchlistCoinCommand command, CancellationToken cancellationToken)
    {
        Record(cancellationToken);
        CapturedAddCoinCommand = command;
        return Task.FromResult(AddCoinResult);
    }

    public Task<ServiceResult<WatchlistView>> RemoveCoinAsync(WatchlistCoinCommand command, CancellationToken cancellationToken)
    {
        Record(cancellationToken);
        CapturedRemoveCoinCommand = command;
        return Task.FromResult(RemoveCoinResult);
    }

    public Task<ServiceResult<CoinPriceView>> GetPricesAsync(WatchlistPriceQuery query, CancellationToken cancellationToken)
    {
        Record(cancellationToken);
        CapturedPriceQuery = query;
        return Task.FromResult(PricesResult);
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

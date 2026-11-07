using CryptoStuff.Core;
using CryptoStuff.Core.Alerts;

namespace CryptoStuff.Api.UnitTests.TestSupport;

internal sealed class SpyAlertService : IAlertService
{
    private static readonly AlertView DefaultView = new()
    {
        Id = 1,
        CoinId = "bitcoin",
        VsCurrency = "usd",
        Direction = AlertDirection.Above,
        Threshold = 50000m,
        State = AlertState.Armed,
        TriggeredAt = null,
        TriggeredPrice = null,
        Version = 1,
    };

    public int CallCount { get; private set; }

    public int? CapturedId { get; private set; }

    public int? CapturedExpectedVersion { get; private set; }

    public CreateAlertCommand? CapturedCreateCommand { get; private set; }

    public UpdateAlertCommand? CapturedUpdateCommand { get; private set; }

    public CancellationToken CapturedCancellationToken { get; private set; }

    public ServiceResult<IReadOnlyList<AlertView>> ListResult { get; set; } =
        ServiceResult<IReadOnlyList<AlertView>>.Success([]);

    public ServiceResult<AlertView> GetResult { get; set; } = ServiceResult<AlertView>.Success(DefaultView);

    public ServiceResult<AlertView> CreateResult { get; set; } = ServiceResult<AlertView>.Success(DefaultView);

    public ServiceResult<AlertView> UpdateResult { get; set; } = ServiceResult<AlertView>.Success(DefaultView);

    public ServiceResult DeleteResult { get; set; } = ServiceResult.Success();

    public ServiceResult<AlertView> RearmResult { get; set; } = ServiceResult<AlertView>.Success(DefaultView);

    public void FailAllWith(ServiceErrorCode errorCode, string? errorDetail = null)
    {
        ListResult = ServiceResult<IReadOnlyList<AlertView>>.Failure(errorCode, errorDetail);
        GetResult = ServiceResult<AlertView>.Failure(errorCode, errorDetail);
        CreateResult = ServiceResult<AlertView>.Failure(errorCode, errorDetail);
        UpdateResult = ServiceResult<AlertView>.Failure(errorCode, errorDetail);
        DeleteResult = ServiceResult.Failure(errorCode, errorDetail);
        RearmResult = ServiceResult<AlertView>.Failure(errorCode, errorDetail);
    }

    public Task<ServiceResult<IReadOnlyList<AlertView>>> ListAsync(CancellationToken cancellationToken)
    {
        Record(cancellationToken);
        return Task.FromResult(ListResult);
    }

    public Task<ServiceResult<AlertView>> GetAsync(int id, CancellationToken cancellationToken)
    {
        Record(cancellationToken);
        CapturedId = id;
        return Task.FromResult(GetResult);
    }

    public Task<ServiceResult<AlertView>> CreateAsync(CreateAlertCommand command, CancellationToken cancellationToken)
    {
        Record(cancellationToken);
        CapturedCreateCommand = command;
        return Task.FromResult(CreateResult);
    }

    public Task<ServiceResult<AlertView>> UpdateAsync(UpdateAlertCommand command, CancellationToken cancellationToken)
    {
        Record(cancellationToken);
        CapturedUpdateCommand = command;
        return Task.FromResult(UpdateResult);
    }

    public Task<ServiceResult> DeleteAsync(int id, int? expectedVersion, CancellationToken cancellationToken)
    {
        Record(cancellationToken);
        CapturedId = id;
        CapturedExpectedVersion = expectedVersion;
        return Task.FromResult(DeleteResult);
    }

    public Task<ServiceResult<AlertView>> RearmAsync(int id, int? expectedVersion, CancellationToken cancellationToken)
    {
        Record(cancellationToken);
        CapturedId = id;
        CapturedExpectedVersion = expectedVersion;
        return Task.FromResult(RearmResult);
    }

    private void Record(CancellationToken cancellationToken)
    {
        CallCount++;
        CapturedCancellationToken = cancellationToken;
    }
}

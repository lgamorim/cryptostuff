using CryptoStuff.Core.Alerts;
using CryptoStuff.Core.UnitTests.TestSupport;

namespace CryptoStuff.Core.UnitTests.Alerts;

// The expected version a write may carry: the API's If-Match. Omitted, a
// write is unconditional, which every test in AlertServiceTests covers.
public class AlertPreconditionTests
{
    private const int UnknownId = 999;

    private static readonly DateTimeOffset Now = new(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static (FakeAlertRepository Repository, AlertService Service) CreateSut()
    {
        var repository = new FakeAlertRepository();
        return (repository, new AlertService(repository, new SpyCryptocurrencyService()));
    }

    // Seeds an armed alert at version 1.
    private static PriceAlert SeedAlert(FakeAlertRepository repository) =>
        repository.Seed(PriceAlert.Create("bitcoin", "usd", AlertDirection.Above, 100m));

    // Asserts the result succeeded and returns what it carries.
    private static T SucceededValue<T>(ServiceResult<T> result)
    {
        result.IsSuccess.Should().BeTrue();

        // `!` is safe: the assertion above fails the test unless the result
        // succeeded, and a successful result always carries its value.
        return result.Value!;
    }

    [Fact]
    public async Task Should_ReturnPreconditionFailedNamingVersionsWithoutSaving_When_UpdateAsyncExpectedVersionIsStale()
    {
        var (repository, service) = CreateSut();
        var alert = SeedAlert(repository);
        var command = new UpdateAlertCommand { AlertId = alert.Id, Direction = AlertDirection.Below, Threshold = 50m, ExpectedVersion = 2 };

        var result = await service.UpdateAsync(command, CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.PreconditionFailed);
        result.ErrorDetail.Should().Be($"Alert {alert.Id} is at version 1, not the expected version 2; reload it and try again.");
        alert.Threshold.Should().Be(100m);
        alert.Version.Should().Be(1);
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_SaveAndReturnNextVersion_When_UpdateAsyncExpectedVersionMatches()
    {
        var (repository, service) = CreateSut();
        var alert = SeedAlert(repository);
        var command = new UpdateAlertCommand { AlertId = alert.Id, Direction = AlertDirection.Below, Threshold = 50m, ExpectedVersion = 1 };

        var result = await service.UpdateAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        SucceededValue(result).Version.Should().Be(2);
        repository.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Should_ReturnPreconditionFailedWithoutRemoving_When_DeleteAsyncExpectedVersionIsStale()
    {
        var (repository, service) = CreateSut();
        var alert = SeedAlert(repository);

        var result = await service.DeleteAsync(alert.Id, expectedVersion: 2, CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.PreconditionFailed);
        result.ErrorDetail.Should().Be($"Alert {alert.Id} is at version 1, not the expected version 2; reload it and try again.");
        repository.Saved.Should().ContainSingle();
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_RemoveAndSave_When_DeleteAsyncExpectedVersionMatches()
    {
        var (repository, service) = CreateSut();
        var alert = SeedAlert(repository);

        var result = await service.DeleteAsync(alert.Id, expectedVersion: 1, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_ReturnPreconditionFailedWithoutRearming_When_RearmAsyncExpectedVersionIsStale()
    {
        var (repository, service) = CreateSut();
        var alert = SeedAlert(repository);
        alert.Trigger(105m, Now);

        var result = await service.RearmAsync(alert.Id, expectedVersion: 1, CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.PreconditionFailed);
        result.ErrorDetail.Should().Be($"Alert {alert.Id} is at version 2, not the expected version 1; reload it and try again.");
        alert.State.Should().Be(AlertState.Triggered);
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnPreconditionFailedRatherThanConflict_When_RearmAsyncAlertIsArmedAndExpectedVersionIsStale()
    {
        var (repository, service) = CreateSut();
        var alert = SeedAlert(repository);

        var result = await service.RearmAsync(alert.Id, expectedVersion: 5, CancellationToken.None);

        result.ErrorCode.Should().Be(ServiceErrorCode.PreconditionFailed);
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_SaveAndReturnNextVersion_When_RearmAsyncExpectedVersionMatches()
    {
        var (repository, service) = CreateSut();
        var alert = SeedAlert(repository);
        alert.Trigger(105m, Now);

        var result = await service.RearmAsync(alert.Id, expectedVersion: 2, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        SucceededValue(result).State.Should().Be(AlertState.Armed);
        SucceededValue(result).Version.Should().Be(3);
    }

    [Fact]
    public async Task Should_ReturnNotFoundRatherThanPreconditionFailed_When_AlertDoesNotExistAndAVersionIsExpected()
    {
        var (_, service) = CreateSut();
        var update = new UpdateAlertCommand { AlertId = UnknownId, Direction = AlertDirection.Below, Threshold = 50m, ExpectedVersion = 1 };

        var updated = await service.UpdateAsync(update, CancellationToken.None);
        var deleted = await service.DeleteAsync(UnknownId, expectedVersion: 1, CancellationToken.None);
        var rearmed = await service.RearmAsync(UnknownId, expectedVersion: 1, CancellationToken.None);

        updated.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
        deleted.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
        rearmed.ErrorCode.Should().Be(ServiceErrorCode.NotFound);
    }
}

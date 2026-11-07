using System.Globalization;
using CryptoStuff.Api.Alerts;
using CryptoStuff.Api.UnitTests.TestSupport;
using CryptoStuff.Core;
using CryptoStuff.Core.Alerts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CryptoStuff.Api.UnitTests.Alerts;

public enum AlertRoute
{
    List,
    Get,
    Create,
    Update,
    Delete,
    Rearm,
    Check,
}

public class AlertEndpointsTests
{
    private static readonly AlertView ArmedView = new()
    {
        Id = 7,
        CoinId = "bitcoin",
        VsCurrency = "usd",
        Direction = AlertDirection.Above,
        Threshold = 50000m,
        State = AlertState.Armed,
        TriggeredAt = null,
        TriggeredPrice = null,
        Version = 1,
    };

    private static readonly CreateAlertRequest ValidCreateRequest = new()
    {
        CoinId = "bitcoin",
        VsCurrency = "usd",
        Direction = "above",
        Threshold = 50000m,
    };

    private static readonly UpdateAlertRequest ValidUpdateRequest = new() { Direction = "below", Threshold = 40000m };

    private readonly SpyAlertService _service = new();
    private readonly SpyAlertEvaluator _evaluator = new();

    [Fact]
    public async Task Should_ReturnOkWithAlerts_When_ListSucceeds()
    {
        IReadOnlyList<AlertView> views = [ArmedView];
        _service.ListResult = ServiceResult<IReadOnlyList<AlertView>>.Success(views);

        var result = await AlertEndpoints.ListAsync(_service, CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<IReadOnlyList<AlertView>>>().Subject;
        ok.Value.Should().BeEquivalentTo(views);
    }

    [Fact]
    public async Task Should_ReturnOkAndForwardId_When_GetSucceeds()
    {
        _service.GetResult = ServiceResult<AlertView>.Success(ArmedView);

        var result = await AlertEndpoints.GetAsync(7, _service, CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<AlertView>>().Subject;
        ok.Value.Should().BeEquivalentTo(ArmedView);
        _service.CapturedId.Should().Be(7);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task Should_ReturnValidationProblem_When_GetIdNotPositive(int id)
    {
        var result = await AlertEndpoints.GetAsync(id, _service, CancellationToken.None);

        AssertValidationProblem(result, "id", "id must be a positive whole number.");
    }

    [Fact]
    public async Task Should_ReturnCreatedWithLocationAndForwardCommand_When_CreateSucceeds()
    {
        _service.CreateResult = ServiceResult<AlertView>.Success(ArmedView);
        var request = new CreateAlertRequest { CoinId = "Bitcoin", VsCurrency = "USD", Direction = "Above", Threshold = 50000.5m };

        var result = await AlertEndpoints.CreateAsync(request, _service, CancellationToken.None);

        var created = result.Should().BeOfType<Created<AlertView>>().Subject;
        created.Location.Should().Be("/alerts/7");
        created.Value.Should().BeEquivalentTo(ArmedView);
        _service.CapturedCreateCommand.Should().BeEquivalentTo(new CreateAlertCommand
        {
            CoinId = "Bitcoin",
            VsCurrency = "USD",
            Direction = AlertDirection.Above,
            Threshold = 50000.5m,
        });
    }

    [Fact]
    public async Task Should_ForwardBelow_When_CreateDirectionIsBelow()
    {
        _service.CreateResult = ServiceResult<AlertView>.Success(ArmedView);
        var request = ValidCreateRequest with { Direction = "below" };

        await AlertEndpoints.CreateAsync(request, _service, CancellationToken.None);

        // A valid request always reaches the service, so the command was captured.
        _service.CapturedCreateCommand!.Direction.Should().Be(AlertDirection.Below);
    }

    [Fact]
    public async Task Should_ReturnValidationProblem_When_CreateBodyMissing()
    {
        var result = await AlertEndpoints.CreateAsync(null, _service, CancellationToken.None);

        AssertValidationProblem(result, "coinId", "coinId is required.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Should_ReturnValidationProblem_When_CreateCoinIdBlank(string? coinId)
    {
        var request = ValidCreateRequest with { CoinId = coinId };

        var result = await AlertEndpoints.CreateAsync(request, _service, CancellationToken.None);

        AssertValidationProblem(result, "coinId", "coinId is required.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Should_ReturnValidationProblem_When_CreateVsCurrencyBlank(string? vsCurrency)
    {
        var request = ValidCreateRequest with { VsCurrency = vsCurrency };

        var result = await AlertEndpoints.CreateAsync(request, _service, CancellationToken.None);

        AssertValidationProblem(result, "vsCurrency", "vsCurrency is required.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sideways")]
    [InlineData(" above")]
    [InlineData("0")]
    public async Task Should_ReturnValidationProblem_When_CreateDirectionInvalid(string? direction)
    {
        var request = ValidCreateRequest with { Direction = direction };

        var result = await AlertEndpoints.CreateAsync(request, _service, CancellationToken.None);

        AssertValidationProblem(result, "direction", "direction must be 'above' or 'below'.");
    }

    [Fact]
    public async Task Should_ReturnValidationProblem_When_CreateThresholdMissing()
    {
        var request = ValidCreateRequest with { Threshold = null };

        var result = await AlertEndpoints.CreateAsync(request, _service, CancellationToken.None);

        AssertValidationProblem(result, "threshold", "threshold is required.");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-0.01")]
    public async Task Should_ReturnValidationProblem_When_CreateThresholdNotPositive(string threshold)
    {
        var request = ValidCreateRequest with { Threshold = decimal.Parse(threshold, CultureInfo.InvariantCulture) };

        var result = await AlertEndpoints.CreateAsync(request, _service, CancellationToken.None);

        AssertValidationProblem(result, "threshold", "threshold must be greater than zero.");
    }

    [Fact]
    public async Task Should_ReturnOkAndForwardUnconditionalCommand_When_UpdateSucceeds()
    {
        _service.UpdateResult = ServiceResult<AlertView>.Success(ArmedView);
        var request = new UpdateAlertRequest { Direction = "BELOW", Threshold = 40000.25m };

        var result = await AlertEndpoints.UpdateAsync(7, request, _service, CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<AlertView>>().Subject;
        ok.Value.Should().BeEquivalentTo(ArmedView);
        _service.CapturedUpdateCommand.Should().BeEquivalentTo(new UpdateAlertCommand
        {
            AlertId = 7,
            Direction = AlertDirection.Below,
            Threshold = 40000.25m,
            ExpectedVersion = null,
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task Should_ReturnValidationProblem_When_UpdateIdNotPositive(int id)
    {
        var result = await AlertEndpoints.UpdateAsync(id, ValidUpdateRequest, _service, CancellationToken.None);

        AssertValidationProblem(result, "id", "id must be a positive whole number.");
    }

    [Fact]
    public async Task Should_ReturnValidationProblem_When_UpdateBodyMissing()
    {
        var result = await AlertEndpoints.UpdateAsync(7, null, _service, CancellationToken.None);

        AssertValidationProblem(result, "direction", "direction must be 'above' or 'below'.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sideways")]
    public async Task Should_ReturnValidationProblem_When_UpdateDirectionInvalid(string? direction)
    {
        var request = ValidUpdateRequest with { Direction = direction };

        var result = await AlertEndpoints.UpdateAsync(7, request, _service, CancellationToken.None);

        AssertValidationProblem(result, "direction", "direction must be 'above' or 'below'.");
    }

    [Fact]
    public async Task Should_ReturnValidationProblem_When_UpdateThresholdMissing()
    {
        var request = ValidUpdateRequest with { Threshold = null };

        var result = await AlertEndpoints.UpdateAsync(7, request, _service, CancellationToken.None);

        AssertValidationProblem(result, "threshold", "threshold is required.");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    public async Task Should_ReturnValidationProblem_When_UpdateThresholdNotPositive(string threshold)
    {
        var request = ValidUpdateRequest with { Threshold = decimal.Parse(threshold, CultureInfo.InvariantCulture) };

        var result = await AlertEndpoints.UpdateAsync(7, request, _service, CancellationToken.None);

        AssertValidationProblem(result, "threshold", "threshold must be greater than zero.");
    }

    [Fact]
    public async Task Should_ReturnNoContentAndDeleteUnconditionally_When_DeleteSucceeds()
    {
        var result = await AlertEndpoints.DeleteAsync(7, _service, CancellationToken.None);

        result.Should().BeOfType<NoContent>();
        _service.CapturedId.Should().Be(7);
        _service.CapturedExpectedVersion.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task Should_ReturnValidationProblem_When_DeleteIdNotPositive(int id)
    {
        var result = await AlertEndpoints.DeleteAsync(id, _service, CancellationToken.None);

        AssertValidationProblem(result, "id", "id must be a positive whole number.");
    }

    [Fact]
    public async Task Should_ReturnOkAndRearmUnconditionally_When_RearmSucceeds()
    {
        _service.RearmResult = ServiceResult<AlertView>.Success(ArmedView);

        var result = await AlertEndpoints.RearmAsync(7, _service, CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<AlertView>>().Subject;
        ok.Value.Should().BeEquivalentTo(ArmedView);
        _service.CapturedId.Should().Be(7);
        _service.CapturedExpectedVersion.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task Should_ReturnValidationProblem_When_RearmIdNotPositive(int id)
    {
        var result = await AlertEndpoints.RearmAsync(id, _service, CancellationToken.None);

        AssertValidationProblem(result, "id", "id must be a positive whole number.");
    }

    [Fact]
    public async Task Should_ReturnOkWithTriggeredAlerts_When_CheckSucceeds()
    {
        var evaluation = new AlertEvaluationView
        {
            Triggered =
            [
                new TriggeredAlertView
                {
                    Id = 7,
                    CoinId = "bitcoin",
                    VsCurrency = "usd",
                    Direction = AlertDirection.Above,
                    Threshold = 50000m,
                    Price = 51000m,
                    TriggeredAt = new DateTimeOffset(2026, 11, 7, 12, 0, 0, TimeSpan.Zero),
                },
            ],
        };
        _evaluator.Result = ServiceResult<AlertEvaluationView>.Success(evaluation);

        var result = await AlertEndpoints.CheckAsync(_evaluator, CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<AlertEvaluationView>>().Subject;
        ok.Value.Should().BeEquivalentTo(evaluation);
        _evaluator.CallCount.Should().Be(1);
    }

    [Theory]
    [MemberData(nameof(AllRoutesWithFailures))]
    public async Task Should_ReturnProblemWithDetail_When_TheServiceFails(AlertRoute route, ServiceErrorCode errorCode, int expectedStatus)
    {
        _service.FailAllWith(errorCode, "the detail");
        _evaluator.Result = ServiceResult<AlertEvaluationView>.Failure(errorCode, "the detail");

        var result = await InvokeAsync(route, CancellationToken.None);

        var problem = result.Should().BeOfType<ProblemHttpResult>().Subject;
        problem.StatusCode.Should().Be(expectedStatus);
        problem.ProblemDetails.Detail.Should().Be("the detail");
    }

    [Theory]
    [MemberData(nameof(AllRoutes))]
    public async Task Should_ForwardCancellationToken_When_RouteCalled(AlertRoute route)
    {
        using var source = new CancellationTokenSource();

        await InvokeAsync(route, source.Token);

        var captured = route == AlertRoute.Check ? _evaluator.CapturedCancellationToken : _service.CapturedCancellationToken;
        captured.Should().Be(source.Token);
    }

    public static TheoryData<AlertRoute> AllRoutes() => new(Enum.GetValues<AlertRoute>());

    public static TheoryData<AlertRoute, ServiceErrorCode, int> AllRoutesWithFailures()
    {
        var data = new TheoryData<AlertRoute, ServiceErrorCode, int>();
        foreach (var route in Enum.GetValues<AlertRoute>())
        {
            data.Add(route, ServiceErrorCode.NotFound, StatusCodes.Status404NotFound);
            data.Add(route, ServiceErrorCode.Conflict, StatusCodes.Status409Conflict);
            data.Add(route, ServiceErrorCode.RuleViolated, StatusCodes.Status422UnprocessableEntity);
            data.Add(route, ServiceErrorCode.UpstreamUnavailable, StatusCodes.Status502BadGateway);
        }

        return data;
    }

    private void AssertValidationProblem(IResult result, string field, string message)
    {
        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.ProblemDetails.Errors.Should().ContainKey(field).WhoseValue.Should().Equal(message);
        _service.CallCount.Should().Be(0);
    }

    private Task<IResult> InvokeAsync(AlertRoute route, CancellationToken cancellationToken) =>
        route switch
        {
            AlertRoute.List => AlertEndpoints.ListAsync(_service, cancellationToken),
            AlertRoute.Get => AlertEndpoints.GetAsync(7, _service, cancellationToken),
            AlertRoute.Create => AlertEndpoints.CreateAsync(ValidCreateRequest, _service, cancellationToken),
            AlertRoute.Update => AlertEndpoints.UpdateAsync(7, ValidUpdateRequest, _service, cancellationToken),
            AlertRoute.Delete => AlertEndpoints.DeleteAsync(7, _service, cancellationToken),
            AlertRoute.Rearm => AlertEndpoints.RearmAsync(7, _service, cancellationToken),
            AlertRoute.Check => AlertEndpoints.CheckAsync(_evaluator, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(route), route, null),
        };
}

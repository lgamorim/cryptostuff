using CryptoStuff.Api.UnitTests.TestSupport;
using CryptoStuff.Api.Watchlists;
using CryptoStuff.Core;
using CryptoStuff.Core.Watchlists;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CryptoStuff.Api.UnitTests.Watchlists;

public enum WatchlistRoute
{
    List,
    Create,
    Get,
    Rename,
    Delete,
    AddCoin,
    RemoveCoin,
    Prices,
}

public class WatchlistEndpointsTests
{
    private readonly SpyWatchlistService _service = new();

    [Fact]
    public async Task Should_ReturnOkWithWatchlists_When_ListSucceeds()
    {
        IReadOnlyList<WatchlistView> views = [new() { Id = 1, Name = "majors", CoinIds = ["bitcoin"] }];
        _service.ListResult = ServiceResult<IReadOnlyList<WatchlistView>>.Success(views);

        var result = await WatchlistEndpoints.ListAsync(_service, CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<IReadOnlyList<WatchlistView>>>().Subject;
        ok.Value.Should().BeEquivalentTo(views);
    }

    [Fact]
    public async Task Should_ReturnOkAndForwardId_When_GetSucceeds()
    {
        var view = new WatchlistView { Id = 7, Name = "majors", CoinIds = ["bitcoin"] };
        _service.GetResult = ServiceResult<WatchlistView>.Success(view);

        var result = await WatchlistEndpoints.GetAsync(7, _service, CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<WatchlistView>>().Subject;
        ok.Value.Should().BeEquivalentTo(view);
        _service.CapturedId.Should().Be(7);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task Should_ReturnValidationProblem_When_GetIdNotPositive(int id)
    {
        var result = await WatchlistEndpoints.GetAsync(id, _service, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Status.Should().Be(400);
        problem.ProblemDetails.Errors.Should().ContainKey("id").WhoseValue.Should().Equal("id must be a positive whole number.");
        _service.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnCreatedWithLocation_When_CreateSucceeds()
    {
        var view = new WatchlistView { Id = 7, Name = "majors", CoinIds = [] };
        _service.CreateResult = ServiceResult<WatchlistView>.Success(view);

        var result = await WatchlistEndpoints.CreateAsync(new WatchlistRequest { Name = "majors" }, _service, CancellationToken.None);

        var created = result.Should().BeOfType<Created<WatchlistView>>().Subject;
        created.Location.Should().Be("/watchlists/7");
        created.Value.Should().BeEquivalentTo(view);
        _service.CapturedCreateCommand.Should().BeEquivalentTo(new CreateWatchlistCommand { Name = "majors" });
    }

    [Theory]
    [InlineData(null, "name is required.")]
    [InlineData("", "name is required.")]
    [InlineData("   ", "name is required.")]
    [InlineData("0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789X", "name must be at most 100 characters.")]
    public async Task Should_ReturnValidationProblem_When_CreateNameInvalid(string? name, string expectedMessage)
    {
        var result = await WatchlistEndpoints.CreateAsync(new WatchlistRequest { Name = name }, _service, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Status.Should().Be(400);
        problem.ProblemDetails.Errors.Should().ContainKey("name").WhoseValue.Should().Equal(expectedMessage);
        _service.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnValidationProblem_When_CreateBodyMissing()
    {
        var result = await WatchlistEndpoints.CreateAsync(null, _service, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey("name").WhoseValue.Should().Equal("name is required.");
        _service.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnOkAndForwardCommand_When_RenameSucceeds()
    {
        var view = new WatchlistView { Id = 7, Name = "blue-chips", CoinIds = ["bitcoin"] };
        _service.RenameResult = ServiceResult<WatchlistView>.Success(view);

        var result = await WatchlistEndpoints.RenameAsync(7, new WatchlistRequest { Name = "blue-chips" }, _service, CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<WatchlistView>>().Subject;
        ok.Value.Should().BeEquivalentTo(view);
        _service.CapturedRenameCommand.Should().BeEquivalentTo(new RenameWatchlistCommand { WatchlistId = 7, Name = "blue-chips" });
    }

    [Fact]
    public async Task Should_ReturnValidationProblem_When_RenameIdNotPositive()
    {
        var result = await WatchlistEndpoints.RenameAsync(0, new WatchlistRequest { Name = "blue-chips" }, _service, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey("id").WhoseValue.Should().Equal("id must be a positive whole number.");
        _service.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public async Task Should_ReturnValidationProblem_When_RenameNameInvalid(string? name)
    {
        var result = await WatchlistEndpoints.RenameAsync(7, new WatchlistRequest { Name = name }, _service, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey("name").WhoseValue.Should().Equal("name is required.");
        _service.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnValidationProblem_When_RenameBodyMissing()
    {
        var result = await WatchlistEndpoints.RenameAsync(7, null, _service, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey("name");
        _service.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnNoContentAndForwardId_When_DeleteSucceeds()
    {
        var result = await WatchlistEndpoints.DeleteAsync(7, _service, CancellationToken.None);

        result.Should().BeOfType<NoContent>();
        _service.CapturedId.Should().Be(7);
    }

    [Fact]
    public async Task Should_ReturnValidationProblem_When_DeleteIdNotPositive()
    {
        var result = await WatchlistEndpoints.DeleteAsync(0, _service, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey("id").WhoseValue.Should().Equal("id must be a positive whole number.");
        _service.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnOkAndForwardCommand_When_AddCoinSucceeds()
    {
        var view = new WatchlistView { Id = 7, Name = "majors", CoinIds = ["bitcoin"] };
        _service.AddCoinResult = ServiceResult<WatchlistView>.Success(view);

        var result = await WatchlistEndpoints.AddCoinAsync(7, "Bitcoin", _service, CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<WatchlistView>>().Subject;
        ok.Value.Should().BeEquivalentTo(view);
        _service.CapturedAddCoinCommand.Should().BeEquivalentTo(new WatchlistCoinCommand { WatchlistId = 7, CoinId = "Bitcoin" });
    }

    [Fact]
    public async Task Should_ReturnValidationProblem_When_AddCoinIdNotPositive()
    {
        var result = await WatchlistEndpoints.AddCoinAsync(0, "bitcoin", _service, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey("id").WhoseValue.Should().Equal("id must be a positive whole number.");
        _service.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Should_ReturnValidationProblem_When_AddCoinCoinBlank(string coin)
    {
        var result = await WatchlistEndpoints.AddCoinAsync(7, coin, _service, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey("coin").WhoseValue.Should().Equal("coin is required.");
        _service.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnOkAndForwardCommand_When_RemoveCoinSucceeds()
    {
        var view = new WatchlistView { Id = 7, Name = "majors", CoinIds = [] };
        _service.RemoveCoinResult = ServiceResult<WatchlistView>.Success(view);

        var result = await WatchlistEndpoints.RemoveCoinAsync(7, "Bitcoin", _service, CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<WatchlistView>>().Subject;
        ok.Value.Should().BeEquivalentTo(view);
        _service.CapturedRemoveCoinCommand.Should().BeEquivalentTo(new WatchlistCoinCommand { WatchlistId = 7, CoinId = "Bitcoin" });
    }

    [Fact]
    public async Task Should_ReturnValidationProblem_When_RemoveCoinIdNotPositive()
    {
        var result = await WatchlistEndpoints.RemoveCoinAsync(0, "bitcoin", _service, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey("id").WhoseValue.Should().Equal("id must be a positive whole number.");
        _service.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Should_ReturnValidationProblem_When_RemoveCoinCoinBlank(string coin)
    {
        var result = await WatchlistEndpoints.RemoveCoinAsync(7, coin, _service, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey("coin").WhoseValue.Should().Equal("coin is required.");
        _service.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnOkAndForwardQuery_When_PricesSucceeds()
    {
        var prices = new CoinPriceView
        {
            Coins = [new CoinPrice { CoinId = "bitcoin", Amounts = [new CurrencyAmount { Currency = "usd", Amount = 43000m }] }],
        };
        _service.PricesResult = ServiceResult<CoinPriceView>.Success(prices);

        var result = await WatchlistEndpoints.GetPricesAsync(7, "usd", _service, CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<CoinPriceView>>().Subject;
        ok.Value.Should().BeEquivalentTo(prices);
        _service.CapturedPriceQuery.Should().BeEquivalentTo(new WatchlistPriceQuery { WatchlistId = 7, VsCurrency = "usd" });
    }

    [Fact]
    public async Task Should_ReturnValidationProblem_When_PricesIdNotPositive()
    {
        var result = await WatchlistEndpoints.GetPricesAsync(0, "usd", _service, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey("id").WhoseValue.Should().Equal("id must be a positive whole number.");
        _service.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Should_ReturnValidationProblem_When_PricesCurrencyBlank(string? currency)
    {
        var result = await WatchlistEndpoints.GetPricesAsync(7, currency, _service, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey("currency").WhoseValue.Should().Equal("currency is required.");
        _service.CallCount.Should().Be(0);
    }

    [Theory]
    [MemberData(nameof(AllRoutesWithFailures))]
    public async Task Should_ReturnProblemWithDetail_When_ServiceFails(WatchlistRoute route, ServiceErrorCode errorCode, int expectedStatus)
    {
        _service.FailAllWith(errorCode, "the detail");

        var result = await InvokeAsync(route, CancellationToken.None);

        var problem = result.Should().BeOfType<ProblemHttpResult>().Subject;
        problem.StatusCode.Should().Be(expectedStatus);
        problem.ProblemDetails.Detail.Should().Be("the detail");
    }

    [Theory]
    [MemberData(nameof(AllRoutes))]
    public async Task Should_ForwardCancellationToken_When_RouteCalled(WatchlistRoute route)
    {
        using var source = new CancellationTokenSource();

        await InvokeAsync(route, source.Token);

        _service.CapturedCancellationToken.Should().Be(source.Token);
    }

    public static TheoryData<WatchlistRoute> AllRoutes() => new(Enum.GetValues<WatchlistRoute>());

    public static TheoryData<WatchlistRoute, ServiceErrorCode, int> AllRoutesWithFailures()
    {
        var data = new TheoryData<WatchlistRoute, ServiceErrorCode, int>();
        foreach (var route in Enum.GetValues<WatchlistRoute>())
        {
            data.Add(route, ServiceErrorCode.NotFound, StatusCodes.Status404NotFound);
            data.Add(route, ServiceErrorCode.Conflict, StatusCodes.Status409Conflict);
            data.Add(route, ServiceErrorCode.RuleViolated, StatusCodes.Status422UnprocessableEntity);
        }

        return data;
    }

    private Task<IResult> InvokeAsync(WatchlistRoute route, CancellationToken cancellationToken)
    {
        var request = new WatchlistRequest { Name = "majors" };

        return route switch
        {
            WatchlistRoute.List => WatchlistEndpoints.ListAsync(_service, cancellationToken),
            WatchlistRoute.Create => WatchlistEndpoints.CreateAsync(request, _service, cancellationToken),
            WatchlistRoute.Get => WatchlistEndpoints.GetAsync(7, _service, cancellationToken),
            WatchlistRoute.Rename => WatchlistEndpoints.RenameAsync(7, request, _service, cancellationToken),
            WatchlistRoute.Delete => WatchlistEndpoints.DeleteAsync(7, _service, cancellationToken),
            WatchlistRoute.AddCoin => WatchlistEndpoints.AddCoinAsync(7, "bitcoin", _service, cancellationToken),
            WatchlistRoute.RemoveCoin => WatchlistEndpoints.RemoveCoinAsync(7, "bitcoin", _service, cancellationToken),
            WatchlistRoute.Prices => WatchlistEndpoints.GetPricesAsync(7, "usd", _service, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(route), route, null),
        };
    }
}

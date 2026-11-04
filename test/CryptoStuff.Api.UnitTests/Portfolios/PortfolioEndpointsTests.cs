using System.Globalization;
using CryptoStuff.Api.Portfolios;
using CryptoStuff.Api.UnitTests.TestSupport;
using CryptoStuff.Core;
using CryptoStuff.Core.Portfolios;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CryptoStuff.Api.UnitTests.Portfolios;

public enum PortfolioRoute
{
    List,
    Create,
    Get,
    Update,
    Delete,
    AddTransaction,
    ReplaceTransaction,
    RemoveTransaction,
    Valuation,
}

public class PortfolioEndpointsTests
{
    private readonly SpyPortfolioService _service = new();

    // A recorded transaction is always in the portfolio returned, and the
    // created response's Location names it.
    public PortfolioEndpointsTests() =>
        _service.AddTransactionResult = ServiceResult<PortfolioView>.Success(View() with { Transactions = [TransactionView(1)] });

    private static PortfolioView View(int id = 7, int version = 3) => new()
    {
        Id = id,
        Name = "main",
        VsCurrency = "usd",
        CostBasisMethod = CostBasisMethod.Fifo,
        Version = version,
        Transactions = [],
    };

    private static PortfolioTransactionView TransactionView(int id) => new()
    {
        Id = id,
        Sequence = id,
        Kind = TransactionKind.Buy,
        CoinId = "bitcoin",
        Quantity = 1m,
        UnitPrice = 100m,
        Fee = 0m,
        OccurredAt = new DateTimeOffset(2024, 1, 31, 14, 30, 0, TimeSpan.Zero),
    };

    private static TransactionRequest ValidTransaction() => new()
    {
        Kind = "buy",
        CoinId = "bitcoin",
        Quantity = 0.5m,
        UnitPrice = 42000m,
        OccurredAt = "2024-01-31T14:30:00Z",
    };

    [Fact]
    public async Task Should_ReturnOkWithPortfolios_When_ListSucceeds()
    {
        IReadOnlyList<PortfolioView> views = [View()];
        _service.ListResult = ServiceResult<IReadOnlyList<PortfolioView>>.Success(views);

        var result = await PortfolioEndpoints.ListAsync(_service, CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<IReadOnlyList<PortfolioView>>>().Subject;
        ok.Value.Should().BeEquivalentTo(views);
    }

    [Fact]
    public async Task Should_ReturnOkAndForwardId_When_GetSucceeds()
    {
        var view = View();
        _service.GetResult = ServiceResult<PortfolioView>.Success(view);

        var result = await PortfolioEndpoints.GetAsync(7, _service, new DefaultHttpContext().Response, CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<PortfolioView>>().Subject;
        ok.Value.Should().BeEquivalentTo(view);
        _service.CapturedId.Should().Be(7);
    }

    [Fact]
    public async Task Should_SetStrongETagFromVersion_When_GetSucceeds()
    {
        _service.GetResult = ServiceResult<PortfolioView>.Success(View(version: 3));
        var response = new DefaultHttpContext().Response;

        await PortfolioEndpoints.GetAsync(7, _service, response, CancellationToken.None);

        response.Headers.ETag.ToString().Should().Be("\"3\"");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task Should_ReturnValidationProblem_When_GetIdNotPositive(int id)
    {
        var result = await PortfolioEndpoints.GetAsync(id, _service, new DefaultHttpContext().Response, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Status.Should().Be(400);
        problem.ProblemDetails.Errors.Should().ContainKey("id").WhoseValue.Should().Equal("id must be a positive whole number.");
        _service.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnCreatedWithLocationAndForwardCommand_When_CreateSucceeds()
    {
        var view = View(version: 1);
        _service.CreateResult = ServiceResult<PortfolioView>.Success(view);
        var request = new CreatePortfolioRequest { Name = "main", VsCurrency = "usd", CostBasisMethod = "average-cost" };

        var result = await PortfolioEndpoints.CreateAsync(request, _service, new DefaultHttpContext().Response, CancellationToken.None);

        var created = result.Should().BeOfType<Created<PortfolioView>>().Subject;
        created.Location.Should().Be("/portfolios/7");
        created.Value.Should().BeEquivalentTo(view);
        _service.CapturedCreateCommand.Should().BeEquivalentTo(
            new CreatePortfolioCommand { Name = "main", VsCurrency = "usd", CostBasisMethod = CostBasisMethod.AverageCost });
    }

    [Fact]
    public async Task Should_SetStrongETagFromVersion_When_CreateSucceeds()
    {
        _service.CreateResult = ServiceResult<PortfolioView>.Success(View(version: 1));
        var response = new DefaultHttpContext().Response;
        var request = new CreatePortfolioRequest { Name = "main", VsCurrency = "usd", CostBasisMethod = "fifo" };

        await PortfolioEndpoints.CreateAsync(request, _service, response, CancellationToken.None);

        response.Headers.ETag.ToString().Should().Be("\"1\"");
    }

    [Theory]
    [InlineData(null, "name is required.")]
    [InlineData("", "name is required.")]
    [InlineData("   ", "name is required.")]
    [InlineData("0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789X", "name must be at most 100 characters.")]
    public async Task Should_ReturnValidationProblem_When_CreateNameInvalid(string? name, string expectedMessage)
    {
        var request = new CreatePortfolioRequest { Name = name, VsCurrency = "usd", CostBasisMethod = "fifo" };

        var result = await PortfolioEndpoints.CreateAsync(request, _service, new DefaultHttpContext().Response, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey("name").WhoseValue.Should().Equal(expectedMessage);
        _service.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnValidationProblem_When_CreateBodyMissing()
    {
        var result = await PortfolioEndpoints.CreateAsync(null, _service, new DefaultHttpContext().Response, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey("name").WhoseValue.Should().Equal("name is required.");
        _service.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Should_ReturnValidationProblem_When_CreateVsCurrencyBlank(string? vsCurrency)
    {
        var request = new CreatePortfolioRequest { Name = "main", VsCurrency = vsCurrency, CostBasisMethod = "fifo" };

        var result = await PortfolioEndpoints.CreateAsync(request, _service, new DefaultHttpContext().Response, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey("vsCurrency").WhoseValue.Should().Equal("vsCurrency is required.");
        _service.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("AverageCost")]
    [InlineData("lifo")]
    [InlineData(" fifo")]
    [InlineData("1")]
    public async Task Should_ReturnValidationProblem_When_CreateCostBasisMethodNotAToken(string? costBasisMethod)
    {
        var request = new CreatePortfolioRequest { Name = "main", VsCurrency = "usd", CostBasisMethod = costBasisMethod };

        var result = await PortfolioEndpoints.CreateAsync(request, _service, new DefaultHttpContext().Response, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey("costBasisMethod")
            .WhoseValue.Should().Equal("costBasisMethod must be 'average-cost' or 'fifo'.");
        _service.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnOkAndForwardCommand_When_UpdateSucceeds()
    {
        var view = View(version: 4);
        _service.UpdateResult = ServiceResult<PortfolioView>.Success(view);
        var request = new UpdatePortfolioRequest { Name = "long term", CostBasisMethod = "fifo" };

        var result = await PortfolioEndpoints.UpdateAsync(7, request, null, _service, new DefaultHttpContext().Response, CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<PortfolioView>>().Subject;
        ok.Value.Should().BeEquivalentTo(view);
        _service.CapturedUpdateCommand.Should().BeEquivalentTo(
            new UpdatePortfolioCommand { PortfolioId = 7, Name = "long term", CostBasisMethod = CostBasisMethod.Fifo, ExpectedVersion = null });
    }

    [Fact]
    public async Task Should_SetStrongETagFromNewVersion_When_UpdateSucceeds()
    {
        _service.UpdateResult = ServiceResult<PortfolioView>.Success(View(version: 4));
        var response = new DefaultHttpContext().Response;
        var request = new UpdatePortfolioRequest { Name = "long term", CostBasisMethod = "fifo" };

        await PortfolioEndpoints.UpdateAsync(7, request, null, _service, response, CancellationToken.None);

        response.Headers.ETag.ToString().Should().Be("\"4\"");
    }

    [Fact]
    public async Task Should_ForwardTaggedVersionAsExpectedVersion_When_UpdateIfMatchIsStrongETag()
    {
        var request = new UpdatePortfolioRequest { Name = "long term", CostBasisMethod = "fifo" };

        await PortfolioEndpoints.UpdateAsync(7, request, "\"3\"", _service, new DefaultHttpContext().Response, CancellationToken.None);

        // `!` is safe: a valid request always reaches the service, and a null command fails the assertion anyway.
        _service.CapturedUpdateCommand!.ExpectedVersion.Should().Be(3);
    }

    [Theory]
    [InlineData("3")]
    [InlineData("\"3")]
    [InlineData("\"1\", \"2\"")]
    [InlineData("W/")]
    [InlineData("abc")]
    public async Task Should_ReturnValidationProblem_When_UpdateIfMatchIsNotOneEntityTag(string ifMatch)
    {
        var request = new UpdatePortfolioRequest { Name = "long term", CostBasisMethod = "fifo" };

        var result = await PortfolioEndpoints.UpdateAsync(7, request, ifMatch, _service, new DefaultHttpContext().Response, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey("If-Match")
            .WhoseValue.Should().Equal("If-Match must be * or one entity tag, such as \"3\".");
        _service.CallCount.Should().Be(0);
    }

    // `*` matches any current version, and the portfolio's existence is checked anyway.
    [Fact]
    public async Task Should_UpdateUnconditionally_When_UpdateIfMatchIsWildcard()
    {
        var request = new UpdatePortfolioRequest { Name = "long term", CostBasisMethod = "fifo" };

        var result = await PortfolioEndpoints.UpdateAsync(7, request, "*", _service, new DefaultHttpContext().Response, CancellationToken.None);

        result.Should().BeOfType<Ok<PortfolioView>>();

        // `!` is safe: the assertion above fails unless the request reached the service.
        _service.CapturedUpdateCommand!.ExpectedVersion.Should().BeNull();
    }

    // If-Match compares strongly, so a weak tag never matches; nor does a tag
    // this API never issues. Version 0 is one no portfolio is ever at.
    [Theory]
    [InlineData("W/\"3\"")]
    [InlineData("\"abc\"")]
    [InlineData("\"03\"")]
    [InlineData("\"+3\"")]
    [InlineData("\"-3\"")]
    [InlineData("\"3.0\"")]
    [InlineData("\"\"")]
    [InlineData("\"99999999999\"")]
    public async Task Should_ForwardAVersionNothingIsAt_When_UpdateIfMatchCanNeverMatch(string ifMatch)
    {
        var request = new UpdatePortfolioRequest { Name = "long term", CostBasisMethod = "fifo" };

        await PortfolioEndpoints.UpdateAsync(7, request, ifMatch, _service, new DefaultHttpContext().Response, CancellationToken.None);

        // `!` is safe: a valid request always reaches the service, and a null command fails the assertion anyway.
        _service.CapturedUpdateCommand!.ExpectedVersion.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnValidationProblem_When_UpdateIdNotPositive()
    {
        var request = new UpdatePortfolioRequest { Name = "long term", CostBasisMethod = "fifo" };

        var result = await PortfolioEndpoints.UpdateAsync(0, request, null, _service, new DefaultHttpContext().Response, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey("id").WhoseValue.Should().Equal("id must be a positive whole number.");
        _service.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public async Task Should_ReturnValidationProblem_When_UpdateNameInvalid(string? name)
    {
        var request = new UpdatePortfolioRequest { Name = name, CostBasisMethod = "fifo" };

        var result = await PortfolioEndpoints.UpdateAsync(7, request, null, _service, new DefaultHttpContext().Response, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey("name").WhoseValue.Should().Equal("name is required.");
        _service.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Fifo ")]
    [InlineData("average_cost")]
    public async Task Should_ReturnValidationProblem_When_UpdateCostBasisMethodNotAToken(string? costBasisMethod)
    {
        var request = new UpdatePortfolioRequest { Name = "long term", CostBasisMethod = costBasisMethod };

        var result = await PortfolioEndpoints.UpdateAsync(7, request, null, _service, new DefaultHttpContext().Response, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey("costBasisMethod")
            .WhoseValue.Should().Equal("costBasisMethod must be 'average-cost' or 'fifo'.");
        _service.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnNoContentAndForwardIdAndExpectedVersion_When_DeleteSucceeds()
    {
        var result = await PortfolioEndpoints.DeleteAsync(7, "\"5\"", _service, CancellationToken.None);

        result.Should().BeOfType<NoContent>();
        _service.CapturedId.Should().Be(7);
        _service.CapturedDeleteExpectedVersion.Should().Be(5);
    }

    [Fact]
    public async Task Should_ReturnValidationProblem_When_DeleteIfMatchIsNotOneEntityTag()
    {
        var result = await PortfolioEndpoints.DeleteAsync(7, "5", _service, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey("If-Match");
        _service.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnValidationProblem_When_DeleteIdNotPositive()
    {
        var result = await PortfolioEndpoints.DeleteAsync(-1, null, _service, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey("id").WhoseValue.Should().Equal("id must be a positive whole number.");
        _service.CallCount.Should().Be(0);
    }

    // The newest transaction has the highest id: ids are never reused.
    [Fact]
    public async Task Should_ReturnCreatedAtNewTransactionAndForwardCommand_When_AddTransactionSucceeds()
    {
        var view = View(version: 3) with { Transactions = [TransactionView(12), TransactionView(4)] };
        _service.AddTransactionResult = ServiceResult<PortfolioView>.Success(view);

        var result = await PortfolioEndpoints.AddTransactionAsync(
            7, ValidTransaction(), null, _service, new DefaultHttpContext().Response, CancellationToken.None);

        var created = result.Should().BeOfType<Created<PortfolioView>>().Subject;
        created.Location.Should().Be("/portfolios/7/transactions/12");
        created.Value.Should().BeEquivalentTo(view);
        _service.CapturedAddTransactionCommand.Should().BeEquivalentTo(new AddTransactionCommand
        {
            PortfolioId = 7,
            Transaction = new TransactionDetails
            {
                Kind = TransactionKind.Buy,
                CoinId = "bitcoin",
                Quantity = 0.5m,
                UnitPrice = 42000m,
                Fee = 0m,
                OccurredAt = new DateTimeOffset(2024, 1, 31, 14, 30, 0, TimeSpan.Zero),
            },
            ExpectedVersion = null,
        });
    }

    [Fact]
    public async Task Should_SetStrongETagFromNewVersion_When_AddTransactionSucceeds()
    {
        _service.AddTransactionResult = ServiceResult<PortfolioView>.Success(View(version: 6) with { Transactions = [TransactionView(1)] });
        var response = new DefaultHttpContext().Response;

        await PortfolioEndpoints.AddTransactionAsync(7, ValidTransaction(), null, _service, response, CancellationToken.None);

        response.Headers.ETag.ToString().Should().Be("\"6\"");
    }

    [Fact]
    public async Task Should_ForwardTaggedVersionAsExpectedVersion_When_AddTransactionIfMatchIsStrongETag()
    {
        await PortfolioEndpoints.AddTransactionAsync(7, ValidTransaction(), "\"5\"", _service, new DefaultHttpContext().Response, CancellationToken.None);

        // `!` is safe: a valid request always reaches the service, and a null command fails the assertion anyway.
        _service.CapturedAddTransactionCommand!.ExpectedVersion.Should().Be(5);
    }

    [Fact]
    public async Task Should_ReturnValidationProblem_When_AddTransactionIfMatchIsNotOneEntityTag()
    {
        var result = await PortfolioEndpoints.AddTransactionAsync(7, ValidTransaction(), "W/5", _service, new DefaultHttpContext().Response, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey("If-Match");
        _service.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnValidationProblem_When_AddTransactionIdNotPositive()
    {
        var result = await PortfolioEndpoints.AddTransactionAsync(0, ValidTransaction(), null, _service, new DefaultHttpContext().Response, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey("id").WhoseValue.Should().Equal("id must be a positive whole number.");
        _service.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("purchase")]
    [InlineData("buy ")]
    [InlineData("0")]
    public async Task Should_ReturnValidationProblem_When_AddTransactionKindNotAToken(string? kind)
    {
        var request = ValidTransaction() with { Kind = kind };

        var result = await PortfolioEndpoints.AddTransactionAsync(7, request, null, _service, new DefaultHttpContext().Response, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey("kind").WhoseValue.Should().Equal("kind must be 'buy' or 'sell'.");
        _service.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Should_ReturnValidationProblem_When_AddTransactionCoinIdBlank(string? coinId)
    {
        var request = ValidTransaction() with { CoinId = coinId };

        var result = await PortfolioEndpoints.AddTransactionAsync(7, request, null, _service, new DefaultHttpContext().Response, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey("coinId").WhoseValue.Should().Equal("coinId is required.");
        _service.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData(null, "quantity is required.")]
    [InlineData("0", "quantity must be greater than zero.")]
    [InlineData("-0.5", "quantity must be greater than zero.")]
    public async Task Should_ReturnValidationProblem_When_AddTransactionQuantityMissingOrNotPositive(string? quantity, string expectedMessage)
    {
        var request = ValidTransaction() with { Quantity = quantity is null ? null : decimal.Parse(quantity, CultureInfo.InvariantCulture) };

        var result = await PortfolioEndpoints.AddTransactionAsync(7, request, null, _service, new DefaultHttpContext().Response, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey("quantity").WhoseValue.Should().Equal(expectedMessage);
        _service.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData(null, "unitPrice is required.")]
    [InlineData("-0.01", "unitPrice must be zero or greater.")]
    public async Task Should_ReturnValidationProblem_When_AddTransactionUnitPriceMissingOrNegative(string? unitPrice, string expectedMessage)
    {
        var request = ValidTransaction() with { UnitPrice = unitPrice is null ? null : decimal.Parse(unitPrice, CultureInfo.InvariantCulture) };

        var result = await PortfolioEndpoints.AddTransactionAsync(7, request, null, _service, new DefaultHttpContext().Response, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey("unitPrice").WhoseValue.Should().Equal(expectedMessage);
        _service.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2024-01-31")]
    [InlineData("2024-01-31T14:30:00")]
    [InlineData("31/01/2024 14:30:00Z")]
    [InlineData(" 2024-01-31T14:30:00Z")]
    public async Task Should_ReturnValidationProblem_When_AddTransactionOccurredAtNotAnIso8601Timestamp(string? occurredAt)
    {
        var request = ValidTransaction() with { OccurredAt = occurredAt };

        var result = await PortfolioEndpoints.AddTransactionAsync(7, request, null, _service, new DefaultHttpContext().Response, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey("occurredAt")
            .WhoseValue.Should().Equal("occurredAt must be an ISO 8601 timestamp with a UTC offset, such as 2024-01-31T14:30:00Z.");
        _service.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnValidationProblem_When_AddTransactionFeeNegative()
    {
        var request = ValidTransaction() with { Fee = -0.01m };

        var result = await PortfolioEndpoints.AddTransactionAsync(7, request, null, _service, new DefaultHttpContext().Response, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey("fee").WhoseValue.Should().Equal("fee must be zero or greater.");
        _service.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ForwardFeeKindAndUtcInstant_When_AddTransactionHasFeeOffsetAndUpperCaseKind()
    {
        var request = ValidTransaction() with { Kind = "SELL", Fee = 1.25m, OccurredAt = "2024-01-31T16:30:00+02:00" };

        await PortfolioEndpoints.AddTransactionAsync(7, request, null, _service, new DefaultHttpContext().Response, CancellationToken.None);

        // `!` is safe: a valid request always reaches the service, and a null command fails the assertion anyway.
        var details = _service.CapturedAddTransactionCommand!.Transaction;
        details.Kind.Should().Be(TransactionKind.Sell);
        details.Fee.Should().Be(1.25m);
        details.OccurredAt.Should().Be(new DateTimeOffset(2024, 1, 31, 14, 30, 0, TimeSpan.Zero));
        details.OccurredAt.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public async Task Should_ReturnValidationProblem_When_AddTransactionBodyMissing()
    {
        var result = await PortfolioEndpoints.AddTransactionAsync(7, null, null, _service, new DefaultHttpContext().Response, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey("kind");
        _service.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnOkAndForwardCommand_When_ReplaceTransactionSucceeds()
    {
        var view = View(version: 5) with { Transactions = [TransactionView(12)] };
        _service.ReplaceTransactionResult = ServiceResult<PortfolioView>.Success(view);

        var result = await PortfolioEndpoints.ReplaceTransactionAsync(
            7, 12, ValidTransaction(), "\"4\"", _service, new DefaultHttpContext().Response, CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<PortfolioView>>().Subject;
        ok.Value.Should().BeEquivalentTo(view);
        _service.CapturedReplaceTransactionCommand.Should().BeEquivalentTo(new ReplaceTransactionCommand
        {
            PortfolioId = 7,
            TransactionId = 12,
            Transaction = new TransactionDetails
            {
                Kind = TransactionKind.Buy,
                CoinId = "bitcoin",
                Quantity = 0.5m,
                UnitPrice = 42000m,
                Fee = 0m,
                OccurredAt = new DateTimeOffset(2024, 1, 31, 14, 30, 0, TimeSpan.Zero),
            },
            ExpectedVersion = 4,
        });
    }

    [Fact]
    public async Task Should_SetStrongETagFromNewVersion_When_ReplaceTransactionSucceeds()
    {
        _service.ReplaceTransactionResult = ServiceResult<PortfolioView>.Success(View(version: 5));
        var response = new DefaultHttpContext().Response;

        await PortfolioEndpoints.ReplaceTransactionAsync(7, 12, ValidTransaction(), null, _service, response, CancellationToken.None);

        response.Headers.ETag.ToString().Should().Be("\"5\"");
    }

    [Theory]
    [InlineData(0, 12, "id")]
    [InlineData(7, 0, "txId")]
    [InlineData(7, -12, "txId")]
    public async Task Should_ReturnValidationProblem_When_ReplaceTransactionIdNotPositive(int id, int txId, string field)
    {
        var result = await PortfolioEndpoints.ReplaceTransactionAsync(
            id, txId, ValidTransaction(), null, _service, new DefaultHttpContext().Response, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey(field).WhoseValue.Should().Equal($"{field} must be a positive whole number.");
        _service.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnValidationProblem_When_ReplaceTransactionIfMatchIsNotOneEntityTag()
    {
        var result = await PortfolioEndpoints.ReplaceTransactionAsync(
            7, 12, ValidTransaction(), "4", _service, new DefaultHttpContext().Response, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey("If-Match");
        _service.CallCount.Should().Be(0);
    }

    // ParseTransaction is shared with add, whose tests cover each field; this pins that replace uses it.
    [Fact]
    public async Task Should_ReturnValidationProblem_When_ReplaceTransactionBodyInvalid()
    {
        var request = ValidTransaction() with { Quantity = 0m };

        var result = await PortfolioEndpoints.ReplaceTransactionAsync(7, 12, request, null, _service, new DefaultHttpContext().Response, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey("quantity").WhoseValue.Should().Equal("quantity must be greater than zero.");
        _service.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnOkAndForwardCommand_When_RemoveTransactionSucceeds()
    {
        var view = View(version: 6);
        _service.RemoveTransactionResult = ServiceResult<PortfolioView>.Success(view);

        var result = await PortfolioEndpoints.RemoveTransactionAsync(7, 12, "\"5\"", _service, new DefaultHttpContext().Response, CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<PortfolioView>>().Subject;
        ok.Value.Should().BeEquivalentTo(view);
        _service.CapturedRemoveTransactionCommand.Should().BeEquivalentTo(
            new RemoveTransactionCommand { PortfolioId = 7, TransactionId = 12, ExpectedVersion = 5 });
    }

    [Fact]
    public async Task Should_SetStrongETagFromNewVersion_When_RemoveTransactionSucceeds()
    {
        _service.RemoveTransactionResult = ServiceResult<PortfolioView>.Success(View(version: 6));
        var response = new DefaultHttpContext().Response;

        await PortfolioEndpoints.RemoveTransactionAsync(7, 12, null, _service, response, CancellationToken.None);

        response.Headers.ETag.ToString().Should().Be("\"6\"");
    }

    [Theory]
    [InlineData(-7, 12, "id")]
    [InlineData(7, 0, "txId")]
    public async Task Should_ReturnValidationProblem_When_RemoveTransactionIdNotPositive(int id, int txId, string field)
    {
        var result = await PortfolioEndpoints.RemoveTransactionAsync(id, txId, null, _service, new DefaultHttpContext().Response, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey(field).WhoseValue.Should().Equal($"{field} must be a positive whole number.");
        _service.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnValidationProblem_When_RemoveTransactionIfMatchIsNotOneEntityTag()
    {
        var result = await PortfolioEndpoints.RemoveTransactionAsync(7, 12, "\"5\", \"6\"", _service, new DefaultHttpContext().Response, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey("If-Match");
        _service.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnOkAndForwardId_When_ValuationSucceeds()
    {
        var valuation = new PortfolioValuationView
        {
            PortfolioId = 7,
            VsCurrency = "usd",
            CostBasisMethod = CostBasisMethod.Fifo,
            Coins = [],
            Totals = new PortfolioValuationTotals { MarketValue = 0m, CostBasis = 0m, UnrealizedProfitAndLoss = 0m, RealizedProfitAndLoss = 0m },
        };
        _service.ValuationResult = ServiceResult<PortfolioValuationView>.Success(valuation);

        var result = await PortfolioEndpoints.GetValuationAsync(7, _service, CancellationToken.None);

        var ok = result.Should().BeOfType<Ok<PortfolioValuationView>>().Subject;
        ok.Value.Should().BeEquivalentTo(valuation);
        _service.CapturedId.Should().Be(7);
    }

    [Fact]
    public async Task Should_ReturnValidationProblem_When_ValuationIdNotPositive()
    {
        var result = await PortfolioEndpoints.GetValuationAsync(0, _service, CancellationToken.None);

        var problem = result.Should().BeOfType<ValidationProblem>().Subject;
        problem.ProblemDetails.Errors.Should().ContainKey("id").WhoseValue.Should().Equal("id must be a positive whole number.");
        _service.CallCount.Should().Be(0);
    }

    [Theory]
    [MemberData(nameof(AllRoutesWithFailures))]
    public async Task Should_ReturnProblemWithDetail_When_ServiceFails(PortfolioRoute route, ServiceErrorCode errorCode, int expectedStatus)
    {
        _service.FailAllWith(errorCode, "the detail");

        var result = await InvokeAsync(route, new DefaultHttpContext().Response, CancellationToken.None);

        var problem = result.Should().BeOfType<ProblemHttpResult>().Subject;
        problem.StatusCode.Should().Be(expectedStatus);
        problem.ProblemDetails.Detail.Should().Be("the detail");
    }

    [Theory]
    [MemberData(nameof(AllRoutes))]
    public async Task Should_SetNoETag_When_ServiceFails(PortfolioRoute route)
    {
        _service.FailAllWith(ServiceErrorCode.PreconditionFailed);
        var response = new DefaultHttpContext().Response;

        await InvokeAsync(route, response, CancellationToken.None);

        response.Headers.ETag.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(AllRoutes))]
    public async Task Should_ForwardCancellationToken_When_RouteCalled(PortfolioRoute route)
    {
        using var source = new CancellationTokenSource();

        await InvokeAsync(route, new DefaultHttpContext().Response, source.Token);

        _service.CapturedCancellationToken.Should().Be(source.Token);
    }

    public static TheoryData<PortfolioRoute> AllRoutes() => new(Enum.GetValues<PortfolioRoute>());

    public static TheoryData<PortfolioRoute, ServiceErrorCode, int> AllRoutesWithFailures()
    {
        var data = new TheoryData<PortfolioRoute, ServiceErrorCode, int>();
        foreach (var route in Enum.GetValues<PortfolioRoute>())
        {
            data.Add(route, ServiceErrorCode.NotFound, StatusCodes.Status404NotFound);
            data.Add(route, ServiceErrorCode.Conflict, StatusCodes.Status409Conflict);
            data.Add(route, ServiceErrorCode.RuleViolated, StatusCodes.Status422UnprocessableEntity);
            data.Add(route, ServiceErrorCode.PreconditionFailed, StatusCodes.Status412PreconditionFailed);
        }

        return data;
    }

    private Task<IResult> InvokeAsync(PortfolioRoute route, HttpResponse response, CancellationToken cancellationToken) => route switch
    {
        PortfolioRoute.List => PortfolioEndpoints.ListAsync(_service, cancellationToken),
        PortfolioRoute.Create => PortfolioEndpoints.CreateAsync(
            new CreatePortfolioRequest { Name = "main", VsCurrency = "usd", CostBasisMethod = "fifo" }, _service, response, cancellationToken),
        PortfolioRoute.Get => PortfolioEndpoints.GetAsync(7, _service, response, cancellationToken),
        PortfolioRoute.Update => PortfolioEndpoints.UpdateAsync(
            7, new UpdatePortfolioRequest { Name = "main", CostBasisMethod = "fifo" }, "\"3\"", _service, response, cancellationToken),
        PortfolioRoute.Delete => PortfolioEndpoints.DeleteAsync(7, "\"3\"", _service, cancellationToken),
        PortfolioRoute.AddTransaction => PortfolioEndpoints.AddTransactionAsync(7, ValidTransaction(), "\"3\"", _service, response, cancellationToken),
        PortfolioRoute.ReplaceTransaction => PortfolioEndpoints.ReplaceTransactionAsync(
            7, 12, ValidTransaction(), "\"3\"", _service, response, cancellationToken),
        PortfolioRoute.RemoveTransaction => PortfolioEndpoints.RemoveTransactionAsync(7, 12, "\"3\"", _service, response, cancellationToken),
        PortfolioRoute.Valuation => PortfolioEndpoints.GetValuationAsync(7, _service, cancellationToken),
        _ => throw new ArgumentOutOfRangeException(nameof(route), route, null),
    };
}

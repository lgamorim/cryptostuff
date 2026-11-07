using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CryptoStuff.Api.IntegrationTests.TestSupport;
using CryptoStuff.CoinGecko;
using CryptoStuff.Core.Portfolios;
using CryptoStuff.Persistence;
using CryptoStuff.Persistence.Portfolios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoStuff.Api.IntegrationTests.Portfolios;

public class PortfolioApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower) },
    };

    [Fact]
    public async Task Should_ReturnCreatedWithLocationAndETag_When_PortfolioCreated()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/portfolios", new { name = "main", vsCurrency = "USD", costBasisMethod = "average-cost" }, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.OriginalString.Should().Be("/portfolios/1");
        response.Headers.ETag!.Tag.Should().Be("\"1\"");
        response.Headers.ETag.IsWeak.Should().BeFalse();
    }

    [Fact]
    public async Task Should_ReturnPortfolioWithLowercasedCurrencyAndKebabCaseTokens_When_PortfolioCreated()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/portfolios", new { name = "main", vsCurrency = "USD", costBasisMethod = "average-cost" }, cancellationToken);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        body.GetProperty("id").GetInt32().Should().Be(1);
        body.GetProperty("name").GetString().Should().Be("main");
        body.GetProperty("vsCurrency").GetString().Should().Be("usd");
        body.GetProperty("costBasisMethod").GetString().Should().Be("average-cost");
        body.GetProperty("version").GetInt32().Should().Be(1);
        body.GetProperty("transactions").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnPortfolioAndETag_When_GetByIdAfterCreate()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClient();
        var created = await CreateAsync(client, "main", cancellationToken);

        var response = await client.GetAsync($"/portfolios/{created.Id}", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.ETag!.Tag.Should().Be("\"1\"");
        var body = await response.Content.ReadFromJsonAsync<PortfolioView>(JsonOptions, cancellationToken);
        body.Should().BeEquivalentTo(created);
    }

    [Fact]
    public async Task Should_ReturnAllPortfoliosInIdOrder_When_ListRequested()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClient();
        var first = await CreateAsync(client, "main", cancellationToken);
        var second = await CreateAsync(client, "trading", cancellationToken);

        var response = await client.GetAsync("/portfolios", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<PortfolioView>>(JsonOptions, cancellationToken);
        body.Should().BeEquivalentTo([first, second], options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task Should_ReplaceNameAndMethodAndReturnNewETag_When_UpdateRequested()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClient();
        var created = await CreateAsync(client, "main", cancellationToken);

        var response = await client.PutAsJsonAsync(
            $"/portfolios/{created.Id}", new { name = "long term", costBasisMethod = "average-cost" }, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.ETag!.Tag.Should().Be("\"2\"");
        var reloaded = await client.GetFromJsonAsync<PortfolioView>($"/portfolios/{created.Id}", JsonOptions, cancellationToken);
        reloaded.Should().BeEquivalentTo(created with { Name = "long term", CostBasisMethod = CostBasisMethod.AverageCost, Version = 2 });
    }

    [Fact]
    public async Task Should_ReturnNoContentThenNotFound_When_PortfolioDeleted()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClient();
        var created = await CreateAsync(client, "main", cancellationToken);

        var response = await client.DeleteAsync($"/portfolios/{created.Id}", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync(cancellationToken)).Should().BeEmpty();
        var reread = await client.GetAsync($"/portfolios/{created.Id}", cancellationToken);
        reread.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_WriteTransactionTimeAsUtcEndingInZ_When_TransactionAddedWithAnotherOffset()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        factory.CoinGeckoClient.PriceMatrixResponse = BitcoinPricedInUsd();
        using var client = factory.CreateClient();
        var created = await CreateAsync(client, "main", cancellationToken);

        var response = await client.PostAsJsonAsync(
            $"/portfolios/{created.Id}/transactions",
            new { kind = "buy", coinId = "bitcoin", quantity = 0.5m, unitPrice = 42000m, occurredAt = "2024-01-31T16:30:00+02:00" },
            cancellationToken);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        body.GetProperty("transactions")[0].GetProperty("occurredAt").GetString().Should().Be("2024-01-31T14:30:00Z");
    }

    [Fact]
    public async Task Should_ReturnCreatedAtTransactionWithPortfolioAndNewETag_When_TransactionAdded()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        factory.CoinGeckoClient.PriceMatrixResponse = BitcoinPricedInUsd();
        using var client = factory.CreateClient();
        var created = await CreateAsync(client, "main", cancellationToken);

        var response = await client.PostAsJsonAsync(
            $"/portfolios/{created.Id}/transactions",
            new { kind = "buy", coinId = "Bitcoin", quantity = 0.5m, unitPrice = 42000m, fee = 2.5m, occurredAt = "2024-01-31T16:30:00+02:00" },
            cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.OriginalString.Should().Be($"/portfolios/{created.Id}/transactions/1");
        response.Headers.ETag!.Tag.Should().Be("\"2\"");
        var body = await response.Content.ReadFromJsonAsync<PortfolioView>(JsonOptions, cancellationToken);

        // `!` is safe: a 201 always carries the portfolio.
        body!.Version.Should().Be(2);
        body.Transactions.Should().BeEquivalentTo([new PortfolioTransactionView
        {
            Id = 1,
            Sequence = 1,
            Kind = TransactionKind.Buy,
            CoinId = "bitcoin",
            Quantity = 0.5m,
            UnitPrice = 42000m,
            Fee = 2.5m,
            OccurredAt = new DateTimeOffset(2024, 1, 31, 14, 30, 0, TimeSpan.Zero),
        }]);
    }

    [Fact]
    public async Task Should_ReplaceTransactionAndReturnNewETag_When_TransactionReplaced()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        factory.CoinGeckoClient.PriceMatrixResponse = BitcoinPricedInUsd();
        using var client = factory.CreateClient();
        var created = await CreateAsync(client, "main", cancellationToken);
        var added = await AddAsync(client, created.Id, Buy(0.5m), cancellationToken);
        var transactionId = added.Transactions.Single().Id;

        var response = await client.PutAsJsonAsync(
            $"/portfolios/{created.Id}/transactions/{transactionId}", Buy(2m, "2024-02-01T00:00:00Z"), cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.ETag!.Tag.Should().Be("\"3\"");
        var reloaded = await client.GetFromJsonAsync<PortfolioView>($"/portfolios/{created.Id}", JsonOptions, cancellationToken);

        // `!` is safe: the GET above succeeds, or GetFromJsonAsync throws.
        var transaction = reloaded!.Transactions.Single();
        transaction.Id.Should().Be(transactionId);
        transaction.Quantity.Should().Be(2m);
        transaction.OccurredAt.Should().Be(new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task Should_RemoveTransactionAndReturnPortfolioWithNewETag_When_TransactionDeleted()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        factory.CoinGeckoClient.PriceMatrixResponse = BitcoinPricedInUsd();
        using var client = factory.CreateClient();
        var created = await CreateAsync(client, "main", cancellationToken);
        var added = await AddAsync(client, created.Id, Buy(0.5m), cancellationToken);

        var response = await client.DeleteAsync($"/portfolios/{created.Id}/transactions/{added.Transactions.Single().Id}", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.ETag!.Tag.Should().Be("\"3\"");
        var body = await response.Content.ReadFromJsonAsync<PortfolioView>(JsonOptions, cancellationToken);

        // `!` is safe: a 200 always carries the portfolio.
        body!.Transactions.Should().BeEmpty();
        body.Version.Should().Be(3);
    }

    [Fact]
    public async Task Should_ValueHoldingsAtLivePrices_When_ValuationRequested()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        factory.CoinGeckoClient.PriceMatrixResponse = BitcoinPricedInUsd(50000m);
        using var client = factory.CreateClient();
        var created = await CreateAsync(client, "main", cancellationToken);
        await AddAsync(client, created.Id, Buy(0.5m), cancellationToken);

        var response = await client.GetAsync($"/portfolios/{created.Id}/valuation", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PortfolioValuationView>(JsonOptions, cancellationToken);
        body.Should().BeEquivalentTo(new PortfolioValuationView
        {
            PortfolioId = created.Id,
            VsCurrency = "usd",
            CostBasisMethod = CostBasisMethod.Fifo,
            Coins =
            [
                new CoinValuationView
                {
                    CoinId = "bitcoin",
                    Quantity = 0.5m,
                    MarketValue = 25000m,
                    CostBasis = 21000m,
                    UnrealizedProfitAndLoss = 4000m,
                    RealizedProfitAndLoss = 0m,
                },
            ],
            Totals = new PortfolioValuationTotals
            {
                MarketValue = 25000m,
                CostBasis = 21000m,
                UnrealizedProfitAndLoss = 4000m,
                RealizedProfitAndLoss = 0m,
            },
        });
    }

    [Fact]
    public async Task Should_ReturnUnprocessableEntityNamingCoinAndDateAndRecordNothing_When_SellOversells()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        factory.CoinGeckoClient.PriceMatrixResponse = BitcoinPricedInUsd();
        using var client = factory.CreateClient();
        var created = await CreateAsync(client, "main", cancellationToken);
        await AddAsync(client, created.Id, Buy(0.5m), cancellationToken);

        var response = await client.PostAsJsonAsync(
            $"/portfolios/{created.Id}/transactions", Sell(0.75m, "2024-02-15T09:00:00Z"), cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var problem = await ReadProblemAsync(response, cancellationToken);
        problem.Detail.Should().Contain("bitcoin").And.Contain("2024-02-15T09:00:00Z");
        var reloaded = await client.GetFromJsonAsync<PortfolioView>($"/portfolios/{created.Id}", JsonOptions, cancellationToken);

        // `!` is safe: the GET above succeeds, or GetFromJsonAsync throws.
        reloaded!.Transactions.Should().ContainSingle();
        reloaded.Version.Should().Be(2);
    }

    [Fact]
    public async Task Should_ReturnPreconditionFailedAndChangeNothing_When_IfMatchIsStale()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClient();
        var created = await CreateAsync(client, "main", cancellationToken);
        await client.PutAsJsonAsync($"/portfolios/{created.Id}", new { name = "renamed", costBasisMethod = "fifo" }, cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/portfolios/{created.Id}")
        {
            Content = JsonContent.Create(new { name = "stale write", costBasisMethod = "average-cost" }),
        };
        request.Headers.IfMatch.Add(new EntityTagHeaderValue("\"1\""));

        var response = await client.SendAsync(request, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
        response.Headers.ETag.Should().BeNull();
        var problem = await ReadProblemAsync(response, cancellationToken);
        problem.Detail.Should().Be($"Portfolio {created.Id} is at version 2, not the expected version 1; reload it and try again.");
        var reloaded = await client.GetFromJsonAsync<PortfolioView>($"/portfolios/{created.Id}", JsonOptions, cancellationToken);
        reloaded.Should().BeEquivalentTo(created with { Name = "renamed", Version = 2 });
    }

    [Fact]
    public async Task Should_SucceedAndReturnNewETag_When_IfMatchIsTheCurrentETag()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        factory.CoinGeckoClient.PriceMatrixResponse = BitcoinPricedInUsd();
        using var client = factory.CreateClient();
        var created = await CreateAsync(client, "main", cancellationToken);
        var read = await client.GetAsync($"/portfolios/{created.Id}", cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/portfolios/{created.Id}/transactions") { Content = JsonContent.Create(Buy(0.5m)) };

        // `!` is safe: a portfolio response always carries its ETag.
        request.Headers.IfMatch.Add(read.Headers.ETag!);

        var response = await client.SendAsync(request, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.ETag!.Tag.Should().Be("\"2\"");
        response.Headers.ETag.Should().NotBe(read.Headers.ETag);
    }

    // The portfolio is at version 1, so "1" would match were it not weak.
    [Theory]
    [InlineData("W/\"1\"", HttpStatusCode.PreconditionFailed)]
    [InlineData("\"one\"", HttpStatusCode.PreconditionFailed)]
    [InlineData("1", HttpStatusCode.BadRequest)]
    [InlineData("\"1\", \"2\"", HttpStatusCode.BadRequest)]
    [InlineData("*", HttpStatusCode.OK)]
    public async Task Should_ReturnStatusForIfMatchForm_When_UpdateCarriesIt(string ifMatch, HttpStatusCode expected)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClient();
        var created = await CreateAsync(client, "main", cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/portfolios/{created.Id}")
        {
            Content = JsonContent.Create(new { name = "renamed", costBasisMethod = "fifo" }),
        };
        request.Headers.TryAddWithoutValidation("If-Match", ifMatch).Should().BeTrue();

        var response = await client.SendAsync(request, cancellationToken);

        response.StatusCode.Should().Be(expected);
    }

    [Theory]
    [InlineData("main")]
    [InlineData("MAIN")]
    public async Task Should_ReturnConflictNamingName_When_CreateNameIsTaken(string name)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClient();
        await CreateAsync(client, "main", cancellationToken);

        var response = await client.PostAsJsonAsync("/portfolios", new { name, vsCurrency = "usd", costBasisMethod = "fifo" }, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadProblemAsync(response, cancellationToken)).Detail.Should().Be($"A portfolio named '{name}' already exists.");
    }

    [Theory]
    [InlineData("GET", "/portfolios/99")]
    [InlineData("PUT", "/portfolios/99")]
    [InlineData("DELETE", "/portfolios/99")]
    [InlineData("POST", "/portfolios/99/transactions")]
    [InlineData("PUT", "/portfolios/99/transactions/1")]
    [InlineData("DELETE", "/portfolios/99/transactions/1")]
    [InlineData("GET", "/portfolios/99/valuation")]
    public async Task Should_ReturnNotFoundNamingId_When_PortfolioIdIsUnknown(string method, string path)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        factory.CoinGeckoClient.PriceMatrixResponse = BitcoinPricedInUsd();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method is "PUT" or "POST")
        {
            request.Content = path.Contains("transactions", StringComparison.Ordinal)
                ? JsonContent.Create(Buy(0.5m))
                : JsonContent.Create(new { name = "renamed", costBasisMethod = "fifo" });
        }

        var response = await client.SendAsync(request, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadProblemAsync(response, cancellationToken)).Detail.Should().Be("No portfolio has id 99.");
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task Should_ReturnNotFoundNamingTransaction_When_TransactionIdIsUnknown(string method)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        factory.CoinGeckoClient.PriceMatrixResponse = BitcoinPricedInUsd();
        using var client = factory.CreateClient();
        var created = await CreateAsync(client, "main", cancellationToken);
        using var request = new HttpRequestMessage(new HttpMethod(method), $"/portfolios/{created.Id}/transactions/42");
        if (method == "PUT")
        {
            request.Content = JsonContent.Create(Buy(0.5m));
        }

        var response = await client.SendAsync(request, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadProblemAsync(response, cancellationToken)).Detail.Should().Be($"Portfolio {created.Id} has no transaction with id 42.");
    }

    [Fact]
    public async Task Should_ReturnUnprocessableEntityNamingCoinAndRecordNothing_When_CoinIsUnknownToCoinGecko()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        factory.CoinGeckoClient.PriceMatrixResponse =
            CoinGeckoResponse<Dictionary<string, Dictionary<string, decimal>>>.Success([], HttpStatusCode.OK);
        using var client = factory.CreateClient();
        var created = await CreateAsync(client, "main", cancellationToken);

        var response = await client.PostAsJsonAsync(
            $"/portfolios/{created.Id}/transactions",
            new { kind = "buy", coinId = "not-a-coin", quantity = 1m, unitPrice = 1m, occurredAt = "2024-01-31T14:30:00Z" },
            cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadProblemAsync(response, cancellationToken)).Detail.Should().Be("CoinGecko has no coin with id 'not-a-coin'.");
        var reloaded = await client.GetFromJsonAsync<PortfolioView>($"/portfolios/{created.Id}", JsonOptions, cancellationToken);

        // `!` is safe: the GET above succeeds, or GetFromJsonAsync throws.
        reloaded!.Transactions.Should().BeEmpty();
    }

    // A body that is not JSON, or puts a string where a number belongs, fails
    // binding; one that binds but breaks a rule fails validation. Both are 400s.
    [Theory]
    [InlineData("POST", "/portfolios", "{ \"name\": ")]
    [InlineData("POST", "/portfolios", "{}")]
    [InlineData("PUT", "/portfolios/1", "{ \"name\": \"main\", \"costBasisMethod\": \"lifo\" }")]
    [InlineData("POST", "/portfolios/1/transactions", "{}")]
    [InlineData("POST", "/portfolios/1/transactions", "{ \"kind\": \"buy\", \"coinId\": \"bitcoin\", \"quantity\": \"1,5\", \"unitPrice\": 1, \"occurredAt\": \"2024-01-31T14:30:00Z\" }")]
    [InlineData("POST", "/portfolios/1/transactions", "{ \"kind\": \"buy\", \"coinId\": \"bitcoin\", \"quantity\": 1, \"unitPrice\": 1, \"occurredAt\": \"2024-01-31\" }")]
    [InlineData("PUT", "/portfolios/1/transactions/1", "[1, 2]")]
    public async Task Should_ReturnBadRequestProblem_When_BodyIsMalformedOrInvalid(string method, string path, string json)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClient();
        await CreateAsync(client, "main", cancellationToken);
        using var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

        var response = await client.SendAsync(request, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Theory]
    [InlineData("/portfolios", "get")]
    [InlineData("/portfolios", "post")]
    [InlineData("/portfolios/{id}", "get")]
    [InlineData("/portfolios/{id}", "put")]
    [InlineData("/portfolios/{id}", "delete")]
    [InlineData("/portfolios/{id}/transactions", "post")]
    [InlineData("/portfolios/{id}/transactions/{txId}", "put")]
    [InlineData("/portfolios/{id}/transactions/{txId}", "delete")]
    [InlineData("/portfolios/{id}/valuation", "get")]
    public async Task Should_ListPortfolioRoute_When_OpenApiDocumentRequested(string path, string method)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClient();

        var document = await ReadOpenApiDocumentAsync(client, cancellationToken);

        document.GetProperty("paths").GetProperty(path).TryGetProperty(method, out _).Should().BeTrue();
    }

    [Theory]
    [InlineData("/portfolios", "get", new[] { "200", "500" })]
    [InlineData("/portfolios", "post", new[] { "201", "400", "409", "500" })]
    [InlineData("/portfolios/{id}", "get", new[] { "200", "400", "404", "500" })]
    [InlineData("/portfolios/{id}", "put", new[] { "200", "400", "404", "409", "412", "500" })]
    [InlineData("/portfolios/{id}", "delete", new[] { "204", "400", "404", "409", "412", "500" })]
    [InlineData("/portfolios/{id}/transactions", "post", new[] { "201", "400", "404", "409", "412", "422", "429", "502", "504", "500" })]
    [InlineData("/portfolios/{id}/transactions/{txId}", "put", new[] { "200", "400", "404", "409", "412", "422", "429", "502", "504", "500" })]
    [InlineData("/portfolios/{id}/transactions/{txId}", "delete", new[] { "200", "400", "404", "409", "412", "422", "500" })]
    [InlineData("/portfolios/{id}/valuation", "get", new[] { "200", "400", "404", "429", "502", "504", "500" })]
    public async Task Should_DocumentResponses_When_OpenApiDocumentRequested(string path, string method, string[] expectedStatuses)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClient();

        var document = await ReadOpenApiDocumentAsync(client, cancellationToken);

        var responses = document.GetProperty("paths").GetProperty(path).GetProperty(method).GetProperty("responses");
        responses.EnumerateObject().Select(response => response.Name).Should().BeEquivalentTo(expectedStatuses);
    }

    // Both writers read version 1. The competitor commits first, so the
    // request's If-Match still held when it was checked; its save is the one
    // that loses on the version token, which is a 409, not a 412.
    [Fact]
    public async Task Should_ReturnConflictAndKeepTheWinningWrite_When_AnotherWriteSavesBetweenReadAndSave()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        var competingWrite = new CompetingWrite();
        using var client = WithCompetingWrite(factory, competingWrite).CreateClient();
        var created = await CreateAsync(client, "main", cancellationToken);
        competingWrite.Arm(token => RenameDirectlyAsync(factory, created.Id, "renamed elsewhere", token));
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/portfolios/{created.Id}")
        {
            Content = JsonContent.Create(new { name = "renamed here", costBasisMethod = "average-cost" }),
        };
        request.Headers.IfMatch.Add(new EntityTagHeaderValue("\"1\""));

        var response = await client.SendAsync(request, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadProblemAsync(response, cancellationToken)).Detail.Should().Contain($"Portfolio {created.Id} could not be saved");
        var reloaded = await client.GetFromJsonAsync<PortfolioView>($"/portfolios/{created.Id}", JsonOptions, cancellationToken);
        reloaded.Should().BeEquivalentTo(created with { Name = "renamed elsewhere", Version = 2 });
    }

    [Fact]
    public async Task Should_ReturnConflictAndRecordNothing_When_AnotherWriteSavesBetweenTransactionReadAndSave()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        factory.CoinGeckoClient.PriceMatrixResponse = BitcoinPricedInUsd();
        var competingWrite = new CompetingWrite();
        using var client = WithCompetingWrite(factory, competingWrite).CreateClient();
        var created = await CreateAsync(client, "main", cancellationToken);
        competingWrite.Arm(token => RenameDirectlyAsync(factory, created.Id, "renamed elsewhere", token));

        var response = await client.PostAsJsonAsync($"/portfolios/{created.Id}/transactions", Buy(0.5m), cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadProblemAsync(response, cancellationToken)).Detail.Should()
            .Be($"Portfolio {created.Id} was changed by another operation; reload it and try again.");
        var reloaded = await client.GetFromJsonAsync<PortfolioView>($"/portfolios/{created.Id}", JsonOptions, cancellationToken);
        reloaded.Should().BeEquivalentTo(created with { Name = "renamed elsewhere", Version = 2 });
    }

    // The API host with its portfolio repository wrapped, so a test can arm a
    // competing write that lands just before the next save.
    private static WebApplicationFactory<Program> WithCompetingWrite(CryptoStuffApiFactory factory, CompetingWrite competingWrite) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<IPortfolioRepository>(provider => new InterleavingPortfolioRepository(
                new PortfolioRepository(provider.GetRequiredService<CryptoStuffDbContext>()), competingWrite))));

    // Another writer: its own context and connection, committing at once.
    private static async Task RenameDirectlyAsync(CryptoStuffApiFactory factory, int portfolioId, string name, CancellationToken cancellationToken)
    {
        await using var context = factory.CreateContext();

        // `!` is safe: the test created this portfolio and nothing deletes it.
        var portfolio = (await context.Portfolios.FindAsync([portfolioId], cancellationToken))!;
        portfolio.Update(name, portfolio.CostBasisMethod);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task<PortfolioView> CreateAsync(HttpClient client, string name, CancellationToken cancellationToken)
    {
        var response = await client.PostAsJsonAsync("/portfolios", new { name, vsCurrency = "usd", costBasisMethod = "fifo" }, cancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // `!` is safe: a 201 always carries the created portfolio.
        return (await response.Content.ReadFromJsonAsync<PortfolioView>(JsonOptions, cancellationToken))!;
    }

    private static async Task<PortfolioView> AddAsync(HttpClient client, int portfolioId, object transaction, CancellationToken cancellationToken)
    {
        var response = await client.PostAsJsonAsync($"/portfolios/{portfolioId}/transactions", transaction, cancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // `!` is safe: a 201 always carries the portfolio.
        return (await response.Content.ReadFromJsonAsync<PortfolioView>(JsonOptions, cancellationToken))!;
    }

    private static async Task<JsonElement> ReadOpenApiDocumentAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var response = await client.GetAsync("/openapi/v1.json", cancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken);
    }

    private static async Task<ProblemDetails> ReadProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken) =>
        // `!` is safe: every error response carries a ProblemDetails body.
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions, cancellationToken))!;

    private static object Sell(decimal quantity, string occurredAt) =>
        new { kind = "sell", coinId = "bitcoin", quantity, unitPrice = 45000m, occurredAt };

    private static object Buy(decimal quantity, string occurredAt = "2024-01-31T14:30:00Z") =>
        new { kind = "buy", coinId = "bitcoin", quantity, unitPrice = 42000m, occurredAt };

    // What CoinGecko returns for every price request: bitcoin, priced in usd.
    private static CoinGeckoResponse<Dictionary<string, Dictionary<string, decimal>>> BitcoinPricedInUsd(decimal price = 43000m) =>
        CoinGeckoResponse<Dictionary<string, Dictionary<string, decimal>>>.Success(
            new() { ["bitcoin"] = new() { ["usd"] = price } },
            HttpStatusCode.OK);
}

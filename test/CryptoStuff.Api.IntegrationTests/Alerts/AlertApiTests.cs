using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CryptoStuff.Api.IntegrationTests.TestSupport;
using CryptoStuff.CoinGecko;
using CryptoStuff.Core.Alerts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoStuff.Api.IntegrationTests.Alerts;

public class AlertApiTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 7, 12, 30, 0, TimeSpan.Zero);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower) },
    };

    [Fact]
    public async Task Should_ReturnCreatedArmedAlertWithLowercasedIdsAndKebabCaseTokens_When_AlertCreated()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        factory.CoinGeckoClient.PriceMatrixResponse = BitcoinPricedInUsd();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/alerts", new { coinId = "Bitcoin", vsCurrency = "USD", direction = "Above", threshold = 50000.5m }, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // The 201 asserted above always carries Location; were it missing, the
        // NullReferenceException would still fail the test.
        response.Headers.Location!.OriginalString.Should().Be("/alerts/1");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        json.GetProperty("direction").GetString().Should().Be("above");
        json.GetProperty("state").GetString().Should().Be("armed");
        var body = json.Deserialize<AlertView>(JsonOptions);
        body.Should().BeEquivalentTo(new AlertView
        {
            Id = 1,
            CoinId = "bitcoin",
            VsCurrency = "usd",
            Direction = AlertDirection.Above,
            Threshold = 50000.5m,
            State = AlertState.Armed,
            TriggeredAt = null,
            TriggeredPrice = null,
            Version = 1,
        });
    }

    [Fact]
    public async Task Should_ReturnAlert_When_GetByIdAfterCreate()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        factory.CoinGeckoClient.PriceMatrixResponse = BitcoinPricedInUsd();
        using var client = factory.CreateClient();
        var created = await CreateAsync(client, cancellationToken);

        var response = await client.GetAsync($"/alerts/{created.Id}", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AlertView>(JsonOptions, cancellationToken);
        body.Should().BeEquivalentTo(created);
    }

    [Fact]
    public async Task Should_ReturnAllAlertsInIdOrder_When_ListRequested()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        factory.CoinGeckoClient.PriceMatrixResponse = BitcoinPricedInUsd();
        using var client = factory.CreateClient();
        var first = await CreateAsync(client, cancellationToken, "above", 50000m);
        var second = await CreateAsync(client, cancellationToken, "below", 30000m);

        var response = await client.GetAsync("/alerts", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<AlertView>>(JsonOptions, cancellationToken);
        body.Should().BeEquivalentTo([first, second], options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task Should_ReplaceDirectionAndThreshold_When_UpdateRequested()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        factory.CoinGeckoClient.PriceMatrixResponse = BitcoinPricedInUsd();
        using var client = factory.CreateClient();
        var created = await CreateAsync(client, cancellationToken);

        var response = await client.PutAsJsonAsync($"/alerts/{created.Id}", new { direction = "below", threshold = 30000.25m }, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var expected = created with { Direction = AlertDirection.Below, Threshold = 30000.25m, Version = 2 };
        (await response.Content.ReadFromJsonAsync<AlertView>(JsonOptions, cancellationToken)).Should().BeEquivalentTo(expected);
        var reloaded = await client.GetFromJsonAsync<AlertView>($"/alerts/{created.Id}", JsonOptions, cancellationToken);
        reloaded.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task Should_ReturnNoContentThenNotFound_When_AlertDeleted()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        factory.CoinGeckoClient.PriceMatrixResponse = BitcoinPricedInUsd();
        using var client = factory.CreateClient();
        var created = await CreateAsync(client, cancellationToken);

        var response = await client.DeleteAsync($"/alerts/{created.Id}", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync($"/alerts/{created.Id}", cancellationToken)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_ReportAndPersistTriggeredAlert_When_CheckFindsItsThresholdMet()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        factory.CoinGeckoClient.PriceMatrixResponse = BitcoinPricedInUsd(43000m);
        using var client = WithClock(factory).CreateClient();
        var met = await CreateAsync(client, cancellationToken, "above", 50000m);
        var unmet = await CreateAsync(client, cancellationToken, "below", 30000m);
        factory.CoinGeckoClient.PriceMatrixResponse = BitcoinPricedInUsd(52000.5m);

        var response = await client.PostAsync("/alerts/check", content: null, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var triggered = json.GetProperty("triggered");
        triggered.GetArrayLength().Should().Be(1);
        triggered[0].GetProperty("direction").GetString().Should().Be("above");
        triggered[0].GetProperty("triggeredAt").GetString().Should().Be("2026-11-07T12:30:00Z");
        json.Deserialize<AlertEvaluationView>(JsonOptions).Should().BeEquivalentTo(new AlertEvaluationView
        {
            Triggered =
            [
                new TriggeredAlertView
                {
                    Id = met.Id,
                    CoinId = "bitcoin",
                    VsCurrency = "usd",
                    Direction = AlertDirection.Above,
                    Threshold = 50000m,
                    Price = 52000.5m,
                    TriggeredAt = Now,
                },
            ],
        });
        var reloaded = await client.GetFromJsonAsync<List<AlertView>>("/alerts", JsonOptions, cancellationToken);
        reloaded.Should().BeEquivalentTo(
            [met with { State = AlertState.Triggered, TriggeredAt = Now, TriggeredPrice = 52000.5m, Version = 2 }, unmet],
            options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task Should_ReturnArmedAlert_When_TriggeredAlertRearmed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        factory.CoinGeckoClient.PriceMatrixResponse = BitcoinPricedInUsd(43000m);
        using var client = WithClock(factory).CreateClient();
        var created = await CreateAsync(client, cancellationToken, "above", 50000m);
        factory.CoinGeckoClient.PriceMatrixResponse = BitcoinPricedInUsd(52000m);
        (await client.PostAsync("/alerts/check", content: null, cancellationToken)).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await client.PostAsync($"/alerts/{created.Id}/rearm", content: null, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var expected = created with { Version = 3 };
        (await response.Content.ReadFromJsonAsync<AlertView>(JsonOptions, cancellationToken)).Should().BeEquivalentTo(expected);
        var reloaded = await client.GetFromJsonAsync<AlertView>($"/alerts/{created.Id}", JsonOptions, cancellationToken);
        reloaded.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task Should_ReturnConflictAndChangeNothing_When_ArmedAlertRearmed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        factory.CoinGeckoClient.PriceMatrixResponse = BitcoinPricedInUsd();
        using var client = factory.CreateClient();
        var created = await CreateAsync(client, cancellationToken);

        var response = await client.PostAsync($"/alerts/{created.Id}/rearm", content: null, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadProblemAsync(response, cancellationToken)).Detail.Should().Be($"Alert {created.Id} is already armed.");
        var reloaded = await client.GetFromJsonAsync<AlertView>($"/alerts/{created.Id}", JsonOptions, cancellationToken);
        reloaded.Should().BeEquivalentTo(created);
    }

    [Theory]
    [InlineData("GET", "/alerts/99")]
    [InlineData("PUT", "/alerts/99")]
    [InlineData("DELETE", "/alerts/99")]
    [InlineData("POST", "/alerts/99/rearm")]
    public async Task Should_ReturnNotFoundNamingId_When_AlertIdIsUnknown(string method, string path)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = JsonContent.Create(new { direction = "below", threshold = 1m }),
        };

        var response = await client.SendAsync(request, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadProblemAsync(response, cancellationToken)).Detail.Should().Be("No alert has id 99.");
    }

    [Fact]
    public async Task Should_ReturnUnprocessableEntityNamingCoinAndRecordNothing_When_CoinIsUnknownToCoinGecko()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        factory.CoinGeckoClient.PriceMatrixResponse =
            CoinGeckoResponse<Dictionary<string, Dictionary<string, decimal>>>.Success([], HttpStatusCode.OK);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/alerts", new { coinId = "not-a-coin", vsCurrency = "usd", direction = "above", threshold = 1m }, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadProblemAsync(response, cancellationToken)).Detail.Should().Be("CoinGecko has no coin with id 'not-a-coin'.");
        await using var context = factory.CreateContext();
        context.Alerts.Should().BeEmpty();
    }

    // A body that is not JSON, or puts a string where a number belongs, fails
    // binding; one that binds but breaks a rule fails validation. Both are 400s.
    [Theory]
    [InlineData("POST", "/alerts", "{ \"coinId\": ")]
    [InlineData("POST", "/alerts", "{}")]
    [InlineData("POST", "/alerts", "{ \"coinId\": \"bitcoin\", \"vsCurrency\": \"usd\", \"direction\": \"sideways\", \"threshold\": 1 }")]
    [InlineData("POST", "/alerts", "{ \"coinId\": \"bitcoin\", \"vsCurrency\": \"usd\", \"direction\": \"above\", \"threshold\": \"1,5\" }")]
    [InlineData("POST", "/alerts", "{ \"coinId\": \"bitcoin\", \"vsCurrency\": \"usd\", \"direction\": \"above\", \"threshold\": 0 }")]
    [InlineData("PUT", "/alerts/1", "{}")]
    [InlineData("PUT", "/alerts/1", "{ \"direction\": \"up\", \"threshold\": 1 }")]
    [InlineData("PUT", "/alerts/1", "{ \"direction\": \"below\", \"threshold\": \"abc\" }")]
    public async Task Should_ReturnBadRequestProblem_When_BodyIsMalformedOrInvalid(string method, string path, string json)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        factory.CoinGeckoClient.PriceMatrixResponse = BitcoinPricedInUsd();
        using var client = factory.CreateClient();
        await CreateAsync(client, cancellationToken);
        using var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

        var response = await client.SendAsync(request, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // The 400 asserted above carries a problem body, so ContentType is set;
        // were it missing, the NullReferenceException would still fail the test.
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Theory]
    [InlineData("/alerts", "get")]
    [InlineData("/alerts", "post")]
    [InlineData("/alerts/{id}", "get")]
    [InlineData("/alerts/{id}", "put")]
    [InlineData("/alerts/{id}", "delete")]
    [InlineData("/alerts/{id}/rearm", "post")]
    [InlineData("/alerts/check", "post")]
    public async Task Should_ListAlertRoute_When_OpenApiDocumentRequested(string path, string method)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClient();

        var document = await ReadOpenApiDocumentAsync(client, cancellationToken);

        document.GetProperty("paths").TryGetProperty(path, out var pathItem).Should().BeTrue();
        pathItem.TryGetProperty(method, out _).Should().BeTrue();
    }

    [Theory]
    [InlineData("/alerts", "get", new[] { "200", "500" })]
    [InlineData("/alerts", "post", new[] { "201", "400", "409", "422", "429", "502", "504", "500" })]
    [InlineData("/alerts/{id}", "get", new[] { "200", "400", "404", "500" })]
    [InlineData("/alerts/{id}", "put", new[] { "200", "400", "404", "409", "500" })]
    [InlineData("/alerts/{id}", "delete", new[] { "204", "400", "404", "409", "500" })]
    [InlineData("/alerts/{id}/rearm", "post", new[] { "200", "400", "404", "409", "500" })]
    [InlineData("/alerts/check", "post", new[] { "200", "429", "502", "504", "500" })]
    public async Task Should_DocumentResponses_When_OpenApiDocumentRequested(string path, string method, string[] expectedStatuses)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClient();

        var document = await ReadOpenApiDocumentAsync(client, cancellationToken);

        var responses = document.GetProperty("paths").GetProperty(path).GetProperty(method).GetProperty("responses");
        responses.EnumerateObject().Select(response => response.Name).Should().BeEquivalentTo(expectedStatuses);
    }

    // The host with its clock pinned at Now, so a trigger's time is exact.
    private static WebApplicationFactory<Program> WithClock(CryptoStuffApiFactory factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now))));

    private static async Task<AlertView> CreateAsync(
        HttpClient client, CancellationToken cancellationToken, string direction = "above", decimal threshold = 50000m)
    {
        var response = await client.PostAsJsonAsync(
            "/alerts", new { coinId = "bitcoin", vsCurrency = "usd", direction, threshold }, cancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // `!` is safe: a 201 always carries the created alert.
        return (await response.Content.ReadFromJsonAsync<AlertView>(JsonOptions, cancellationToken))!;
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

    // What CoinGecko returns for every price request: bitcoin, priced in usd.
    private static CoinGeckoResponse<Dictionary<string, Dictionary<string, decimal>>> BitcoinPricedInUsd(decimal price = 43000m) =>
        CoinGeckoResponse<Dictionary<string, Dictionary<string, decimal>>>.Success(
            new() { ["bitcoin"] = new() { ["usd"] = price } },
            HttpStatusCode.OK);
}

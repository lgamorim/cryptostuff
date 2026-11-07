using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CryptoStuff.Api.IntegrationTests.TestSupport;
using CryptoStuff.CoinGecko;
using CryptoStuff.Core;
using CryptoStuff.Core.Watchlists;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CryptoStuff.Api.IntegrationTests.Watchlists;

public class WatchlistApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Should_ReturnCreatedWithLocation_When_WatchlistCreated()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/watchlists", new { name = "majors" }, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        // `!` is safe: a 201 always carries Location.
        response.Headers.Location!.OriginalString.Should().Be("/watchlists/1");
        var body = await response.Content.ReadFromJsonAsync<WatchlistView>(JsonOptions, cancellationToken);
        body.Should().BeEquivalentTo(new WatchlistView { Id = 1, Name = "majors", CoinIds = [] });
    }

    [Fact]
    public async Task Should_ReturnWatchlist_When_GetByIdAfterCreate()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClient();
        var created = await CreateAsync(client, "majors", cancellationToken);

        var response = await client.GetAsync($"/watchlists/{created.Id}", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<WatchlistView>(JsonOptions, cancellationToken);
        body.Should().BeEquivalentTo(created);
    }

    [Fact]
    public async Task Should_ReturnAllWatchlistsInIdOrder_When_ListRequested()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClient();
        var first = await CreateAsync(client, "majors", cancellationToken);
        var second = await CreateAsync(client, "defi", cancellationToken);

        var response = await client.GetAsync("/watchlists", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<WatchlistView>>(JsonOptions, cancellationToken);
        body.Should().BeEquivalentTo([first, second], options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task Should_ReturnRenamedWatchlist_When_RenameRequested()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClient();
        var created = await CreateAsync(client, "majors", cancellationToken);

        var response = await client.PutAsJsonAsync($"/watchlists/{created.Id}", new { name = "blue-chips" }, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<WatchlistView>(JsonOptions, cancellationToken);
        body.Should().BeEquivalentTo(new WatchlistView { Id = created.Id, Name = "blue-chips", CoinIds = [] });
        var reloaded = await client.GetFromJsonAsync<WatchlistView>($"/watchlists/{created.Id}", JsonOptions, cancellationToken);
        // `!` is safe: the GET above succeeds, or GetFromJsonAsync throws.
        reloaded!.Name.Should().Be("blue-chips");
    }

    [Fact]
    public async Task Should_ReturnNoContentThenNotFound_When_WatchlistDeleted()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClient();
        var created = await CreateAsync(client, "majors", cancellationToken);

        var response = await client.DeleteAsync($"/watchlists/{created.Id}", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync(cancellationToken)).Should().BeEmpty();
        var reread = await client.GetAsync($"/watchlists/{created.Id}", cancellationToken);
        reread.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_AddCoinOnceAndReturnOk_When_AddRepeated()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        factory.CoinGeckoClient.CoinResponse = KnownCoin("bitcoin");
        using var client = factory.CreateClient();
        var created = await CreateAsync(client, "majors", cancellationToken);

        var first = await client.PutAsync($"/watchlists/{created.Id}/coins/Bitcoin", content: null, cancellationToken);
        var second = await client.PutAsync($"/watchlists/{created.Id}/coins/bitcoin", content: null, cancellationToken);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await second.Content.ReadFromJsonAsync<WatchlistView>(JsonOptions, cancellationToken);
        body.Should().BeEquivalentTo(new WatchlistView { Id = created.Id, Name = "majors", CoinIds = ["bitcoin"] });
    }

    [Fact]
    public async Task Should_RemoveCoinAndReturnOk_When_RemoveRepeated()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        factory.CoinGeckoClient.CoinResponse = KnownCoin("bitcoin");
        using var client = factory.CreateClient();
        var created = await CreateAsync(client, "majors", cancellationToken);
        await client.PutAsync($"/watchlists/{created.Id}/coins/bitcoin", content: null, cancellationToken);

        var first = await client.DeleteAsync($"/watchlists/{created.Id}/coins/bitcoin", cancellationToken);
        var second = await client.DeleteAsync($"/watchlists/{created.Id}/coins/bitcoin", cancellationToken);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await second.Content.ReadFromJsonAsync<WatchlistView>(JsonOptions, cancellationToken);
        body.Should().BeEquivalentTo(new WatchlistView { Id = created.Id, Name = "majors", CoinIds = [] });
    }

    [Fact]
    public async Task Should_ReturnPricesOfWatchlistCoins_When_PricesRequested()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        factory.CoinGeckoClient.CoinResponse = KnownCoin("bitcoin");
        factory.CoinGeckoClient.PriceMatrixResponse = CoinGeckoResponse<Dictionary<string, Dictionary<string, decimal>>>.Success(
            new Dictionary<string, Dictionary<string, decimal>> { ["bitcoin"] = new() { ["usd"] = 43000m } },
            HttpStatusCode.OK);
        using var client = factory.CreateClient();
        var created = await CreateAsync(client, "majors", cancellationToken);
        await client.PutAsync($"/watchlists/{created.Id}/coins/bitcoin", content: null, cancellationToken);

        var response = await client.GetAsync($"/watchlists/{created.Id}/prices?currency=usd", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CoinPriceView>(JsonOptions, cancellationToken);
        body.Should().BeEquivalentTo(new CoinPriceView
        {
            Coins = [new CoinPrice { CoinId = "bitcoin", Amounts = [new CurrencyAmount { Currency = "usd", Amount = 43000m }] }],
        });
    }

    [Fact]
    public async Task Should_ReturnValidationProblem_When_CreateBodyIsMalformedJson()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClient();
        using var content = new StringContent("{ \"name\": ", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/watchlists", content, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        // `!` is safe: a 400 always carries its validation-problem body, so ContentType is set.
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Theory]
    [InlineData("majors")]
    [InlineData("MAJORS")]
    public async Task Should_ReturnConflict_When_CreateNameAlreadyTaken(string duplicate)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClient();
        await CreateAsync(client, "majors", cancellationToken);

        var response = await client.PostAsJsonAsync("/watchlists", new { name = duplicate }, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions, cancellationToken);
        // `!` is safe: every error response carries a ProblemDetails body.
        problem!.Title.Should().Be("Conflict");
        problem.Detail.Should().Be($"A watchlist named '{duplicate}' already exists.");
    }

    [Fact]
    public async Task Should_ReturnConflict_When_RenameToNameOfAnotherWatchlist()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClient();
        await CreateAsync(client, "majors", cancellationToken);
        var other = await CreateAsync(client, "defi", cancellationToken);

        var response = await client.PutAsJsonAsync($"/watchlists/{other.Id}", new { name = "Majors" }, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Should_ReturnNotFoundWithDetail_When_IdUnknown()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/watchlists/99", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions, cancellationToken);
        // `!` is safe: every error response carries a ProblemDetails body.
        problem!.Title.Should().Be("Not Found");
        problem.Detail.Should().Contain("99");
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_ChangingAnUnknownWatchlist()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        factory.CoinGeckoClient.CoinResponse = KnownCoin("bitcoin");
        using var client = factory.CreateClient();

        var rename = await client.PutAsJsonAsync("/watchlists/99", new { name = "x" }, cancellationToken);
        var delete = await client.DeleteAsync("/watchlists/99", cancellationToken);
        var add = await client.PutAsync("/watchlists/99/coins/bitcoin", content: null, cancellationToken);
        var remove = await client.DeleteAsync("/watchlists/99/coins/bitcoin", cancellationToken);
        var prices = await client.GetAsync("/watchlists/99/prices?currency=usd", cancellationToken);

        new[] { rename, delete, add, remove, prices }.Select(response => response.StatusCode)
            .Should().OnlyContain(status => status == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_ReturnUnprocessableEntityAndLeaveWatchlistUnchanged_When_CoinUnknown()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        factory.CoinGeckoClient.CoinResponse = CoinGeckoResponse<CoinGeckoCoin>.Failure(HttpStatusCode.NotFound);
        using var client = factory.CreateClient();
        var created = await CreateAsync(client, "majors", cancellationToken);

        var response = await client.PutAsync($"/watchlists/{created.Id}/coins/not-a-coin", content: null, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions, cancellationToken);
        // `!` is safe: every error response carries a ProblemDetails body.
        problem!.Detail.Should().Be("CoinGecko has no coin with id 'not-a-coin'.");
        var reloaded = await client.GetFromJsonAsync<WatchlistView>($"/watchlists/{created.Id}", JsonOptions, cancellationToken);
        // `!` is safe: the GET above succeeds, or GetFromJsonAsync throws.
        reloaded!.CoinIds.Should().BeEmpty();
    }

    [Theory]
    [InlineData("{\"name\": \"\"}")]
    [InlineData("{\"name\": \"   \"}")]
    [InlineData("{}")]
    [InlineData("null")]
    public async Task Should_ReturnValidationProblem_When_CreateNameBlank(string json)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClient();
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/watchlists", content, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(JsonOptions, cancellationToken);
        // `!` is safe: a 400 always carries a validation-problem body.
        problem!.Errors.Should().ContainKey("name").WhoseValue.Should().Equal("name is required.");
    }

    [Fact]
    public async Task Should_ReturnValidationProblemAndKeepName_When_RenameNameBlank()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClient();
        var created = await CreateAsync(client, "majors", cancellationToken);

        var response = await client.PutAsJsonAsync($"/watchlists/{created.Id}", new { name = " " }, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var reloaded = await client.GetFromJsonAsync<WatchlistView>($"/watchlists/{created.Id}", JsonOptions, cancellationToken);
        // `!` is safe: the GET above succeeds, or GetFromJsonAsync throws.
        reloaded!.Name.Should().Be("majors");
    }

    [Fact]
    public async Task Should_ReturnValidationProblem_When_PricesCurrencyMissing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClient();
        var created = await CreateAsync(client, "majors", cancellationToken);

        var response = await client.GetAsync($"/watchlists/{created.Id}/prices", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(JsonOptions, cancellationToken);
        // `!` is safe: a 400 always carries a validation-problem body.
        problem!.Errors.Should().ContainKey("currency").WhoseValue.Should().Equal("currency is required.");
    }

    [Fact]
    public async Task Should_ReturnValidationProblem_When_IdNotPositive()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/watchlists/0", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(JsonOptions, cancellationToken);
        // `!` is safe: a 400 always carries a validation-problem body.
        problem!.Errors.Should().ContainKey("id");
    }

    [Fact]
    public async Task Should_ReturnNoContentOnlyOnce_When_DeleteRepeated()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClient();
        var created = await CreateAsync(client, "majors", cancellationToken);

        var first = await client.DeleteAsync($"/watchlists/{created.Id}", cancellationToken);
        var second = await client.DeleteAsync($"/watchlists/{created.Id}", cancellationToken);

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("/watchlists", "get")]
    [InlineData("/watchlists", "post")]
    [InlineData("/watchlists/{id}", "get")]
    [InlineData("/watchlists/{id}", "put")]
    [InlineData("/watchlists/{id}", "delete")]
    [InlineData("/watchlists/{id}/coins/{coin}", "put")]
    [InlineData("/watchlists/{id}/coins/{coin}", "delete")]
    [InlineData("/watchlists/{id}/prices", "get")]
    public async Task Should_ListWatchlistRoute_When_OpenApiDocumentRequested(string path, string method)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClient();

        var document = await ReadOpenApiDocumentAsync(client, cancellationToken);

        document.GetProperty("paths").GetProperty(path).TryGetProperty(method, out _).Should().BeTrue();
    }

    [Theory]
    [InlineData("/watchlists", "get", new[] { "200", "500" })]
    [InlineData("/watchlists", "post", new[] { "201", "400", "409", "500" })]
    [InlineData("/watchlists/{id}", "get", new[] { "200", "400", "404", "500" })]
    [InlineData("/watchlists/{id}", "put", new[] { "200", "400", "404", "409", "500" })]
    [InlineData("/watchlists/{id}", "delete", new[] { "204", "400", "404", "409", "500" })]
    [InlineData("/watchlists/{id}/coins/{coin}", "put", new[] { "200", "400", "404", "409", "422", "429", "502", "504", "500" })]
    [InlineData("/watchlists/{id}/coins/{coin}", "delete", new[] { "200", "400", "404", "409", "500" })]
    [InlineData("/watchlists/{id}/prices", "get", new[] { "200", "400", "404", "429", "502", "504", "500" })]
    public async Task Should_DocumentResponses_When_OpenApiDocumentRequested(string path, string method, string[] expectedStatuses)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClient();

        var document = await ReadOpenApiDocumentAsync(client, cancellationToken);

        var responses = document.GetProperty("paths").GetProperty(path).GetProperty(method).GetProperty("responses");
        responses.EnumerateObject().Select(response => response.Name).Should().BeEquivalentTo(expectedStatuses);
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_IdNotNumeric()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/watchlists/abc", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable, HttpStatusCode.BadGateway)]
    public async Task Should_ReturnMappedStatus_When_CoinGeckoFailsWhilePricing(HttpStatusCode upstream, HttpStatusCode expected)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        factory.CoinGeckoClient.CoinResponse = KnownCoin("bitcoin");
        factory.CoinGeckoClient.PriceMatrixResponse =
            CoinGeckoResponse<Dictionary<string, Dictionary<string, decimal>>>.Failure(upstream);
        using var client = factory.CreateClient();
        var created = await CreateAsync(client, "majors", cancellationToken);
        await client.PutAsync($"/watchlists/{created.Id}/coins/bitcoin", content: null, cancellationToken);

        var response = await client.GetAsync($"/watchlists/{created.Id}/prices?currency=usd", cancellationToken);

        response.StatusCode.Should().Be(expected);
    }

    private static async Task<WatchlistView> CreateAsync(HttpClient client, string name, CancellationToken cancellationToken)
    {
        var response = await client.PostAsJsonAsync("/watchlists", new { name }, cancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        // `!` is safe: a 201 always carries the created watchlist.
        return (await response.Content.ReadFromJsonAsync<WatchlistView>(JsonOptions, cancellationToken))!;
    }

    private static CoinGeckoResponse<CoinGeckoCoin> KnownCoin(string id) =>
        CoinGeckoResponse<CoinGeckoCoin>.Success(
            new CoinGeckoCoin { Id = id, Symbol = "btc", Name = "Bitcoin", Image = new CoinGeckoImage() },
            HttpStatusCode.OK);

    private static async Task<JsonElement> ReadOpenApiDocumentAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var response = await client.GetAsync("/openapi/v1.json", cancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken);
    }
}

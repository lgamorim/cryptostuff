using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CryptoStuff.Api.IntegrationTests.TestSupport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CryptoStuff.Api.IntegrationTests;

public class ApiKeyAuthenticationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private const string MissingKeyMessage =
        "API key is missing. Choose a secret key and set it locally by running "
        + "'dotnet user-secrets set Api:Key <your-api-key>' from the src/CryptoStuff.Api "
        + "project directory, or set the Api__Key environment variable in deployment. "
        + "Clients send it in the X-Api-Key header.";

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Should_FailToStartBeforeServingAnything_When_ApiKeyIsBlank(string apiKey)
    {
        using var factory = new CryptoStuffApiFactory();
        using var keyless = factory.WithWebHostBuilder(builder => builder.UseSetting("Api:Key", apiKey));

        var act = () => keyless.CreateClient();

        act.Should().Throw<InvalidOperationException>().WithMessage(MissingKeyMessage);
        File.Exists(factory.DatabaseFilePath).Should().BeFalse("the host failed before even its start-up migration ran");
    }

    [Fact]
    public async Task Should_ServeTheKeyWithoutItsPaddingAndStillRejectNoKey_When_ConfiguredApiKeyIsPadded()
    {
        // A real HTTP server strips spaces and tabs around a header's value, so
        // a client can only ever send the configured key without its padding.
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var padded = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Api:Key", " \t" + CryptoStuffApiFactory.ApiKey + " "));
        using var authenticated = padded.CreateClient();
        using var anonymous = padded.CreateClientWithoutApiKey();

        using var served = await authenticated.GetAsync("/watchlists", cancellationToken);
        using var rejected = await anonymous.GetAsync("/watchlists", cancellationToken);

        served.StatusCode.Should().Be(HttpStatusCode.OK);
        rejected.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_RejectEveryRouteBeforeItRuns_When_ApiKeyIsMissing()
    {
        // Every route the host maps, apart from the open ones, is sent a
        // malformed body with no key. Were any of them to run, it would answer
        // 400 for the body, 500 for the database the armed interceptor refuses,
        // or call the fake CoinGecko client; a path no route matches is
        // rejected the same way.
        var cancellationToken = TestContext.Current.CancellationToken;
        var failingOpen = new FailingConnectionOpen();
        using var factory = new CryptoStuffApiFactory(interceptor: failingOpen);
        using var client = factory.CreateClientWithoutApiKey();
        failingOpen.Armed = true;
        var requests = MappedRoutes(factory)
            .Where(route => !IsOpen(route.Pattern))
            .Select(route => (route.Method, Path: Regex.Replace(route.Pattern, @"\{[^}]*\}", "1")))
            .Append(("GET", "/not-a-real-route"))
            .ToList();

        var answers = new List<string>();
        foreach (var (method, path) in requests)
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), path)
            {
                Content = new StringContent("{", Encoding.UTF8, "application/json"),
            };
            using var response = await client.SendAsync(request, cancellationToken);
            answers.Add($"{method} {path} -> {(int)response.StatusCode} {response.Content.Headers.ContentType?.MediaType}");
        }

        answers.Should().OnlyContain(answer => answer.EndsWith(" -> 401 application/problem+json", StringComparison.Ordinal));
        requests.Should().Contain([("GET", "/prices"), ("POST", "/watchlists/"), ("POST", "/alerts/check")]);
        factory.CoinGeckoClient.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("wrong-key")]
    [InlineData("INTEGRATION-TEST-API-KEY")]
    [InlineData("integration-test-api-ke")]
    [InlineData(CryptoStuffApiFactory.ApiKey + "x")]
    public async Task Should_ReturnTheSameUnauthorizedProblem_When_ApiKeyIsMissingBlankOrWrong(string? suppliedKey)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClientWithoutApiKey();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/prices?coins=bitcoin&currencies=usd");
        if (suppliedKey is not null)
        {
            request.Headers.TryAddWithoutValidation(CryptoStuffApiFactory.ApiKeyHeader, suppliedKey);
        }

        using var response = await client.SendAsync(request, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var problem = JsonSerializer.Deserialize<ProblemDetails>(body, JsonOptions);

        // `!` is safe: the content type asserted above is a ProblemDetails body.
        problem!.Status.Should().Be(401);
        problem.Title.Should().Be("Unauthorized");
        problem.Detail.Should().Be("Send a valid API key in the X-Api-Key header.");
        problem.Type.Should().Be("https://tools.ietf.org/html/rfc9110#section-15.5.2");
        var everythingSent = body + response.Headers + response.Content.Headers;
        everythingSent.Should().NotContain(CryptoStuffApiFactory.ApiKey);
        if (!string.IsNullOrWhiteSpace(suppliedKey))
        {
            everythingSent.Should().NotContain(suppliedKey.Trim());
        }

        factory.CoinGeckoClient.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_ReturnUnauthorized_When_TheApiKeyIsSentInTheQueryString()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClientWithoutApiKey();

        using var response = await client.GetAsync(
            $"/watchlists?{CryptoStuffApiFactory.ApiKeyHeader}={CryptoStuffApiFactory.ApiKey}", cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_NeverLogTheConfiguredOrSuppliedKey_When_CallersAreAcceptedOrRejected()
    {
        const string WrongKey = "wrong-key-that-must-not-be-logged";
        var cancellationToken = TestContext.Current.CancellationToken;
        var logs = new CollectingLoggerProvider();
        using var factory = new CryptoStuffApiFactory();
        using var logged = factory.WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging
            .AddProvider(logs)
            .AddFilter<CollectingLoggerProvider>(category: null, LogLevel.Trace)));
        using var client = logged.CreateClientWithoutApiKey();

        (await SendAsync(client, "/watchlists", CryptoStuffApiFactory.ApiKey, cancellationToken)).Dispose();
        (await SendAsync(client, "/watchlists", WrongKey, cancellationToken)).Dispose();
        (await SendAsync(client, "/watchlists", suppliedKey: null, cancellationToken)).Dispose();

        logs.Entries.Should().NotBeEmpty();
        logs.Entries.Should().NotContain(entry => entry.Contains(CryptoStuffApiFactory.ApiKey, StringComparison.Ordinal));
        logs.Entries.Should().NotContain(entry => entry.Contains(WrongKey, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("/health", null)]
    [InlineData("/health", "wrong-key")]
    [InlineData("/health/ready", null)]
    [InlineData("/health/ready", "wrong-key")]
    [InlineData("/openapi/v1.json", null)]
    [InlineData("/openapi/v1.json", "wrong-key")]
    public async Task Should_AnswerAnOpenRoute_When_ApiKeyIsMissingOrWrong(string path, string? suppliedKey)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClientWithoutApiKey();

        using var response = await SendAsync(client, path, suppliedKey, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/scalar/v1", null)]
    [InlineData("/scalar/v1", "wrong-key")]
    public async Task Should_AnswerTheScalarUi_When_ApiKeyIsMissingOrWrongInDevelopment(string path, string? suppliedKey)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var baseFactory = new CryptoStuffApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        using var client = factory.CreateClientWithoutApiKey();

        using var response = await SendAsync(client, path, suppliedKey, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Should_KeepRoutesOpenOrProtectedAsAtTheRoot_When_ServedUnderThePathBase()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var mounted = factory.WithWebHostBuilder(builder => builder.UseSetting("PathBase", "/api"));
        using var anonymous = mounted.CreateClientWithoutApiKey();
        using var authenticated = mounted.CreateClient();

        using var open = await anonymous.GetAsync("/api/health", cancellationToken);
        using var rejected = await anonymous.GetAsync("/api/watchlists", cancellationToken);
        using var served = await authenticated.GetAsync("/api/watchlists", cancellationToken);

        open.StatusCode.Should().Be(HttpStatusCode.OK);
        rejected.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        served.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Should_RequireTheApiKey_When_ARouteIsAddedLaterWithoutDeclaringAnything()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var extended = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IStartupFilter, RouteAddedLater>()));
        using var anonymous = extended.CreateClientWithoutApiKey();
        using var authenticated = extended.CreateClient();

        using var rejected = await anonymous.GetAsync(RouteAddedLater.Path, cancellationToken);
        using var served = await authenticated.GetAsync(RouteAddedLater.Path, cancellationToken);

        rejected.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        served.StatusCode.Should().Be(HttpStatusCode.OK);
        (await served.Content.ReadAsStringAsync(cancellationToken)).Should().Be(RouteAddedLater.Body);
    }

    [Fact]
    public async Task Should_DeclareTheApiKeyHeaderScheme_When_OpenApiDocumentRequested()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClientWithoutApiKey();

        var document = await ReadOpenApiDocumentAsync(client, cancellationToken);

        document.GetProperty("components").TryGetProperty("securitySchemes", out var schemes).Should().BeTrue();
        schemes.TryGetProperty("ApiKey", out var scheme).Should().BeTrue();
        scheme.GetProperty("type").GetString().Should().Be("apiKey");
        scheme.GetProperty("in").GetString().Should().Be("header");
        scheme.GetProperty("name").GetString().Should().Be("X-Api-Key");
    }

    [Fact]
    public async Task Should_RequireTheApiKeySchemeOnEveryDocumentedOperation_When_OpenApiDocumentRequested()
    {
        // The open routes are not API operations: health checks and the
        // document itself never appear in it, so every operation it lists is
        // a protected one.
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClientWithoutApiKey();

        var document = await ReadOpenApiDocumentAsync(client, cancellationToken);

        var operations = DocumentedOperations(document);
        operations.Should().NotBeEmpty();
        operations
            .Where(operation => !RequiresApiKey(operation.Value))
            .Select(operation => operation.Key)
            .Should().BeEmpty();
        document.GetProperty("paths").EnumerateObject().Select(path => path.Name)
            .Should().NotContain(["/health", "/health/ready", "/openapi/{documentName}.json"]);
    }

    [Fact]
    public async Task Should_ListTheUnauthorizedProblemOnEveryDocumentedOperation_When_OpenApiDocumentRequested()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var factory = new CryptoStuffApiFactory();
        using var client = factory.CreateClientWithoutApiKey();

        var document = await ReadOpenApiDocumentAsync(client, cancellationToken);

        var operations = DocumentedOperations(document);
        operations.Should().NotBeEmpty();
        operations
            .Where(operation => !operation.Value.GetProperty("responses").TryGetProperty("401", out var unauthorized)
                || !unauthorized.GetProperty("content").TryGetProperty("application/problem+json", out _))
            .Select(operation => operation.Key)
            .Should().BeEmpty();
    }

    // Every operation in the document, keyed "<method> <path>".
    private static Dictionary<string, JsonElement> DocumentedOperations(JsonElement document) =>
        document.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject().Select(operation => (path, operation)))
            .ToDictionary(pair => $"{pair.operation.Name} {pair.path.Name}", pair => pair.operation.Value);

    private static bool RequiresApiKey(JsonElement operation) =>
        operation.TryGetProperty("security", out var security)
        && security.EnumerateArray().Any(requirement => requirement.TryGetProperty("ApiKey", out _));

    private static async Task<JsonElement> ReadOpenApiDocumentAsync(HttpClient client, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync("/openapi/v1.json", cancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken);
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client, string path, string? suppliedKey, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (suppliedKey is not null)
        {
            request.Headers.TryAddWithoutValidation(CryptoStuffApiFactory.ApiKeyHeader, suppliedKey);
        }

        return await client.SendAsync(request, cancellationToken);
    }

    private static bool IsOpen(string pattern) =>
        pattern is "/health" or "/health/ready" or "/openapi/{documentName}.json"
        || pattern.StartsWith("/scalar", StringComparison.Ordinal);

    // Every route endpoint the running host maps, one entry per HTTP method.
    private static IEnumerable<(string Method, string Pattern)> MappedRoutes(WebApplicationFactory<Program> factory) =>
        factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .SelectMany(endpoint =>
                (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["GET"])
                .Select(method => (method, endpoint.RoutePattern.RawText ?? string.Empty)));
}

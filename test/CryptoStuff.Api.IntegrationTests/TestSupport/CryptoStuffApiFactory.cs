using CryptoStuff.Api.Alerts;
using CryptoStuff.CoinGecko;
using CryptoStuff.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CryptoStuff.Api.IntegrationTests.TestSupport;

/// <summary>
/// Hosts <c>CryptoStuff.Api</c> for real, with only <see cref="ICoinGeckoClient"/>
/// substituted by a <see cref="FakeCoinGeckoClient"/> and the database pointed
/// at a file of this factory's own — every other registration from
/// <c>AddCryptoStuff</c> (the service, the caching decorator, validation, the
/// persistence services) and the API host's own routing and start-up migration
/// run unmodified, except that <see cref="AlertEvaluationBackgroundService"/>
/// is removed unless asked for, so no timer-driven evaluation runs beside a
/// test's own requests.
/// </summary>
/// <remarks>
/// The database file is GUID-named under <c>&lt;OS temp&gt;/cryptostuff-tests</c>,
/// the folder the persistence integration tests use and sweep of stale files,
/// so parallel factories never share one. It is opened with <c>Pooling=False</c>
/// so no pooled connection keeps it locked on Windows once the host is
/// disposed, and disposing the factory deletes it.
/// </remarks>
internal sealed class CryptoStuffApiFactory : WebApplicationFactory<Program>
{
    /// <summary>The API key the hosted API is configured with, and every client from <c>CreateClient</c> sends.</summary>
    public const string ApiKey = "integration-test-api-key";

    /// <summary>The request header the API reads its key from.</summary>
    public const string ApiKeyHeader = "X-Api-Key";

    private static readonly string DatabaseRoot = Path.Combine(Path.GetTempPath(), "cryptostuff-tests");

    private readonly bool _hostAlertEvaluation;
    private readonly IInterceptor? _interceptor;

    /// <param name="hostAlertEvaluation">
    /// Keeps the background alert evaluation registered, for tests about the
    /// host's own registrations; every other test leaves it out.
    /// </param>
    /// <param name="busyTimeoutSeconds">
    /// How long the host waits for another writer's lock before failing as
    /// busy; the driver's own default, thirty seconds, when null.
    /// </param>
    /// <param name="interceptor">
    /// Added to the host's database context, for tests that watch or steer
    /// the commands it sends.
    /// </param>
    public CryptoStuffApiFactory(bool hostAlertEvaluation = false, int? busyTimeoutSeconds = null, IInterceptor? interceptor = null)
    {
        _hostAlertEvaluation = hostAlertEvaluation;
        _interceptor = interceptor;

        // AddCryptoStuff reads CoinGecko:ApiKey synchronously in Program.cs,
        // before the host builds - i.e. before WebApplicationFactory's own
        // configuration-override hooks are guaranteed to have applied. A real
        // process environment variable, which AddEnvironmentVariables() picks
        // up unconditionally, sidesteps that timing question entirely, and
        // doubles as coverage that the documented CoinGecko__ApiKey deployment
        // mechanism (see README) actually works.
        Environment.SetEnvironmentVariable("CoinGecko__ApiKey", "integration-test-key");

        // The API's own key, supplied the same way and for the same reasons,
        // so it doubles as coverage of the documented Api__Key deployment
        // mechanism. Every client this factory creates sends it (see
        // ConfigureClient); a test about rejected callers uses
        // CreateClientWithoutApiKey instead.
        Environment.SetEnvironmentVariable("Api__Key", ApiKey);

        DatabaseFilePath = Path.Combine(DatabaseRoot, $"{Guid.NewGuid():N}.db");
        var connectionString = new SqliteConnectionStringBuilder { DataSource = DatabaseFilePath, Pooling = false };
        if (busyTimeoutSeconds is { } seconds)
        {
            connectionString.DefaultTimeout = seconds;
        }

        ConnectionString = connectionString.ToString();
    }

    /// <summary>The fake substituted for the real CoinGecko client. Configure its response before issuing a request.</summary>
    public FakeCoinGeckoClient CoinGeckoClient { get; } = new();

    /// <summary>This factory's database file; it exists once the host has started.</summary>
    public string DatabaseFilePath { get; }

    /// <summary>The connection string the hosted API uses for <see cref="DatabaseFilePath"/>.</summary>
    public string ConnectionString { get; }

    /// <summary>A new context over this factory's database, independent of the host's scopes.</summary>
    public CryptoStuffDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<CryptoStuffDbContext>().UseSqlite(ConnectionString).Options);

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        DeleteDatabaseFile();
    }

    /// <summary>
    /// Makes every client created from this factory, or from one derived with
    /// <c>WithWebHostBuilder</c>, an authenticated caller.
    /// </summary>
    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        client.DefaultRequestHeaders.Add(ApiKeyHeader, ApiKey);
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Replaced here rather than through a ConnectionStrings__CryptoStuff
        // environment variable: that is process-wide, so parallel tests would
        // race to set it and could share, or delete, each other's file.
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICoinGeckoClient>();
            services.AddSingleton<ICoinGeckoClient>(CoinGeckoClient);

            // AddDbContext accumulates option configurations, so the host's
            // UseSqlite would otherwise still run beside this one.
            services.RemoveAll<IDbContextOptionsConfiguration<CryptoStuffDbContext>>();
            services.RemoveAll<DbContextOptions<CryptoStuffDbContext>>();
            services.AddDbContext<CryptoStuffDbContext>(options =>
            {
                options.UseSqlite(ConnectionString);
                if (_interceptor is not null)
                {
                    options.AddInterceptors(_interceptor);
                }
            });

            if (!_hostAlertEvaluation)
            {
                var backgroundEvaluation = services.Single(descriptor =>
                    descriptor.ServiceType == typeof(IHostedService)
                    && descriptor.ImplementationType == typeof(AlertEvaluationBackgroundService));
                services.Remove(backgroundEvaluation);
            }
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            DeleteDatabaseFile();
        }
    }

    private void DeleteDatabaseFile()
    {
        if (File.Exists(DatabaseFilePath))
        {
            File.Delete(DatabaseFilePath);
        }
    }
}

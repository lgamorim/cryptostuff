using CryptoStuff.Api;
using CryptoStuff.Api.Alerts;
using CryptoStuff.Api.Portfolios;
using CryptoStuff.Api.Watchlists;
using CryptoStuff.Composition;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCryptoStuff(builder.Configuration);

// The host's first start-up step, before the server listens, so no request
// ever meets a missing or outdated schema; see StartupDatabaseMigration.
builder.Services.AddHostedService(services => new StartupDatabaseMigration(services.MigrateCryptoStuffDatabaseAsync));

// Only this host evaluates alerts continuously; the CLI exits after each
// command, so it evaluates only on `alert check`.
builder.Services.AddAlertEvaluationSchedule(builder.Configuration);
builder.Services.AddHostedService<AlertEvaluationBackgroundService>();
builder.Services.AddAlertCheckRateLimit(builder.Configuration);
builder.Services.AddProblemDetails();

// The same JSON shape the CLI prints: enumerations as their tokens, timestamps
// as UTC ending in `Z`.
builder.Services.ConfigureHttpJsonOptions(options => CryptoStuffJson.Configure(options.SerializerOptions));
// Readiness checks the shared database only. CoinGecko is deliberately not
// probed, so a probe never spends the API key's rate limit.
builder.Services.AddHealthChecks()
    .Add(new HealthCheckRegistration(
        "database",
        services => new DatabaseReadinessHealthCheck(services.IsCryptoStuffDatabaseReadyAsync),
        failureStatus: null,
        tags: null));
builder.Services.AddOpenApi();

var app = builder.Build();

// A reverse proxy serving the API under a path (`/api`), forwarding requests
// with that prefix still on, is met by setting PathBase to it: routes then
// match beneath it, and every Location header names it. Left empty, as by
// default, UsePathBase adds nothing.
app.UsePathBase(app.Configuration["PathBase"]);

// Gives framework-generated error responses (e.g. an unmatched route's 404) a
// ProblemDetails body via the AddProblemDetails() service above; without this,
// that registration has no effect, since TypedResults.Problem/ValidationProblem
// write their own bodies and never consult IProblemDetailsService.
app.UseStatusCodePages();
app.UsePersistenceBusyResponses();
app.UseRateLimiter();

app.MapCryptoStuffEndpoints();
app.MapWatchlistEndpoints();
app.MapPortfolioEndpoints();
app.MapAlertEndpoints();
// Liveness runs no check, so it answers even while the database is unusable;
// readiness runs every registered check.
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready");
app.MapOpenApi();

if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference();
}

// Taken before running: once RunAsync fails it has disposed the host, and
// reading app.Lifetime then throws inside the filter below, which fails it.
var stopping = app.Lifetime.ApplicationStopping;
try
{
    await app.RunAsync();
}

// Once a stop has been requested, a cancellation escaping RunAsync is that
// stop, not a crash. It is most often Ctrl+C while the start-up migration was
// still running: the host has logged that it failed to start and served
// nothing. Either way the process ends as it would after any requested stop.
catch (OperationCanceledException) when (stopping.IsCancellationRequested)
{
}

/// <summary>Exposes the entry point to <c>WebApplicationFactory&lt;Program&gt;</c> for integration testing.</summary>
public partial class Program;

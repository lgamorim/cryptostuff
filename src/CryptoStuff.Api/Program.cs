using CryptoStuff.Api;
using CryptoStuff.Api.Watchlists;
using CryptoStuff.Composition;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCryptoStuff(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();

var app = builder.Build();

// Gives framework-generated error responses (e.g. an unmatched route's 404) a
// ProblemDetails body via the AddProblemDetails() service above; without this,
// that registration has no effect, since TypedResults.Problem/ValidationProblem
// write their own bodies and never consult IProblemDetailsService.
app.UseStatusCodePages();

app.MapCryptoStuffEndpoints();
app.MapWatchlistEndpoints();
app.MapHealthChecks("/health");
app.MapOpenApi();

if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference();
}

// Before the host starts listening, so no request ever meets a missing or
// outdated schema. A failure here ends start-up with the exception, as a
// missing API key does above. No token to pass: the host only handles Ctrl+C
// once it runs, so until then Ctrl+C ends the process outright.
await app.Services.MigrateCryptoStuffDatabaseAsync(CancellationToken.None);

await app.RunAsync();

/// <summary>Exposes the entry point to <c>WebApplicationFactory&lt;Program&gt;</c> for integration testing.</summary>
public partial class Program;

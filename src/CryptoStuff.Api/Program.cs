using CryptoStuff.Api;
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
app.MapHealthChecks("/health");
app.MapOpenApi();

if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference();
}

app.Run();

/// <summary>Exposes the entry point to <c>WebApplicationFactory&lt;Program&gt;</c> for integration testing.</summary>
public partial class Program;

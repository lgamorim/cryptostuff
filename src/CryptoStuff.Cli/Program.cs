using CryptoStuff.Cli;
using CryptoStuff.Composition;
using CryptoStuff.Core;
using CryptoStuff.Core.Alerts;
using CryptoStuff.Core.Portfolios;
using CryptoStuff.Core.Watchlists;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables()
    .Build();

var services = new ServiceCollection();

try
{
    services.AddCryptoStuff(configuration);
}
catch (InvalidOperationException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

// ValidateScopes makes resolving a scoped service from the root provider throw,
// so persistence services can only be reached through the scope below;
// ValidateOnBuild reports a registration that can never resolve before any
// command runs.
await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
{
    ValidateScopes = true,
    ValidateOnBuild = true,
});

// Not disposed: the process exits immediately after RunAsync returns, and a
// CancelKeyPress arriving during that shutdown window must not race a Dispose()
// that would throw ObjectDisposedException from the handler.
var cancellationTokenSource = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellationTokenSource.Cancel();
};

// One scope per invocation: the command's DbContext lives exactly as long as
// the command, and is disposed before the process exits.
await using var scope = provider.CreateAsyncScope();
var cryptocurrencyService = scope.ServiceProvider.GetRequiredService<ICryptocurrencyService>();

// The runner migrates, then resolves a stored-feature service, only for a
// command that uses the database; the migration runs in a scope of its own.
var storedFeatures = new CliStoredFeatures(
    provider.MigrateCryptoStuffDatabaseAsync,
    () => scope.ServiceProvider.GetRequiredService<IWatchlistService>(),
    () => scope.ServiceProvider.GetRequiredService<IPortfolioService>(),
    () => scope.ServiceProvider.GetRequiredService<IAlertService>(),
    () => scope.ServiceProvider.GetRequiredService<IAlertEvaluator>());
var runner = new CliRunner(cryptocurrencyService, storedFeatures, Console.Out, Console.Error);
return await runner.RunAsync(args, cancellationTokenSource.Token);

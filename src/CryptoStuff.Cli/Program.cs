using CryptoStuff.Cli;
using CryptoStuff.Composition;
using CryptoStuff.Core;
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

// Only before a command that uses the database, so it sees an up-to-date
// schema; the CLI and the API share the file, and whichever starts first after
// an upgrade migrates it. CoinGecko-only commands skip this entirely, since
// EF Core's cold start costs about 640 ms per process.
var migrationExitCode = await CliDatabaseStartup.MigrateIfRequiredAsync(
    CliArgumentParser.Parse(args), provider.MigrateCryptoStuffDatabaseAsync, Console.Error, cancellationTokenSource.Token);
if (migrationExitCode != 0)
{
    return migrationExitCode;
}

// One scope per invocation: the command's DbContext lives exactly as long as
// the command, and is disposed before the process exits.
await using var scope = provider.CreateAsyncScope();
var cryptocurrencyService = scope.ServiceProvider.GetRequiredService<ICryptocurrencyService>();

// Resolved on demand: building the watchlist service constructs the DbContext,
// which a CoinGecko-only command must not pay for.
var runner = new CliRunner(
    cryptocurrencyService,
    Console.Out,
    Console.Error,
    () => scope.ServiceProvider.GetRequiredService<IWatchlistService>());
return await runner.RunAsync(args, cancellationTokenSource.Token);

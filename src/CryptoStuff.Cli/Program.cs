using CryptoStuff.Cli;
using CryptoStuff.Composition;
using CryptoStuff.Core;
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

await using var provider = services.BuildServiceProvider();
var cryptocurrencyService = provider.GetRequiredService<ICryptocurrencyService>();

// Not disposed: the process exits immediately after RunAsync returns, and a
// CancelKeyPress arriving during that shutdown window must not race a Dispose()
// that would throw ObjectDisposedException from the handler.
var cancellationTokenSource = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellationTokenSource.Cancel();
};

var runner = new CliRunner(cryptocurrencyService, Console.Out, Console.Error);
return await runner.RunAsync(args, cancellationTokenSource.Token);

using CryptoStuff.Cli.Alerts;
using CryptoStuff.Cli.Portfolios;
using CryptoStuff.Cli.Watchlists;
using CryptoStuff.Core;

namespace CryptoStuff.Cli;

/// <summary>
/// Parses, validates, and executes a single CLI invocation against
/// <see cref="ICryptocurrencyService"/> or a stored feature, writing results
/// and errors to the given writers instead of the console directly, so tests
/// can assert on strings rather than the console.
/// </summary>
public sealed class CliRunner
{
    private readonly ICryptocurrencyService _service;
    private readonly CliStoredFeatures _storedFeatures;
    private readonly CliResultWriter _writer;

    /// <summary>
    /// Creates a runner that calls <paramref name="service"/> for market data,
    /// reaches the database through <paramref name="storedFeatures"/>, and
    /// writes to the given writers.
    /// </summary>
    public CliRunner(ICryptocurrencyService service, CliStoredFeatures storedFeatures, TextWriter output, TextWriter error)
    {
        _service = service;
        _storedFeatures = storedFeatures;
        _writer = new CliResultWriter(output, error);
    }

    /// <summary>
    /// Parses and executes <paramref name="args"/>, returning the process
    /// exit code: <c>0</c> on success, <c>1</c> on a usage, validation, or
    /// service failure, a database that could not be opened or migrated, or
    /// one another writer kept locked, <c>2</c>
    /// when <c>alert check --exit-code</c> triggered an alert, and
    /// <c>130</c> if <paramref name="cancellationToken"/> is canceled mid-request.
    /// </summary>
    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        try
        {
            var parsed = CliArgumentParser.Parse(args);

            return parsed.Command switch
            {
                // Each option belongs to one family's subcommands, which that
                // family names; swallowing it silently anywhere else would hide
                // a mistyped invocation. Checked before anything runs, so such
                // an invocation never touches the database.
                _ when parsed.Fee is not null && !PortfolioCommands.TakesFee(parsed) => _writer.WriteUsage(),
                _ when parsed.ExitCodeOnTrigger && !AlertCommands.TakesExitCode(parsed) => _writer.WriteUsage(),
                "price" => await ExecutePriceAsync(parsed, cancellationToken),
                "token" => await ExecuteTokenAsync(parsed, cancellationToken),
                "history" => await ExecuteHistoryAsync(parsed, cancellationToken),
                "coin" => await ExecuteCoinAsync(parsed, cancellationToken),
                "developer" => await ExecuteDeveloperAsync(parsed, cancellationToken),
                WatchlistCommands.Name => await ExecuteStoredAsync(
                    () => new WatchlistCommands(_storedFeatures.Watchlists(), _writer).ExecuteAsync(parsed, cancellationToken),
                    cancellationToken),
                PortfolioCommands.Name => await ExecuteStoredAsync(
                    () => new PortfolioCommands(_storedFeatures.Portfolios(), _writer).ExecuteAsync(parsed, cancellationToken),
                    cancellationToken),
                AlertCommands.Name => await ExecuteStoredAsync(
                    () => new AlertCommands(_storedFeatures.Alerts(), _storedFeatures.AlertEvaluator(), _writer).ExecuteAsync(parsed, cancellationToken),
                    cancellationToken),
                _ => _writer.WriteUsage(),
            };
        }
        catch (OperationCanceledException)
        {
            return 130;
        }

        // Another writer (the API, its background evaluation, or another CLI
        // run) held the database's lock past the provider's wait. Nothing was
        // written, so running the command again can succeed.
        catch (PersistenceBusyException exception)
        {
            return _writer.WriteError($"Error: {exception.Message}");
        }
    }

    // Migrates before a command that uses the database, so it sees an
    // up-to-date schema; the CLI and the API share the file, and whichever
    // starts first after an upgrade migrates it. Only then is the family's
    // service resolved, since building it constructs the DbContext, and only
    // these commands pay for either: EF Core's cold start costs about 640 ms
    // per process, which the market-data commands skip.
    private async Task<int> ExecuteStoredAsync(Func<Task<int>> execute, CancellationToken cancellationToken)
    {
        var migrationExitCode = await CliDatabaseStartup.MigrateAsync(_storedFeatures.Migrate, _writer, cancellationToken);
        return migrationExitCode != 0 ? migrationExitCode : await execute();
    }

    private async Task<int> ExecutePriceAsync(CliArguments parsed, CancellationToken cancellationToken)
    {
        if (parsed.Arguments.Count != 2)
        {
            return _writer.WriteUsage();
        }

        if (!CliArgumentReader.TryParseIdentifierList(parsed.Arguments[0], "Coins", out var coinIds, out var error))
        {
            return _writer.WriteError(error);
        }

        if (!CliArgumentReader.TryParseIdentifierList(parsed.Arguments[1], "Currencies", out var vsCurrencies, out error))
        {
            return _writer.WriteError(error);
        }

        var query = new CoinPriceQuery { CoinIds = coinIds, VsCurrencies = vsCurrencies };
        var result = await _service.GetPricesAsync(query, cancellationToken);

        return _writer.WriteServiceResult(result, parsed.JsonOutput, CliOutputFormatter.Format);
    }

    private async Task<int> ExecuteTokenAsync(CliArguments parsed, CancellationToken cancellationToken)
    {
        if (parsed.Arguments.Count != 3)
        {
            return _writer.WriteUsage();
        }

        if (!CliArgumentReader.TryValidateScalar(parsed.Arguments[0], "Platform", out var error))
        {
            return _writer.WriteError(error);
        }

        if (!CliArgumentReader.TryParseIdentifierList(parsed.Arguments[1], "Addresses", out var addresses, out error))
        {
            return _writer.WriteError(error);
        }

        if (!CliArgumentReader.TryParseIdentifierList(parsed.Arguments[2], "Currencies", out var vsCurrencies, out error))
        {
            return _writer.WriteError(error);
        }

        var query = new TokenPriceQuery
        {
            Platform = parsed.Arguments[0],
            ContractAddresses = addresses,
            VsCurrencies = vsCurrencies,
        };

        var result = await _service.GetTokenPricesAsync(query, cancellationToken);

        return _writer.WriteServiceResult(result, parsed.JsonOutput, CliOutputFormatter.Format);
    }

    private async Task<int> ExecuteHistoryAsync(CliArguments parsed, CancellationToken cancellationToken)
    {
        if (parsed.Arguments.Count != 3)
        {
            return _writer.WriteUsage();
        }

        if (!CliArgumentReader.TryValidateScalar(parsed.Arguments[0], "Coin", out var error))
        {
            return _writer.WriteError(error);
        }

        if (!CliArgumentReader.TryValidateScalar(parsed.Arguments[1], "Currency", out error))
        {
            return _writer.WriteError(error);
        }

        if (!CliArgumentReader.TryValidateDays(parsed.Arguments[2], out var days, out error))
        {
            return _writer.WriteError(error);
        }

        var query = new CoinMarketChartQuery { CoinId = parsed.Arguments[0], VsCurrency = parsed.Arguments[1], Days = days };
        var result = await _service.GetMarketChartAsync(query, cancellationToken);

        return _writer.WriteServiceResult(result, parsed.JsonOutput, CliOutputFormatter.Format);
    }

    private async Task<int> ExecuteCoinAsync(CliArguments parsed, CancellationToken cancellationToken)
    {
        if (parsed.Arguments.Count != 1)
        {
            return _writer.WriteUsage();
        }

        if (!CliArgumentReader.TryValidateScalar(parsed.Arguments[0], "Id", out var error))
        {
            return _writer.WriteError(error);
        }

        var query = new CoinQuery { CoinId = parsed.Arguments[0] };
        var result = await _service.GetCoinAsync(query, cancellationToken);

        return _writer.WriteServiceResult(result, parsed.JsonOutput, CliOutputFormatter.Format);
    }

    private async Task<int> ExecuteDeveloperAsync(CliArguments parsed, CancellationToken cancellationToken)
    {
        if (parsed.Arguments.Count != 2)
        {
            return _writer.WriteUsage();
        }

        if (!CliArgumentReader.TryValidateScalar(parsed.Arguments[0], "Id", out var error))
        {
            return _writer.WriteError(error);
        }

        if (!CliArgumentReader.TryValidateDate(parsed.Arguments[1], "Date", out error))
        {
            return _writer.WriteError(error);
        }

        var query = new CoinDeveloperDataQuery { CoinId = parsed.Arguments[0], Date = parsed.Arguments[1] };
        var result = await _service.GetDeveloperDataAsync(query, cancellationToken);

        return _writer.WriteServiceResult(result, parsed.JsonOutput, CliOutputFormatter.Format);
    }
}

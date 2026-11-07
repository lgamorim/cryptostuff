using CryptoStuff.Cli.Alerts;
using CryptoStuff.Cli.Portfolios;
using CryptoStuff.Cli.Watchlists;
using CryptoStuff.Core;
using CryptoStuff.Core.Alerts;
using CryptoStuff.Core.Portfolios;
using CryptoStuff.Core.Watchlists;

namespace CryptoStuff.Cli;

/// <summary>
/// Parses, validates, and executes a single CLI invocation against
/// <see cref="ICryptocurrencyService"/>, writing results and errors to the
/// given writers instead of the console directly, so tests can assert on
/// strings rather than the console.
/// </summary>
public sealed class CliRunner
{
    private readonly ICryptocurrencyService _service;
    private readonly CliResultWriter _writer;
    private readonly Func<IWatchlistService>? _watchlists;
    private readonly Func<IPortfolioService>? _portfolios;
    private readonly Func<IAlertService>? _alerts;
    private readonly Func<IAlertEvaluator>? _alertEvaluator;

    /// <summary>
    /// Creates a runner that calls <paramref name="service"/> and writes to the given writers.
    /// <paramref name="watchlists"/> supplies the watchlist service on demand, so a
    /// command that never touches the database never resolves it; without it the
    /// <c>watchlist</c> command family is not available. <paramref name="portfolios"/>
    /// does the same for the <c>portfolio</c> family, and <paramref name="alerts"/> and
    /// <paramref name="alertEvaluator"/> for the <c>alert</c> family, which needs both.
    /// </summary>
    public CliRunner(
        ICryptocurrencyService service,
        TextWriter output,
        TextWriter error,
        Func<IWatchlistService>? watchlists = null,
        Func<IPortfolioService>? portfolios = null,
        Func<IAlertService>? alerts = null,
        Func<IAlertEvaluator>? alertEvaluator = null)
    {
        _service = service;
        _writer = new CliResultWriter(output, error);
        _watchlists = watchlists;
        _portfolios = portfolios;
        _alerts = alerts;
        _alertEvaluator = alertEvaluator;
    }

    /// <summary>
    /// Parses and executes <paramref name="args"/>, returning the process
    /// exit code: <c>0</c> on success, <c>1</c> on a usage, validation, or
    /// service failure, and <c>130</c> if <paramref name="cancellationToken"/>
    /// is canceled mid-request.
    /// </summary>
    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        try
        {
            var parsed = CliArgumentParser.Parse(args);

            return parsed.Command switch
            {
                // Only portfolio transactions take --fee; swallowing it silently
                // elsewhere would hide a mistyped invocation.
                _ when parsed.Fee is not null && parsed.Command != PortfolioCommands.Name => _writer.WriteUsage(),
                "price" => await ExecutePriceAsync(parsed, cancellationToken),
                "token" => await ExecuteTokenAsync(parsed, cancellationToken),
                "history" => await ExecuteHistoryAsync(parsed, cancellationToken),
                "coin" => await ExecuteCoinAsync(parsed, cancellationToken),
                "developer" => await ExecuteDeveloperAsync(parsed, cancellationToken),
                WatchlistCommands.Name when _watchlists is not null =>
                    await new WatchlistCommands(_watchlists(), _writer).ExecuteAsync(parsed, cancellationToken),
                PortfolioCommands.Name when _portfolios is not null =>
                    await new PortfolioCommands(_portfolios(), _writer).ExecuteAsync(parsed, cancellationToken),
                AlertCommands.Name when _alerts is not null && _alertEvaluator is not null =>
                    await new AlertCommands(_alerts(), _alertEvaluator(), _writer).ExecuteAsync(parsed, cancellationToken),
                _ => _writer.WriteUsage(),
            };
        }
        catch (OperationCanceledException)
        {
            return 130;
        }
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

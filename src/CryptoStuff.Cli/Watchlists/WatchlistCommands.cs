using CryptoStuff.Core;
using CryptoStuff.Core.Watchlists;

namespace CryptoStuff.Cli.Watchlists;

/// <summary>Executes the <c>watchlist</c> command family against <see cref="IWatchlistService"/>.</summary>
public sealed class WatchlistCommands
{
    /// <summary>The command noun this family handles.</summary>
    public const string Name = "watchlist";

    private readonly IWatchlistService _service;
    private readonly CliResultWriter _writer;

    /// <summary>Creates the family over <paramref name="service"/>, reporting through <paramref name="writer"/>.</summary>
    public WatchlistCommands(IWatchlistService service, CliResultWriter writer)
    {
        _service = service;
        _writer = writer;
    }

    /// <summary>
    /// Executes the subcommand named by the first argument of <paramref name="parsed"/>;
    /// a missing or unknown subcommand, or the wrong number of arguments, prints the usage.
    /// </summary>
    public Task<int> ExecuteAsync(CliArguments parsed, CancellationToken cancellationToken) =>
        parsed.Arguments switch
        {
            ["list"] => ListAsync(parsed, cancellationToken),
            ["create", var name] => CreateAsync(name, parsed, cancellationToken),
            ["show", var rawId] => ShowAsync(rawId, parsed, cancellationToken),
            ["rename", var rawId, var name] => RenameAsync(rawId, name, parsed, cancellationToken),
            ["delete", var rawId] => DeleteAsync(rawId, parsed, cancellationToken),
            ["add", var rawId, var coin] => ChangeCoinAsync(_service.AddCoinAsync, rawId, coin, parsed, cancellationToken),
            ["remove", var rawId, var coin] => ChangeCoinAsync(_service.RemoveCoinAsync, rawId, coin, parsed, cancellationToken),
            ["prices", var rawId, var currency] => PricesAsync(rawId, currency, parsed, cancellationToken),
            _ => Task.FromResult(_writer.WriteUsage()),
        };

    private async Task<int> ListAsync(CliArguments parsed, CancellationToken cancellationToken)
    {
        var result = await _service.ListAsync(cancellationToken);
        return _writer.WriteServiceResult(result, parsed.JsonOutput, WatchlistOutputFormatter.Format);
    }

    private async Task<int> CreateAsync(string name, CliArguments parsed, CancellationToken cancellationToken)
    {
        if (!CliArgumentReader.TryValidateName(name, "Name", out var error))
        {
            return _writer.WriteError(error);
        }

        var result = await _service.CreateAsync(new CreateWatchlistCommand { Name = name }, cancellationToken);
        return _writer.WriteServiceResult(result, parsed.JsonOutput, WatchlistOutputFormatter.Format);
    }

    private async Task<int> ShowAsync(string rawId, CliArguments parsed, CancellationToken cancellationToken)
    {
        if (!CliArgumentReader.TryParseId(rawId, "Id", out var id, out var error))
        {
            return _writer.WriteError(error);
        }

        var result = await _service.GetAsync(id, cancellationToken);
        return _writer.WriteServiceResult(result, parsed.JsonOutput, WatchlistOutputFormatter.Format);
    }

    private async Task<int> RenameAsync(string rawId, string name, CliArguments parsed, CancellationToken cancellationToken)
    {
        if (!CliArgumentReader.TryParseId(rawId, "Id", out var id, out var error))
        {
            return _writer.WriteError(error);
        }

        if (!CliArgumentReader.TryValidateName(name, "Name", out error))
        {
            return _writer.WriteError(error);
        }

        var command = new RenameWatchlistCommand { WatchlistId = id, Name = name };
        var result = await _service.RenameAsync(command, cancellationToken);
        return _writer.WriteServiceResult(result, parsed.JsonOutput, WatchlistOutputFormatter.Format);
    }

    private async Task<int> DeleteAsync(string rawId, CliArguments parsed, CancellationToken cancellationToken)
    {
        if (!CliArgumentReader.TryParseId(rawId, "Id", out var id, out var error))
        {
            return _writer.WriteError(error);
        }

        var result = await _service.DeleteAsync(id, cancellationToken);
        return _writer.WriteServiceResult(result, WatchlistOutputFormatter.FormatDeleted(id, parsed.JsonOutput));
    }

    private async Task<int> ChangeCoinAsync(
        Func<WatchlistCoinCommand, CancellationToken, Task<ServiceResult<WatchlistView>>> change,
        string rawId,
        string coin,
        CliArguments parsed,
        CancellationToken cancellationToken)
    {
        if (!CliArgumentReader.TryParseId(rawId, "Id", out var id, out var error))
        {
            return _writer.WriteError(error);
        }

        if (!CliArgumentReader.TryValidateScalar(coin, "Coin", out error))
        {
            return _writer.WriteError(error);
        }

        var result = await change(new WatchlistCoinCommand { WatchlistId = id, CoinId = coin }, cancellationToken);
        return _writer.WriteServiceResult(result, parsed.JsonOutput, WatchlistOutputFormatter.Format);
    }

    private async Task<int> PricesAsync(string rawId, string currency, CliArguments parsed, CancellationToken cancellationToken)
    {
        if (!CliArgumentReader.TryParseId(rawId, "Id", out var id, out var error))
        {
            return _writer.WriteError(error);
        }

        if (!CliArgumentReader.TryValidateScalar(currency, "Currency", out error))
        {
            return _writer.WriteError(error);
        }

        var query = new WatchlistPriceQuery { WatchlistId = id, VsCurrency = currency };
        var result = await _service.GetPricesAsync(query, cancellationToken);
        return _writer.WriteServiceResult(result, parsed.JsonOutput, WatchlistOutputFormatter.FormatPrices);
    }
}

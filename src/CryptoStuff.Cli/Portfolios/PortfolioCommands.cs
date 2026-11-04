using System.Diagnostics.CodeAnalysis;
using CryptoStuff.Core.Portfolios;

namespace CryptoStuff.Cli.Portfolios;

/// <summary>Executes the <c>portfolio</c> command family against <see cref="IPortfolioService"/>.</summary>
public sealed class PortfolioCommands
{
    /// <summary>The command noun this family handles.</summary>
    public const string Name = "portfolio";

    private readonly IPortfolioService _service;
    private readonly CliResultWriter _writer;

    /// <summary>Creates the family over <paramref name="service"/>, reporting through <paramref name="writer"/>.</summary>
    public PortfolioCommands(IPortfolioService service, CliResultWriter writer)
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
            ["tx", "add", var rawId, var kind, var coin, var quantity, var price, var when] =>
                AddTransactionAsync(rawId, new RawTransaction(kind, coin, quantity, price, when), parsed, cancellationToken),
            ["tx", "edit", var rawId, var rawTransactionId, var kind, var coin, var quantity, var price, var when] =>
                EditTransactionAsync(rawId, rawTransactionId, new RawTransaction(kind, coin, quantity, price, when), parsed, cancellationToken),

            // Only the two transaction writes above take --fee.
            _ when parsed.Fee is not null => Task.FromResult(_writer.WriteUsage()),

            ["list"] => ListAsync(parsed, cancellationToken),
            ["create", var name, var currency, var rawMethod] => CreateAsync(name, currency, rawMethod, parsed, cancellationToken),
            ["show", var rawId] => ShowAsync(rawId, parsed, cancellationToken),
            ["update", var rawId, var name, var rawMethod] => UpdateAsync(rawId, name, rawMethod, parsed, cancellationToken),
            ["delete", var rawId] => DeleteAsync(rawId, parsed, cancellationToken),
            ["value", var rawId] => ValueAsync(rawId, parsed, cancellationToken),
            ["tx", "delete", var rawId, var rawTransactionId] => RemoveTransactionAsync(rawId, rawTransactionId, parsed, cancellationToken),
            _ => Task.FromResult(_writer.WriteUsage()),
        };

    private async Task<int> ListAsync(CliArguments parsed, CancellationToken cancellationToken)
    {
        var result = await _service.ListAsync(cancellationToken);
        return _writer.WriteServiceResult(result, parsed.JsonOutput, PortfolioOutputFormatter.Format);
    }

    private async Task<int> ShowAsync(string rawId, CliArguments parsed, CancellationToken cancellationToken)
    {
        if (!CliArgumentReader.TryParseId(rawId, "Id", out var id, out var error))
        {
            return _writer.WriteError(error);
        }

        var result = await _service.GetAsync(id, cancellationToken);
        return _writer.WriteServiceResult(result, parsed.JsonOutput, PortfolioOutputFormatter.Format);
    }

    private async Task<int> CreateAsync(string name, string currency, string rawMethod, CliArguments parsed, CancellationToken cancellationToken)
    {
        if (!CliArgumentReader.TryValidateName(name, "Name", out var error))
        {
            return _writer.WriteError(error);
        }

        if (!CliArgumentReader.TryValidateScalar(currency, "Currency", out error))
        {
            return _writer.WriteError(error);
        }

        if (!CliArgumentReader.TryParseCostBasisMethod(rawMethod, "Method", out var method, out error))
        {
            return _writer.WriteError(error);
        }

        var command = new CreatePortfolioCommand { Name = name, VsCurrency = currency, CostBasisMethod = method };
        var result = await _service.CreateAsync(command, cancellationToken);
        return _writer.WriteServiceResult(result, parsed.JsonOutput, PortfolioOutputFormatter.Format);
    }

    private async Task<int> UpdateAsync(string rawId, string name, string rawMethod, CliArguments parsed, CancellationToken cancellationToken)
    {
        if (!CliArgumentReader.TryParseId(rawId, "Id", out var id, out var error))
        {
            return _writer.WriteError(error);
        }

        if (!CliArgumentReader.TryValidateName(name, "Name", out error))
        {
            return _writer.WriteError(error);
        }

        if (!CliArgumentReader.TryParseCostBasisMethod(rawMethod, "Method", out var method, out error))
        {
            return _writer.WriteError(error);
        }

        var command = new UpdatePortfolioCommand { PortfolioId = id, Name = name, CostBasisMethod = method };
        var result = await _service.UpdateAsync(command, cancellationToken);
        return _writer.WriteServiceResult(result, parsed.JsonOutput, PortfolioOutputFormatter.Format);
    }

    private async Task<int> DeleteAsync(string rawId, CliArguments parsed, CancellationToken cancellationToken)
    {
        if (!CliArgumentReader.TryParseId(rawId, "Id", out var id, out var error))
        {
            return _writer.WriteError(error);
        }

        var result = await _service.DeleteAsync(id, cancellationToken);
        return _writer.WriteServiceResult(result, PortfolioOutputFormatter.FormatDeleted(id, parsed.JsonOutput));
    }

    private async Task<int> ValueAsync(string rawId, CliArguments parsed, CancellationToken cancellationToken)
    {
        if (!CliArgumentReader.TryParseId(rawId, "Id", out var id, out var error))
        {
            return _writer.WriteError(error);
        }

        var result = await _service.GetValuationAsync(id, cancellationToken);
        return _writer.WriteServiceResult(result, parsed.JsonOutput, PortfolioOutputFormatter.Format);
    }

    private async Task<int> AddTransactionAsync(string rawId, RawTransaction raw, CliArguments parsed, CancellationToken cancellationToken)
    {
        if (!CliArgumentReader.TryParseId(rawId, "Id", out var id, out var error))
        {
            return _writer.WriteError(error);
        }

        if (!TryReadTransaction(raw, parsed.Fee, out var details, out error))
        {
            return _writer.WriteError(error);
        }

        var result = await _service.AddTransactionAsync(new AddTransactionCommand { PortfolioId = id, Transaction = details }, cancellationToken);
        return _writer.WriteServiceResult(result, parsed.JsonOutput, PortfolioOutputFormatter.Format);
    }

    private async Task<int> EditTransactionAsync(string rawId, string rawTransactionId, RawTransaction raw, CliArguments parsed, CancellationToken cancellationToken)
    {
        if (!CliArgumentReader.TryParseId(rawId, "Id", out var id, out var error))
        {
            return _writer.WriteError(error);
        }

        if (!CliArgumentReader.TryParseId(rawTransactionId, "Transaction id", out var transactionId, out error))
        {
            return _writer.WriteError(error);
        }

        if (!TryReadTransaction(raw, parsed.Fee, out var details, out error))
        {
            return _writer.WriteError(error);
        }

        var command = new ReplaceTransactionCommand { PortfolioId = id, TransactionId = transactionId, Transaction = details };
        var result = await _service.ReplaceTransactionAsync(command, cancellationToken);
        return _writer.WriteServiceResult(result, parsed.JsonOutput, PortfolioOutputFormatter.Format);
    }

    private async Task<int> RemoveTransactionAsync(string rawId, string rawTransactionId, CliArguments parsed, CancellationToken cancellationToken)
    {
        if (!CliArgumentReader.TryParseId(rawId, "Id", out var id, out var error))
        {
            return _writer.WriteError(error);
        }

        if (!CliArgumentReader.TryParseId(rawTransactionId, "Transaction id", out var transactionId, out error))
        {
            return _writer.WriteError(error);
        }

        var command = new RemoveTransactionCommand { PortfolioId = id, TransactionId = transactionId };
        var result = await _service.RemoveTransactionAsync(command, cancellationToken);
        return _writer.WriteServiceResult(result, parsed.JsonOutput, PortfolioOutputFormatter.Format);
    }

    // The fee defaults to zero when --fee is absent.
    private static bool TryReadTransaction(RawTransaction raw, string? rawFee, [NotNullWhen(true)] out TransactionDetails? details, out string error)
    {
        details = null;

        if (!CliArgumentReader.TryParseTransactionKind(raw.Kind, "Kind", out var kind, out error)
            || !CliArgumentReader.TryValidateScalar(raw.Coin, "Coin", out error)
            || !CliArgumentReader.TryParsePositiveDecimal(raw.Quantity, "Quantity", out var quantity, out error)
            || !CliArgumentReader.TryParseNonNegativeDecimal(raw.Price, "Price", out var price, out error)
            || !CliArgumentReader.TryParseTimestamp(raw.When, "When", out var when, out error))
        {
            return false;
        }

        var fee = 0m;
        if (rawFee is not null && !CliArgumentReader.TryParseNonNegativeDecimal(rawFee, "Fee", out fee, out error))
        {
            return false;
        }

        details = new TransactionDetails
        {
            Kind = kind,
            CoinId = raw.Coin,
            Quantity = quantity,
            UnitPrice = price,
            Fee = fee,
            OccurredAt = when,
        };
        return true;
    }

    private sealed record RawTransaction(string Kind, string Coin, string Quantity, string Price, string When);
}

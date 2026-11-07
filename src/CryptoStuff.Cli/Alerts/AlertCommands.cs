using CryptoStuff.Core.Alerts;

namespace CryptoStuff.Cli.Alerts;

/// <summary>Executes the <c>alert</c> command family against <see cref="IAlertService"/> and <see cref="IAlertEvaluator"/>.</summary>
public sealed class AlertCommands
{
    /// <summary>The command noun this family handles.</summary>
    public const string Name = "alert";

    private readonly IAlertService _service;
    private readonly IAlertEvaluator _evaluator;
    private readonly CliResultWriter _writer;

    /// <summary>Creates the family over <paramref name="service"/> and <paramref name="evaluator"/>, reporting through <paramref name="writer"/>.</summary>
    public AlertCommands(IAlertService service, IAlertEvaluator evaluator, CliResultWriter writer)
    {
        _service = service;
        _evaluator = evaluator;
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
            ["create", var coin, var currency, var rawDirection, var rawThreshold] =>
                CreateAsync(coin, currency, rawDirection, rawThreshold, parsed, cancellationToken),
            ["show", var rawId] => ShowAsync(rawId, parsed, cancellationToken),
            ["update", var rawId, var rawDirection, var rawThreshold] =>
                UpdateAsync(rawId, rawDirection, rawThreshold, parsed, cancellationToken),
            ["delete", var rawId] => DeleteAsync(rawId, parsed, cancellationToken),
            ["rearm", var rawId] => RearmAsync(rawId, parsed, cancellationToken),
            ["check"] => CheckAsync(parsed, cancellationToken),
            _ => Task.FromResult(_writer.WriteUsage()),
        };

    private async Task<int> ListAsync(CliArguments parsed, CancellationToken cancellationToken)
    {
        var result = await _service.ListAsync(cancellationToken);
        return _writer.WriteServiceResult(result, parsed.JsonOutput, AlertOutputFormatter.Format);
    }

    private async Task<int> ShowAsync(string rawId, CliArguments parsed, CancellationToken cancellationToken)
    {
        if (!CliArgumentReader.TryParseId(rawId, "Id", out var id, out var error))
        {
            return _writer.WriteError(error);
        }

        var result = await _service.GetAsync(id, cancellationToken);
        return _writer.WriteServiceResult(result, parsed.JsonOutput, AlertOutputFormatter.Format);
    }

    private async Task<int> CreateAsync(string coin, string currency, string rawDirection, string rawThreshold, CliArguments parsed, CancellationToken cancellationToken)
    {
        if (!CliArgumentReader.TryValidateScalar(coin, "Coin", out var error))
        {
            return _writer.WriteError(error);
        }

        if (!CliArgumentReader.TryValidateScalar(currency, "Currency", out error))
        {
            return _writer.WriteError(error);
        }

        if (!TryReadTrigger(rawDirection, rawThreshold, out var direction, out var threshold, out error))
        {
            return _writer.WriteError(error);
        }

        var command = new CreateAlertCommand { CoinId = coin, VsCurrency = currency, Direction = direction, Threshold = threshold };
        var result = await _service.CreateAsync(command, cancellationToken);
        return _writer.WriteServiceResult(result, parsed.JsonOutput, AlertOutputFormatter.Format);
    }

    private async Task<int> UpdateAsync(string rawId, string rawDirection, string rawThreshold, CliArguments parsed, CancellationToken cancellationToken)
    {
        if (!CliArgumentReader.TryParseId(rawId, "Id", out var id, out var error))
        {
            return _writer.WriteError(error);
        }

        if (!TryReadTrigger(rawDirection, rawThreshold, out var direction, out var threshold, out error))
        {
            return _writer.WriteError(error);
        }

        // The CLI never passes an expected version: one user, no ETags.
        var command = new UpdateAlertCommand { AlertId = id, Direction = direction, Threshold = threshold };
        var result = await _service.UpdateAsync(command, cancellationToken);
        return _writer.WriteServiceResult(result, parsed.JsonOutput, AlertOutputFormatter.Format);
    }

    private async Task<int> DeleteAsync(string rawId, CliArguments parsed, CancellationToken cancellationToken)
    {
        if (!CliArgumentReader.TryParseId(rawId, "Id", out var id, out var error))
        {
            return _writer.WriteError(error);
        }

        var result = await _service.DeleteAsync(id, expectedVersion: null, cancellationToken);
        return _writer.WriteServiceResult(result, AlertOutputFormatter.FormatDeleted(id, parsed.JsonOutput));
    }

    private async Task<int> RearmAsync(string rawId, CliArguments parsed, CancellationToken cancellationToken)
    {
        if (!CliArgumentReader.TryParseId(rawId, "Id", out var id, out var error))
        {
            return _writer.WriteError(error);
        }

        var result = await _service.RearmAsync(id, expectedVersion: null, cancellationToken);
        return _writer.WriteServiceResult(result, parsed.JsonOutput, AlertOutputFormatter.Format);
    }

    // Triggering is not a failure, so an evaluation that triggered nothing still exits 0;
    // only a failed evaluation, such as an upstream error, exits 1.
    private async Task<int> CheckAsync(CliArguments parsed, CancellationToken cancellationToken)
    {
        var result = await _evaluator.EvaluateAsync(cancellationToken);
        return _writer.WriteServiceResult(result, parsed.JsonOutput, AlertOutputFormatter.Format);
    }

    // The direction and threshold a create and an update both take.
    private static bool TryReadTrigger(string rawDirection, string rawThreshold, out AlertDirection direction, out decimal threshold, out string error)
    {
        threshold = 0m;
        return CliArgumentReader.TryParseAlertDirection(rawDirection, "Direction", out direction, out error)
            && CliArgumentReader.TryParsePositiveDecimal(rawThreshold, "Threshold", out threshold, out error);
    }
}

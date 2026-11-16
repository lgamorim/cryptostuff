using CryptoStuff.Core.Alerts;
using CryptoStuff.Core.Portfolios;
using CryptoStuff.Core.Watchlists;

namespace CryptoStuff.Cli;

/// <summary>
/// What the command families that store data need from the database: the
/// start-up <paramref name="Migrate"/>, and each family's service, resolved on
/// demand. <see cref="CliRunner"/> migrates only before a command that uses
/// the database and resolves a service only after migrating, so a market-data
/// command neither migrates nor builds the DbContext.
/// </summary>
/// <param name="Migrate">Brings the database's schema up to date.</param>
/// <param name="Watchlists">Resolves the <c>watchlist</c> family's service.</param>
/// <param name="Portfolios">Resolves the <c>portfolio</c> family's service.</param>
/// <param name="Alerts">Resolves the <c>alert</c> family's service.</param>
/// <param name="AlertEvaluator">Resolves the evaluator <c>alert check</c> runs.</param>
public sealed record CliStoredFeatures(
    Func<CancellationToken, Task> Migrate,
    Func<IWatchlistService> Watchlists,
    Func<IPortfolioService> Portfolios,
    Func<IAlertService> Alerts,
    Func<IAlertEvaluator> AlertEvaluator);

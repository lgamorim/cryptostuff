using CryptoStuff.Core.Alerts;
using CryptoStuff.Core.Portfolios;
using CryptoStuff.Core.Watchlists;

namespace CryptoStuff.Cli.UnitTests.TestSupport;

// Builds the stored features a runner needs, filling each part a test leaves
// out with a migration that succeeds or a fresh spy service, so a test names
// only the parts it observes.
internal static class TestStoredFeatures
{
    public static CliStoredFeatures With(
        Func<IWatchlistService>? watchlists = null,
        Func<IPortfolioService>? portfolios = null,
        Func<IAlertService>? alerts = null,
        Func<IAlertEvaluator>? alertEvaluator = null,
        Func<CancellationToken, Task>? migrate = null) =>
        new(
            migrate ?? (_ => Task.CompletedTask),
            watchlists ?? (() => new SpyWatchlistService()),
            portfolios ?? (() => new SpyPortfolioService()),
            alerts ?? (() => new SpyAlertService()),
            alertEvaluator ?? (() => new SpyAlertEvaluator()));
}

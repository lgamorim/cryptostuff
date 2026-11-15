using System.Globalization;

namespace CryptoStuff.Core.Portfolios;

/// <summary>
/// The default <see cref="IPortfolioService"/>, storing portfolios through an
/// <see cref="IPortfolioRepository"/>, checking coins through an
/// <see cref="ICryptocurrencyService"/>, and reading the current time from a
/// <see cref="TimeProvider"/>.
/// </summary>
public sealed class PortfolioService(
    IPortfolioRepository repository,
    ICryptocurrencyService cryptocurrencyService,
    TimeProvider timeProvider) : IPortfolioService
{
    /// <inheritdoc />
    public async Task<ServiceResult<IReadOnlyList<PortfolioView>>> ListAsync(CancellationToken cancellationToken)
    {
        var portfolios = await repository.ListAsync(cancellationToken);
        return ServiceResult<IReadOnlyList<PortfolioView>>.Success(portfolios.Select(PortfolioMapper.ToView).ToArray());
    }

    /// <inheritdoc />
    public async Task<ServiceResult<PortfolioView>> GetAsync(int id, CancellationToken cancellationToken)
    {
        var portfolio = await repository.FindAsync(id, cancellationToken);
        return portfolio is null ? NotFound(id) : Success(portfolio);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<PortfolioView>> CreateAsync(CreatePortfolioCommand command, CancellationToken cancellationToken)
    {
        var portfolio = Portfolio.Create(command.Name, command.VsCurrency, command.CostBasisMethod);
        if (await repository.NameExistsAsync(portfolio.Name, excludingId: null, cancellationToken))
        {
            return NameTaken(portfolio.Name);
        }

        repository.Add(portfolio);
        return await SaveAsync(portfolio, cancellationToken) == SaveOutcome.Saved ? Success(portfolio) : NameTaken(portfolio.Name);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<PortfolioView>> UpdateAsync(UpdatePortfolioCommand command, CancellationToken cancellationToken)
    {
        var portfolio = await repository.FindAsync(command.PortfolioId, cancellationToken);
        if (portfolio is null)
        {
            return NotFound(command.PortfolioId);
        }

        if (StaleVersionDetail(portfolio, command.ExpectedVersion) is { } staleVersion)
        {
            return ServiceResult<PortfolioView>.Failure(ServiceErrorCode.PreconditionFailed, staleVersion);
        }

        var name = NameValidator.Normalize(command.Name);
        if (await repository.NameExistsAsync(name, command.PortfolioId, cancellationToken))
        {
            return NameTaken(name);
        }

        portfolio.Update(name, command.CostBasisMethod);

        // Any race can lose this save: another portfolio taking the name since
        // the check above, another write moving the version on, or another
        // writer deleting the portfolio.
        return await SaveAsync(portfolio, cancellationToken) switch
        {
            SaveOutcome.Saved => Success(portfolio),
            SaveOutcome.Deleted => NotFound(portfolio.Id),
            _ => ServiceResult<PortfolioView>.Failure(
                ServiceErrorCode.Conflict,
                $"Portfolio {portfolio.Id} could not be saved: another portfolio may have taken the name '{name}', or another operation changed it; reload it and try again."),
        };
    }

    /// <inheritdoc />
    public async Task<ServiceResult> DeleteAsync(int id, int? expectedVersion, CancellationToken cancellationToken)
    {
        var portfolio = await repository.FindAsync(id, cancellationToken);
        if (portfolio is null)
        {
            return ServiceResult.Failure(ServiceErrorCode.NotFound, NotFoundDetail(id));
        }

        if (StaleVersionDetail(portfolio, expectedVersion) is { } staleVersion)
        {
            return ServiceResult.Failure(ServiceErrorCode.PreconditionFailed, staleVersion);
        }

        repository.Remove(portfolio);
        return await SaveAsync(portfolio, cancellationToken) switch
        {
            SaveOutcome.Saved => ServiceResult.Success(),
            SaveOutcome.Deleted => ServiceResult.Failure(ServiceErrorCode.NotFound, NotFoundDetail(id)),
            _ => ServiceResult.Failure(ServiceErrorCode.Conflict, ChangedConcurrentlyDetail(id)),
        };
    }

    /// <inheritdoc />
    public async Task<ServiceResult<PortfolioView>> AddTransactionAsync(AddTransactionCommand command, CancellationToken cancellationToken)
    {
        var portfolio = await repository.FindAsync(command.PortfolioId, cancellationToken);
        if (portfolio is null)
        {
            return NotFound(command.PortfolioId);
        }

        if (StaleVersionDetail(portfolio, command.ExpectedVersion) is { } staleVersion)
        {
            return ServiceResult<PortfolioView>.Failure(ServiceErrorCode.PreconditionFailed, staleVersion);
        }

        return await CheckTransactionAsync(command.Transaction, portfolio, cancellationToken) is { } rejected
            ? rejected
            : await SaveLedgerChangeAsync(portfolio, portfolio.AddTransaction(command.Transaction), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<PortfolioView>> ReplaceTransactionAsync(ReplaceTransactionCommand command, CancellationToken cancellationToken)
    {
        var portfolio = await repository.FindAsync(command.PortfolioId, cancellationToken);
        if (portfolio is null)
        {
            return NotFound(command.PortfolioId);
        }

        if (StaleVersionDetail(portfolio, command.ExpectedVersion) is { } staleVersion)
        {
            return ServiceResult<PortfolioView>.Failure(ServiceErrorCode.PreconditionFailed, staleVersion);
        }

        var transaction = portfolio.FindTransaction(command.TransactionId);
        if (transaction is null)
        {
            return TransactionNotFound(command.PortfolioId, command.TransactionId);
        }

        return await CheckTransactionAsync(command.Transaction, portfolio, cancellationToken) is { } rejected
            ? rejected
            : await SaveLedgerChangeAsync(portfolio, portfolio.ReplaceTransaction(transaction, command.Transaction), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<PortfolioView>> RemoveTransactionAsync(RemoveTransactionCommand command, CancellationToken cancellationToken)
    {
        var portfolio = await repository.FindAsync(command.PortfolioId, cancellationToken);
        if (portfolio is null)
        {
            return NotFound(command.PortfolioId);
        }

        if (StaleVersionDetail(portfolio, command.ExpectedVersion) is { } staleVersion)
        {
            return ServiceResult<PortfolioView>.Failure(ServiceErrorCode.PreconditionFailed, staleVersion);
        }

        var transaction = portfolio.FindTransaction(command.TransactionId);
        if (transaction is null)
        {
            return TransactionNotFound(command.PortfolioId, command.TransactionId);
        }

        return await SaveLedgerChangeAsync(portfolio, portfolio.RemoveTransaction(transaction), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<PortfolioValuationView>> GetValuationAsync(int id, CancellationToken cancellationToken)
    {
        var portfolio = await repository.FindAsync(id, cancellationToken);
        if (portfolio is null)
        {
            return ServiceResult<PortfolioValuationView>.Failure(ServiceErrorCode.NotFound, NotFoundDetail(id));
        }

        var method = CostBasisMethods.For(portfolio.CostBasisMethod);
        List<(string CoinId, CoinPosition Position)> positions;
        try
        {
            positions = portfolio.Transactions
                .GroupBy(t => t.CoinId)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => (CoinId: g.Key, Position: method.CalculatePosition(g)))
                .ToList();
        }
        catch (OverflowException)
        {
            // Only a ledger stored before the portfolio refused amounts too
            // large to compute can get here.
            return TooLargeToValue(portfolio.Id);
        }

        // Only the units still held need a price: a closed position is worth
        // nothing, and with none open there is nothing to ask CoinGecko.
        IReadOnlyList<CoinPrice> livePrices = [];
        var openCoinIds = positions.Where(p => p.Position.Quantity > 0m).Select(p => p.CoinId).ToList();
        if (openCoinIds.Count > 0)
        {
            var priceQuery = new CoinPriceQuery { CoinIds = openCoinIds, VsCurrencies = [portfolio.VsCurrency] };
            var prices = await cryptocurrencyService.GetKnownCoinPricesAsync(priceQuery, cancellationToken);
            if (prices.ErrorCode is { } errorCode)
            {
                return ServiceResult<PortfolioValuationView>.Failure(errorCode, prices.ErrorDetail);
            }

            // `!` is safe: a successful result always carries its value.
            livePrices = prices.Value!.Coins;
        }

        try
        {
            return ServiceResult<PortfolioValuationView>.Success(Value(portfolio, positions, livePrices));
        }
        catch (OverflowException)
        {
            // Every coin's own figures fit, but a live price times the
            // quantity held, or a total across coins, may not.
            return TooLargeToValue(portfolio.Id);
        }
    }

    private static ServiceResult<PortfolioValuationView> TooLargeToValue(int id) =>
        ServiceResult<PortfolioValuationView>.Failure(
            ServiceErrorCode.RuleViolated,
            string.Create(
                CultureInfo.InvariantCulture,
                $"Portfolio {id} cannot be valued: an amount in its valuation would be too large to compute, beyond {decimal.MaxValue} or needing more significant digits than a decimal holds."));

    /// <summary>
    /// Joins each coin's position with its live price. A coin still held that
    /// CoinGecko gave no price for, in the portfolio's vs_currency, because it
    /// no longer knows the coin or does not quote it in that currency, is left
    /// unpriced rather than failing the valuation: its row has no market value
    /// or unrealized profit and loss, and the totals leave it out, so they
    /// still add up across the rows they cover.
    /// </summary>
    private static PortfolioValuationView Value(
        Portfolio portfolio,
        IReadOnlyList<(string CoinId, CoinPosition Position)> positions,
        IReadOnlyList<CoinPrice> livePrices)
    {
        var coins = new List<CoinValuationView>();
        var unpricedCoins = new List<string>();
        foreach (var (coinId, position) in positions)
        {
            decimal? marketValue = 0m;
            if (position.Quantity > 0m)
            {
                var livePrice = livePrices.FirstOrDefault(c => c.CoinId == coinId)?.Amounts.FirstOrDefault(a => a.Currency == portfolio.VsCurrency);
                if (livePrice is null)
                {
                    marketValue = null;
                    unpricedCoins.Add(coinId);
                }
                else
                {
                    marketValue = position.Quantity * livePrice.Amount;
                }
            }

            coins.Add(new CoinValuationView
            {
                CoinId = coinId,
                Quantity = position.Quantity,
                MarketValue = marketValue,
                CostBasis = position.CostBasis,
                UnrealizedProfitAndLoss = marketValue - position.CostBasis,
                RealizedProfitAndLoss = position.RealizedProfitAndLoss,
            });
        }

        var priced = coins.Where(c => c.MarketValue is not null).ToList();
        return new PortfolioValuationView
        {
            PortfolioId = portfolio.Id,
            VsCurrency = portfolio.VsCurrency,
            CostBasisMethod = portfolio.CostBasisMethod,
            Coins = coins,
            UnpricedCoins = unpricedCoins,
            Totals = new PortfolioValuationTotals
            {
                MarketValue = priced.Sum(c => c.MarketValue ?? 0m),
                CostBasis = priced.Sum(c => c.CostBasis),
                UnrealizedProfitAndLoss = priced.Sum(c => c.UnrealizedProfitAndLoss ?? 0m),
                RealizedProfitAndLoss = priced.Sum(c => c.RealizedProfitAndLoss),
            },
        };
    }

    /// <summary>
    /// Checks the rules on a transaction's details that need more than the
    /// ledger — the clock and CoinGecko — returning the failure to report, or
    /// null when they hold. Details that break the entity's own rules throw
    /// first, before CoinGecko is asked about the coin. Only a coin new to the
    /// ledger is checked upstream: one any of its transactions already holds
    /// passed the check when that transaction was recorded, and asking again
    /// would stop a coin CoinGecko has since delisted from being sold, and
    /// every write to a held coin while CoinGecko is failing.
    /// </summary>
    private async Task<ServiceResult<PortfolioView>?> CheckTransactionAsync(
        TransactionDetails details,
        Portfolio portfolio,
        CancellationToken cancellationToken)
    {
        PortfolioTransaction.EnsureValid(details);
        if (RejectIfFutureDated(details) is { } futureDated)
        {
            return futureDated;
        }

        var coinId = PortfolioTransaction.NormalizeCoinId(details.CoinId);
        return portfolio.Transactions.Any(t => t.CoinId == coinId)
            ? null
            : await CheckCoinUpstreamAsync(coinId, portfolio.VsCurrency, cancellationToken);
    }

    /// <summary>
    /// Saves a ledger change the portfolio accepted, or reports why it refused
    /// one — an oversell, or amounts too large to compute — as a
    /// <see cref="ServiceErrorCode.RuleViolated"/>.
    /// </summary>
    private async Task<ServiceResult<PortfolioView>> SaveLedgerChangeAsync(Portfolio portfolio, ValidationOutcome outcome, CancellationToken cancellationToken)
    {
        if (!outcome.IsValid)
        {
            return ServiceResult<PortfolioView>.Failure(ServiceErrorCode.RuleViolated, outcome.ErrorMessage);
        }

        return await SaveAsync(portfolio, cancellationToken) switch
        {
            SaveOutcome.Saved => Success(portfolio),
            SaveOutcome.Deleted => NotFound(portfolio.Id),
            _ => ServiceResult<PortfolioView>.Failure(ServiceErrorCode.Conflict, ChangedConcurrentlyDetail(portfolio.Id)),
        };
    }

    /// <summary>
    /// Returns the failure to report when <paramref name="transaction"/>
    /// happened after the current time, or null when it did not; exactly now
    /// is allowed.
    /// </summary>
    private ServiceResult<PortfolioView>? RejectIfFutureDated(TransactionDetails transaction)
    {
        var now = timeProvider.GetUtcNow();
        return transaction.OccurredAt > now
            ? ServiceResult<PortfolioView>.Failure(
                ServiceErrorCode.RuleViolated,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"A transaction cannot be dated in the future: {UtcTimestamp.Format(transaction.OccurredAt)} is after the current time, {UtcTimestamp.Format(now)}."))
            : null;
    }

    /// <summary>
    /// Checks with CoinGecko that the coin exists and is priced in the
    /// portfolio's vs_currency — the price lookup valuation will make —
    /// returning the failure to report, or null when both hold. Either one
    /// missing is a <see cref="ServiceErrorCode.RuleViolated"/>; any other
    /// upstream failure is passed on unchanged.
    /// </summary>
    private async Task<ServiceResult<PortfolioView>?> CheckCoinUpstreamAsync(string coinId, string vsCurrency, CancellationToken cancellationToken)
    {
        var priceQuery = new CoinPriceQuery { CoinIds = [coinId], VsCurrencies = [vsCurrency] };
        var prices = await cryptocurrencyService.GetPricesAsync(priceQuery, cancellationToken);
        if (prices.ErrorCode is { } errorCode)
        {
            return errorCode == ServiceErrorCode.NotFound
                ? ServiceResult<PortfolioView>.Failure(ServiceErrorCode.RuleViolated, $"CoinGecko has no coin with id '{coinId}'.")
                : ServiceResult<PortfolioView>.Failure(errorCode, prices.ErrorDetail);
        }

        // A currency CoinGecko doesn't know is not an error upstream: the
        // coin just comes back without an amount in it.
        // `!` is safe: a successful result always carries its value.
        var pricedInCurrency = prices.Value!.Coins.Any(c => c.Amounts.Any(a => a.Currency == vsCurrency));
        return pricedInCurrency
            ? null
            : ServiceResult<PortfolioView>.Failure(ServiceErrorCode.RuleViolated, $"CoinGecko has no price for '{coinId}' in '{vsCurrency}'.");
    }

    /// <summary>
    /// Saves the pending change to <paramref name="portfolio"/>. On a
    /// <see cref="PersistenceConflictException"/> it discards that change, so
    /// nothing stale stays pending, and tells a portfolio another writer deleted
    /// from any other conflict. Any other exception, including cancellation and
    /// <see cref="PersistenceBusyException"/>, propagates.
    /// </summary>
    private async Task<SaveOutcome> SaveAsync(Portfolio portfolio, CancellationToken cancellationToken)
    {
        try
        {
            await repository.SaveChangesAsync(cancellationToken);
            return SaveOutcome.Saved;
        }
        catch (PersistenceConflictException)
        {
            repository.Discard(portfolio);

            // An id of zero is a portfolio never saved, which no one can have deleted.
            return portfolio.Id != 0 && !await repository.ExistsAsync(portfolio.Id, cancellationToken)
                ? SaveOutcome.Deleted
                : SaveOutcome.Conflicted;
        }
    }

    /// <summary>
    /// Returns the detail to report when the caller expected
    /// <paramref name="portfolio"/> at a version other than its own, or null
    /// when it expected none or the one it is at.
    /// </summary>
    private static string? StaleVersionDetail(Portfolio portfolio, int? expectedVersion) =>
        expectedVersion is { } expected && expected != portfolio.Version
            ? $"Portfolio {portfolio.Id} is at version {portfolio.Version}, not the expected version {expected}; reload it and try again."
            : null;

    private static ServiceResult<PortfolioView> Success(Portfolio portfolio) =>
        ServiceResult<PortfolioView>.Success(PortfolioMapper.ToView(portfolio));

    private static ServiceResult<PortfolioView> NotFound(int id) =>
        ServiceResult<PortfolioView>.Failure(ServiceErrorCode.NotFound, NotFoundDetail(id));

    private static ServiceResult<PortfolioView> TransactionNotFound(int portfolioId, int transactionId) =>
        ServiceResult<PortfolioView>.Failure(ServiceErrorCode.NotFound, $"Portfolio {portfolioId} has no transaction with id {transactionId}.");

    private static ServiceResult<PortfolioView> NameTaken(string name) =>
        ServiceResult<PortfolioView>.Failure(ServiceErrorCode.Conflict, $"A portfolio named '{name}' already exists.");

    private static string NotFoundDetail(int id) => $"No portfolio has id {id}.";

    private static string ChangedConcurrentlyDetail(int id) =>
        $"Portfolio {id} was changed by another operation; reload it and try again.";
}

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
        if (await repository.NameExistsAsync(command.Name, excludingId: null, cancellationToken))
        {
            return NameTaken(command.Name);
        }

        repository.Add(portfolio);
        return await TrySaveChangesAsync(cancellationToken) ? Success(portfolio) : NameTaken(command.Name);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<PortfolioView>> UpdateAsync(UpdatePortfolioCommand command, CancellationToken cancellationToken)
    {
        var portfolio = await repository.FindAsync(command.PortfolioId, cancellationToken);
        if (portfolio is null)
        {
            return NotFound(command.PortfolioId);
        }

        if (await repository.NameExistsAsync(command.Name, command.PortfolioId, cancellationToken))
        {
            return NameTaken(command.Name);
        }

        portfolio.Update(command.Name, command.CostBasisMethod);

        // Either race can lose this save: another portfolio taking the name
        // since the check above, or another write moving the version on.
        return await TrySaveChangesAsync(cancellationToken)
            ? Success(portfolio)
            : ServiceResult<PortfolioView>.Failure(
                ServiceErrorCode.Conflict,
                $"Portfolio {portfolio.Id} could not be saved: another portfolio may have taken the name '{command.Name}', or another operation changed it; reload it and try again.");
    }

    /// <inheritdoc />
    public async Task<ServiceResult> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var portfolio = await repository.FindAsync(id, cancellationToken);
        if (portfolio is null)
        {
            return ServiceResult.Failure(ServiceErrorCode.NotFound, NotFoundDetail(id));
        }

        repository.Remove(portfolio);
        return await TrySaveChangesAsync(cancellationToken)
            ? ServiceResult.Success()
            : ServiceResult.Failure(ServiceErrorCode.Conflict, ChangedConcurrentlyDetail(id));
    }

    /// <inheritdoc />
    public async Task<ServiceResult<PortfolioView>> AddTransactionAsync(AddTransactionCommand command, CancellationToken cancellationToken)
    {
        var portfolio = await repository.FindAsync(command.PortfolioId, cancellationToken);
        if (portfolio is null)
        {
            return NotFound(command.PortfolioId);
        }

        return await CheckTransactionAsync(command.Transaction, portfolio.VsCurrency, recordedCoinId: null, cancellationToken) is { } rejected
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

        var transaction = portfolio.FindTransaction(command.TransactionId);
        if (transaction is null)
        {
            return TransactionNotFound(command.PortfolioId, command.TransactionId);
        }

        return await CheckTransactionAsync(command.Transaction, portfolio.VsCurrency, transaction.CoinId, cancellationToken) is { } rejected
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
        var positions = portfolio.Transactions
            .GroupBy(t => t.CoinId)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => (CoinId: g.Key, Position: method.CalculatePosition(g)))
            .ToList();

        // Only the units still held need a price: a closed position is worth
        // nothing, and with none open there is nothing to ask CoinGecko.
        IReadOnlyList<CoinPrice> livePrices = [];
        var openCoinIds = positions.Where(p => p.Position.Quantity > 0m).Select(p => p.CoinId).ToList();
        if (openCoinIds.Count > 0)
        {
            var priceQuery = new CoinPriceQuery { CoinIds = openCoinIds, VsCurrencies = [portfolio.VsCurrency] };
            var prices = await cryptocurrencyService.GetPricesAsync(priceQuery, cancellationToken);
            if (prices.ErrorCode is { } errorCode)
            {
                // The portfolio exists, so a coin CoinGecko no longer knows is
                // an upstream problem, not a missing portfolio.
                return errorCode == ServiceErrorCode.NotFound
                    ? ServiceResult<PortfolioValuationView>.Failure(
                        ServiceErrorCode.UpstreamUnavailable,
                        $"CoinGecko does not know one of the coins held: {string.Join(", ", openCoinIds)}.")
                    : ServiceResult<PortfolioValuationView>.Failure(errorCode, prices.ErrorDetail);
            }

            // `!` is safe: a successful result always carries its value.
            livePrices = prices.Value!.Coins;
        }

        return Value(portfolio, positions, livePrices);
    }

    /// <summary>
    /// Joins each coin's position with its live price, or reports the open
    /// coin CoinGecko returned no price for, in the portfolio's vs_currency,
    /// as <see cref="ServiceErrorCode.UpstreamUnavailable"/>.
    /// </summary>
    private static ServiceResult<PortfolioValuationView> Value(
        Portfolio portfolio,
        IReadOnlyList<(string CoinId, CoinPosition Position)> positions,
        IReadOnlyList<CoinPrice> livePrices)
    {
        var coins = new List<CoinValuationView>();
        foreach (var (coinId, position) in positions)
        {
            var marketValue = 0m;
            if (position.Quantity > 0m)
            {
                var livePrice = livePrices.FirstOrDefault(c => c.CoinId == coinId)?.Amounts.FirstOrDefault(a => a.Currency == portfolio.VsCurrency);
                if (livePrice is null)
                {
                    return ServiceResult<PortfolioValuationView>.Failure(
                        ServiceErrorCode.UpstreamUnavailable,
                        $"CoinGecko returned no price for '{coinId}' in '{portfolio.VsCurrency}'.");
                }

                marketValue = position.Quantity * livePrice.Amount;
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

        return ServiceResult<PortfolioValuationView>.Success(new PortfolioValuationView
        {
            PortfolioId = portfolio.Id,
            VsCurrency = portfolio.VsCurrency,
            CostBasisMethod = portfolio.CostBasisMethod,
            Coins = coins,
            Totals = new PortfolioValuationTotals
            {
                MarketValue = coins.Sum(c => c.MarketValue),
                CostBasis = coins.Sum(c => c.CostBasis),
                UnrealizedProfitAndLoss = coins.Sum(c => c.UnrealizedProfitAndLoss),
                RealizedProfitAndLoss = coins.Sum(c => c.RealizedProfitAndLoss),
            },
        });
    }

    /// <summary>
    /// Checks the rules on a transaction's details that need more than the
    /// ledger — the clock and CoinGecko — returning the failure to report, or
    /// null when they hold. Details that break the entity's own rules throw
    /// first, before CoinGecko is asked about the coin. The coin is checked
    /// upstream unless it is <paramref name="recordedCoinId"/>, the coin the
    /// transaction already holds, which passed the check when it was recorded.
    /// </summary>
    private async Task<ServiceResult<PortfolioView>?> CheckTransactionAsync(
        TransactionDetails details,
        string vsCurrency,
        string? recordedCoinId,
        CancellationToken cancellationToken)
    {
        PortfolioTransaction.EnsureValid(details);
        if (RejectIfFutureDated(details) is { } futureDated)
        {
            return futureDated;
        }

        var coinId = PortfolioTransaction.NormalizeCoinId(details.CoinId);
        return coinId == recordedCoinId ? null : await CheckCoinUpstreamAsync(coinId, vsCurrency, cancellationToken);
    }

    /// <summary>
    /// Saves a ledger change the portfolio accepted, or reports the oversell
    /// that made it refuse one as a <see cref="ServiceErrorCode.RuleViolated"/>.
    /// </summary>
    private async Task<ServiceResult<PortfolioView>> SaveLedgerChangeAsync(Portfolio portfolio, ValidationOutcome outcome, CancellationToken cancellationToken)
    {
        if (!outcome.IsValid)
        {
            return ServiceResult<PortfolioView>.Failure(ServiceErrorCode.RuleViolated, outcome.ErrorMessage);
        }

        return await TrySaveChangesAsync(cancellationToken)
            ? Success(portfolio)
            : ServiceResult<PortfolioView>.Failure(ServiceErrorCode.Conflict, ChangedConcurrentlyDetail(portfolio.Id));
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
                    $"A transaction cannot be dated in the future: {transaction.OccurredAt.UtcDateTime:yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'} is after the current time, {now.UtcDateTime:yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'}."))
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
    /// Saves pending changes, returning false when the repository reports a
    /// <see cref="PersistenceConflictException"/>. Any other exception,
    /// including cancellation, propagates.
    /// </summary>
    private async Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await repository.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (PersistenceConflictException)
        {
            return false;
        }
    }

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

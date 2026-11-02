using CryptoStuff.Core.Portfolios;

namespace CryptoStuff.Core.UnitTests.TestSupport;

/// <summary>
/// An in-memory <see cref="IPortfolioRepository"/> that behaves like a unit of
/// work: <see cref="Add"/> and <see cref="Remove"/> stay pending until
/// <see cref="SaveChangesAsync"/>, which assigns portfolio and transaction ids
/// the way the database does — increasing and never reused.
/// </summary>
internal sealed class FakePortfolioRepository : IPortfolioRepository
{
    private readonly List<Portfolio> _saved = [];
    private readonly List<Portfolio> _pendingAdds = [];
    private readonly List<Portfolio> _pendingRemoves = [];
    private int _lastPortfolioId;
    private int _lastTransactionId;

    public int SaveChangesCallCount { get; private set; }

    public List<CancellationToken> CapturedCancellationTokens { get; } = [];

    /// <summary>When set, the next <see cref="SaveChangesAsync"/> throws it and saves nothing.</summary>
    public Exception? SaveChangesException { get; set; }

    public IReadOnlyList<Portfolio> Saved => _saved;

    /// <summary>
    /// Stores <paramref name="portfolio"/> as already saved, assigning it the
    /// next id and its transactions theirs.
    /// </summary>
    public Portfolio Seed(Portfolio portfolio)
    {
        PortfolioStorage.AssignId(portfolio, ++_lastPortfolioId);
        AssignTransactionIds(portfolio);
        _saved.Add(portfolio);
        return portfolio;
    }

    public Task<IReadOnlyList<Portfolio>> ListAsync(CancellationToken cancellationToken)
    {
        CapturedCancellationTokens.Add(cancellationToken);
        return Task.FromResult<IReadOnlyList<Portfolio>>(_saved.OrderBy(p => p.Id).ToArray());
    }

    public Task<Portfolio?> FindAsync(int id, CancellationToken cancellationToken)
    {
        CapturedCancellationTokens.Add(cancellationToken);
        return Task.FromResult(_saved.SingleOrDefault(p => p.Id == id));
    }

    public Task<bool> NameExistsAsync(string name, int? excludingId, CancellationToken cancellationToken)
    {
        CapturedCancellationTokens.Add(cancellationToken);
        return Task.FromResult(_saved.Any(p => p.Id != excludingId && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)));
    }

    public void Add(Portfolio portfolio) => _pendingAdds.Add(portfolio);

    public void Remove(Portfolio portfolio) => _pendingRemoves.Add(portfolio);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCallCount++;
        CapturedCancellationTokens.Add(cancellationToken);
        if (SaveChangesException is not null)
        {
            _pendingAdds.Clear();
            _pendingRemoves.Clear();
            throw SaveChangesException;
        }

        foreach (var portfolio in _pendingAdds)
        {
            Seed(portfolio);
        }

        _saved.RemoveAll(_pendingRemoves.Contains);
        foreach (var portfolio in _saved)
        {
            AssignTransactionIds(portfolio);
        }

        _pendingAdds.Clear();
        _pendingRemoves.Clear();
        return Task.CompletedTask;
    }

    private void AssignTransactionIds(Portfolio portfolio)
    {
        foreach (var transaction in portfolio.Transactions.Where(t => t.Id == 0))
        {
            PortfolioStorage.AssignId(transaction, ++_lastTransactionId);
        }
    }
}

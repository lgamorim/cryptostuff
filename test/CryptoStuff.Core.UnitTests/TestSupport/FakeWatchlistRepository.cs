using CryptoStuff.Core.Watchlists;

namespace CryptoStuff.Core.UnitTests.TestSupport;

/// <summary>
/// An in-memory <see cref="IWatchlistRepository"/> that behaves like a unit of
/// work: <see cref="Add"/> and <see cref="Remove"/> stay pending until
/// <see cref="SaveChangesAsync"/>, which assigns ids the way the database
/// does — increasing and never reused.
/// </summary>
internal sealed class FakeWatchlistRepository : IWatchlistRepository
{
    private readonly List<Watchlist> _saved = [];
    private readonly List<Watchlist> _pendingAdds = [];
    private readonly List<Watchlist> _pendingRemoves = [];
    private int _lastId;

    public int SaveChangesCallCount { get; private set; }

    public List<CancellationToken> CapturedCancellationTokens { get; } = [];

    /// <summary>When set, the next <see cref="SaveChangesAsync"/> throws it and saves nothing.</summary>
    public Exception? SaveChangesException { get; set; }

    public IReadOnlyList<Watchlist> Saved => _saved;

    /// <summary>Stores <paramref name="watchlist"/> as already saved, assigning it the next id.</summary>
    public Watchlist Seed(Watchlist watchlist)
    {
        AssignId(watchlist);
        _saved.Add(watchlist);
        return watchlist;
    }

    public Task<IReadOnlyList<Watchlist>> ListAsync(CancellationToken cancellationToken)
    {
        CapturedCancellationTokens.Add(cancellationToken);
        return Task.FromResult<IReadOnlyList<Watchlist>>(_saved.OrderBy(w => w.Id).ToArray());
    }

    public Task<Watchlist?> FindAsync(int id, CancellationToken cancellationToken)
    {
        CapturedCancellationTokens.Add(cancellationToken);
        return Task.FromResult(_saved.SingleOrDefault(w => w.Id == id));
    }

    public Task<bool> NameExistsAsync(string name, int? excludingId, CancellationToken cancellationToken)
    {
        CapturedCancellationTokens.Add(cancellationToken);
        return Task.FromResult(_saved.Any(w => w.Id != excludingId && string.Equals(w.Name, name, StringComparison.OrdinalIgnoreCase)));
    }

    public void Add(Watchlist watchlist) => _pendingAdds.Add(watchlist);

    public void Remove(Watchlist watchlist) => _pendingRemoves.Add(watchlist);

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

        foreach (var watchlist in _pendingAdds)
        {
            Seed(watchlist);
        }

        _saved.RemoveAll(_pendingRemoves.Contains);
        _pendingAdds.Clear();
        _pendingRemoves.Clear();
        return Task.CompletedTask;
    }

    // Watchlist.Id has a private setter, which the database provider writes
    // through; reflection stands in for it here. `!` is safe: the property is
    // declared on the type, so the lookup cannot return null.
    private void AssignId(Watchlist watchlist) =>
        typeof(Watchlist).GetProperty(nameof(Watchlist.Id))!.SetValue(watchlist, ++_lastId);
}

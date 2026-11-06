using CryptoStuff.Core.Alerts;

namespace CryptoStuff.Core.UnitTests.TestSupport;

/// <summary>
/// An in-memory <see cref="IAlertRepository"/> that behaves like a unit of
/// work: <see cref="Add"/> and <see cref="Remove"/> stay pending until
/// <see cref="SaveChangesAsync"/>, which assigns ids the way the database
/// does — increasing and never reused.
/// </summary>
internal sealed class FakeAlertRepository : IAlertRepository
{
    private readonly List<PriceAlert> _saved = [];
    private readonly List<PriceAlert> _pendingAdds = [];
    private readonly List<PriceAlert> _pendingRemoves = [];
    private int _lastId;

    public int SaveChangesCallCount { get; private set; }

    public List<CancellationToken> CapturedCancellationTokens { get; } = [];

    /// <summary>When set, the next <see cref="SaveChangesAsync"/> throws it and saves nothing.</summary>
    public Exception? SaveChangesException { get; set; }

    public IReadOnlyList<PriceAlert> Saved => _saved;

    /// <summary>Stores <paramref name="alert"/> as already saved, assigning it the next id.</summary>
    public PriceAlert Seed(PriceAlert alert)
    {
        AlertStorage.AssignId(alert, ++_lastId);
        _saved.Add(alert);
        return alert;
    }

    public Task<IReadOnlyList<PriceAlert>> ListAsync(CancellationToken cancellationToken)
    {
        CapturedCancellationTokens.Add(cancellationToken);
        return Task.FromResult<IReadOnlyList<PriceAlert>>(_saved.OrderBy(a => a.Id).ToArray());
    }

    public Task<PriceAlert?> FindAsync(int id, CancellationToken cancellationToken)
    {
        CapturedCancellationTokens.Add(cancellationToken);
        return Task.FromResult(_saved.SingleOrDefault(a => a.Id == id));
    }

    public void Add(PriceAlert alert) => _pendingAdds.Add(alert);

    public void Remove(PriceAlert alert) => _pendingRemoves.Add(alert);

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

        foreach (var alert in _pendingAdds)
        {
            Seed(alert);
        }

        _saved.RemoveAll(_pendingRemoves.Contains);
        _pendingAdds.Clear();
        _pendingRemoves.Clear();
        return Task.CompletedTask;
    }
}

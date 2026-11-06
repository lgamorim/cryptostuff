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
    private readonly Dictionary<PriceAlert, int> _trackedVersions = [];
    private readonly Dictionary<int, AlertState> _persistedStates = [];
    private readonly HashSet<int> _changedConcurrently = [];
    private int _lastId;

    public int SaveChangesCallCount { get; private set; }

    public List<CancellationToken> CapturedCancellationTokens { get; } = [];

    /// <summary>When set, the next <see cref="SaveChangesAsync"/> throws it and saves nothing.</summary>
    public Exception? SaveChangesException { get; set; }

    /// <summary>
    /// When set, <see cref="ListArmedAsync"/> returns every alert, triggered
    /// ones included, as a repository that broke its contract would.
    /// </summary>
    public bool ListArmedIncludesTriggeredAlerts { get; set; }

    public IReadOnlyList<PriceAlert> Saved => _saved;

    /// <summary>
    /// Makes another writer win the race for the alert with the given id: any
    /// save that writes a change to it conflicts.
    /// </summary>
    public void ChangeConcurrently(int id) => _changedConcurrently.Add(id);

    /// <summary>The state the alert with the given id has in storage, whatever its in-memory copy says.</summary>
    public AlertState PersistedStateOf(int id) => _persistedStates[id];

    /// <summary>Stores <paramref name="alert"/> as already saved, assigning it the next id.</summary>
    public PriceAlert Seed(PriceAlert alert)
    {
        AlertStorage.AssignId(alert, ++_lastId);
        _saved.Add(alert);
        _persistedStates[alert.Id] = alert.State;
        return alert;
    }

    public Task<IReadOnlyList<PriceAlert>> ListAsync(CancellationToken cancellationToken)
    {
        CapturedCancellationTokens.Add(cancellationToken);
        return Task.FromResult<IReadOnlyList<PriceAlert>>(_saved.OrderBy(a => a.Id).Select(Track).ToArray());
    }

    public Task<IReadOnlyList<PriceAlert>> ListArmedAsync(CancellationToken cancellationToken)
    {
        CapturedCancellationTokens.Add(cancellationToken);
        return Task.FromResult<IReadOnlyList<PriceAlert>>(_saved.Where(a => ListArmedIncludesTriggeredAlerts || a.State == AlertState.Armed).OrderBy(a => a.Id).Select(Track).ToArray());
    }

    public Task<PriceAlert?> FindAsync(int id, CancellationToken cancellationToken)
    {
        CapturedCancellationTokens.Add(cancellationToken);
        return Task.FromResult(_saved.SingleOrDefault(a => a.Id == id) is { } alert ? Track(alert) : null);
    }

    public void Add(PriceAlert alert) => _pendingAdds.Add(alert);

    public void Remove(PriceAlert alert) => _pendingRemoves.Add(alert);

    /// <summary>Stops tracking <paramref name="alert"/>, so a save no longer writes its changes.</summary>
    public void Discard(PriceAlert alert) => _trackedVersions.Remove(alert);

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

        var changed = _trackedVersions.Where(tracked => tracked.Key.Version != tracked.Value).Select(tracked => tracked.Key).ToArray();
        if (changed.Any(alert => _changedConcurrently.Contains(alert.Id)))
        {
            throw new PersistenceConflictException("An alert was changed by another writer.");
        }

        foreach (var alert in changed)
        {
            _trackedVersions[alert] = alert.Version;
            _persistedStates[alert.Id] = alert.State;
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

    // Starts tracking an alert the way a context does when it returns one, at the version it was read at.
    private PriceAlert Track(PriceAlert alert)
    {
        _trackedVersions.TryAdd(alert, alert.Version);
        return alert;
    }
}

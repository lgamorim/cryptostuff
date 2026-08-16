namespace CryptoStuff.Composition.UnitTests.TestSupport;

/// <summary>
/// A test double that returns a queued sequence of responses, one per call
/// to <see cref="SendAsync"/> — unlike CoinGecko.UnitTests' own
/// <c>FakeHttpMessageHandler</c> (one canned response for the handler's
/// lifetime), this lets a test script a whole retry sequence (e.g. 429, 429,
/// 200). Each response comes from its own factory so a fresh, undisposed
/// <see cref="HttpResponseMessage"/> is produced per call.
/// </summary>
internal sealed class SequencedHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpResponseMessage>> _responseFactories;
    private readonly List<HttpRequestMessage> _requests = [];

    private SequencedHttpMessageHandler(IEnumerable<Func<HttpResponseMessage>> responseFactories) =>
        _responseFactories = new Queue<Func<HttpResponseMessage>>(responseFactories);

    /// <summary>
    /// Returns each given response factory in order, one per call; throws if
    /// called more times than factories were queued.
    /// </summary>
    public static SequencedHttpMessageHandler ReturningInOrder(params Func<HttpResponseMessage>[] responseFactories) =>
        new(responseFactories);

    public int CallCount { get; private set; }

    public IReadOnlyList<HttpRequestMessage> Requests => _requests;

    public CancellationToken? LastCancellationToken { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CallCount++;
        _requests.Add(request);
        LastCancellationToken = cancellationToken;

        if (_responseFactories.Count == 0)
        {
            throw new InvalidOperationException(
                $"{nameof(SequencedHttpMessageHandler)} received more calls ({CallCount}) than responses were queued.");
        }

        return Task.FromResult(_responseFactories.Dequeue()());
    }
}

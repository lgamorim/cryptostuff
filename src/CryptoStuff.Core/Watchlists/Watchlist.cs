namespace CryptoStuff.Core.Watchlists;

/// <summary>
/// A named, ordered list of distinct CoinGecko coin ids. Coin ids are stored
/// lowercased, so ids differing only in case are the same coin. The id is
/// zero until the repository first saves the watchlist and assigns one.
/// </summary>
public sealed class Watchlist
{
    private readonly List<string> _coinIds = [];

    // Takes only the name so the persistence layer can construct the entity
    // through this constructor, binding the parameter to Name, without a
    // public parameterless constructor that would bypass the name rule.
    private Watchlist(string name)
    {
        Name = name;
    }

    /// <summary>The watchlist's id; zero until first saved.</summary>
    public int Id { get; private set; }

    /// <summary>The watchlist's name, satisfying <see cref="NameValidator"/>.</summary>
    public string Name { get; private set; }

    /// <summary>The watchlist's lowercased coin ids, in the order they were added.</summary>
    public IReadOnlyList<string> CoinIds => _coinIds.AsReadOnly();

    /// <summary>Creates an empty watchlist with the given name.</summary>
    /// <exception cref="ArgumentException"><paramref name="name"/> fails <see cref="NameValidator"/>.</exception>
    public static Watchlist Create(string name)
    {
        EnsureValidName(name);
        return new Watchlist(name);
    }

    /// <summary>Gives the watchlist a new name.</summary>
    /// <exception cref="ArgumentException"><paramref name="name"/> fails <see cref="NameValidator"/>.</exception>
    public void Rename(string name)
    {
        EnsureValidName(name);
        Name = name;
    }

    /// <summary>Whether the watchlist holds <paramref name="coinId"/>, compared case-insensitively.</summary>
    /// <exception cref="ArgumentException"><paramref name="coinId"/> is null, empty, or whitespace-only.</exception>
    public bool Contains(string coinId) => _coinIds.Contains(NormalizeCoinId(coinId));

    /// <summary>
    /// Appends <paramref name="coinId"/>, lowercased, to the end of the list.
    /// Returns false, leaving the list unchanged, when the coin is already present.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="coinId"/> is null, empty, or whitespace-only.</exception>
    public bool AddCoin(string coinId)
    {
        var normalized = NormalizeCoinId(coinId);
        if (_coinIds.Contains(normalized))
        {
            return false;
        }

        _coinIds.Add(normalized);
        return true;
    }

    /// <summary>
    /// Removes <paramref name="coinId"/>, keeping the remaining coins in order.
    /// Returns false, leaving the list unchanged, when the coin is absent.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="coinId"/> is null, empty, or whitespace-only.</exception>
    public bool RemoveCoin(string coinId) => _coinIds.Remove(NormalizeCoinId(coinId));

    /// <summary>The form in which a coin id is stored and compared.</summary>
    internal static string NormalizeCoinId(string coinId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(coinId);
        return coinId.ToLowerInvariant();
    }

    private static void EnsureValidName(string name)
    {
        var outcome = NameValidator.Validate(name, nameof(name));
        if (!outcome.IsValid)
        {
            throw new ArgumentException(outcome.ErrorMessage, nameof(name));
        }
    }
}

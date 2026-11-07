using System.Globalization;

namespace CryptoStuff.Core.Portfolios;

/// <summary>
/// A named ledger of buys and sells, priced in one vs_currency fixed at
/// creation. The ledger never oversells: replaying its transactions in ledger
/// order — by when each happened, then by
/// <see cref="PortfolioTransaction.Sequence"/> — never takes a coin's holding
/// below zero. Every add, replace, and removal is checked against the whole
/// replayed ledger, because removing an earlier buy or moving a sell earlier
/// can break a later sell. The id is zero until the repository first saves
/// the portfolio and assigns one.
/// </summary>
public sealed class Portfolio
{
    private readonly List<PortfolioTransaction> _transactions = [];

    // Takes only the scalar properties so the persistence layer can construct
    // the entity through this constructor, binding each parameter to its
    // property, without a public parameterless constructor that would bypass
    // the rules in Create.
    private Portfolio(string name, string vsCurrency, CostBasisMethod costBasisMethod)
    {
        Name = name;
        VsCurrency = vsCurrency;
        CostBasisMethod = costBasisMethod;
    }

    /// <summary>The portfolio's id; zero until first saved.</summary>
    public int Id { get; private set; }

    /// <summary>The portfolio's name, satisfying <see cref="NameValidator"/>.</summary>
    public string Name { get; private set; }

    /// <summary>The lowercased vs_currency the portfolio is priced in; it never changes.</summary>
    public string VsCurrency { get; }

    /// <summary>How the portfolio attributes cost to the coins it sells.</summary>
    public CostBasisMethod CostBasisMethod { get; private set; }

    /// <summary>
    /// The portfolio's version: 1 when created, and one higher after every
    /// change to the portfolio or its ledger, so a save can detect that the
    /// stored portfolio changed since it was read.
    /// </summary>
    public int Version { get; private set; } = 1;

    /// <summary>The portfolio's transactions in ledger order.</summary>
    public IReadOnlyList<PortfolioTransaction> Transactions => InLedgerOrder(_transactions).ToList().AsReadOnly();

    /// <summary>Creates an empty portfolio priced in <paramref name="vsCurrency"/>, which is stored lowercased.</summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> fails <see cref="NameValidator"/>, or
    /// <paramref name="vsCurrency"/> is null, empty, or whitespace-only.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="costBasisMethod"/> is not a defined method.</exception>
    public static Portfolio Create(string name, string vsCurrency, CostBasisMethod costBasisMethod)
    {
        EnsureValidName(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(vsCurrency);
        EnsureDefined(costBasisMethod);
        return new Portfolio(name, vsCurrency.ToLowerInvariant(), costBasisMethod);
    }

    /// <summary>Gives the portfolio a new name and cost-basis method. The vs_currency never changes.</summary>
    /// <exception cref="ArgumentException"><paramref name="name"/> fails <see cref="NameValidator"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="costBasisMethod"/> is not a defined method.</exception>
    public void Update(string name, CostBasisMethod costBasisMethod)
    {
        EnsureValidName(name);
        EnsureDefined(costBasisMethod);
        Name = name;
        CostBasisMethod = costBasisMethod;
        Version++;
    }

    /// <summary>
    /// Gets the transaction with the given id, or null when none of this
    /// portfolio's transactions has it. Ids are assigned when the portfolio is saved.
    /// </summary>
    public PortfolioTransaction? FindTransaction(int transactionId) =>
        _transactions.Find(t => t.Id == transactionId);

    /// <summary>
    /// Records a new transaction, numbered after every transaction the
    /// portfolio holds. A failed outcome names the coin and date of the sell
    /// that would oversell, and leaves the portfolio unchanged.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="details"/> breaks a rule <see cref="TransactionDetails"/> documents.</exception>
    public ValidationOutcome AddTransaction(TransactionDetails details)
    {
        var nextSequence = _transactions.Count == 0 ? 1 : _transactions.Max(t => t.Sequence) + 1;
        var transaction = PortfolioTransaction.Create(nextSequence, details);
        return ApplyIfLedgerHolds([.. _transactions, transaction], () => _transactions.Add(transaction));
    }

    /// <summary>
    /// Replaces every detail of one of the portfolio's transactions, keeping
    /// its id and sequence. A failed outcome names the coin and date of the
    /// sell that would oversell, and leaves the portfolio unchanged.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="transaction"/> is not one of this portfolio's, or
    /// <paramref name="details"/> breaks a rule <see cref="TransactionDetails"/> documents.
    /// </exception>
    public ValidationOutcome ReplaceTransaction(PortfolioTransaction transaction, TransactionDetails details)
    {
        EnsureOwned(transaction);
        var replacement = PortfolioTransaction.Create(transaction.Sequence, details);
        return ApplyIfLedgerHolds(
            _transactions.Select(t => t == transaction ? replacement : t),
            () => transaction.ReplaceWith(replacement));
    }

    /// <summary>
    /// Deletes one of the portfolio's transactions. A failed outcome names
    /// the coin and date of the sell that would oversell, and leaves the
    /// portfolio unchanged.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="transaction"/> is not one of this portfolio's.</exception>
    public ValidationOutcome RemoveTransaction(PortfolioTransaction transaction)
    {
        EnsureOwned(transaction);
        return ApplyIfLedgerHolds(_transactions.Where(t => t != transaction), () => _transactions.Remove(transaction));
    }

    /// <summary>
    /// Replays <paramref name="ledgerAfterChange"/>, the whole ledger as it
    /// would be after a change, and makes the change only when no holding
    /// goes below zero.
    /// </summary>
    private ValidationOutcome ApplyIfLedgerHolds(IEnumerable<PortfolioTransaction> ledgerAfterChange, Action change)
    {
        var outcome = Replay(ledgerAfterChange);
        if (outcome.IsValid)
        {
            change();
            Version++;
        }

        return outcome;
    }

    private void EnsureOwned(PortfolioTransaction transaction)
    {
        if (!_transactions.Contains(transaction))
        {
            throw new ArgumentException("The transaction does not belong to this portfolio.", nameof(transaction));
        }
    }

    private static ValidationOutcome Replay(IEnumerable<PortfolioTransaction> transactions)
    {
        var holdings = new Dictionary<string, decimal>();
        foreach (var transaction in InLedgerOrder(transactions))
        {
            var held = holdings.GetValueOrDefault(transaction.CoinId);
            if (transaction.Kind == TransactionKind.Sell && transaction.Quantity > held)
            {
                return Oversold(transaction, held);
            }

            holdings[transaction.CoinId] = transaction.Kind == TransactionKind.Buy
                ? held + transaction.Quantity
                : held - transaction.Quantity;
        }

        return ValidationOutcome.Success();
    }

    // The stored list's order means nothing, since loading fills it in
    // whatever order rows come back; Sequence breaks ties between
    // transactions that happened at the same instant. The cost-basis methods
    // replay in this order too.
    internal static IEnumerable<PortfolioTransaction> InLedgerOrder(IEnumerable<PortfolioTransaction> transactions) =>
        transactions.OrderBy(t => t.OccurredAt).ThenBy(t => t.Sequence);

    private static ValidationOutcome Oversold(PortfolioTransaction sell, decimal held) =>
        ValidationOutcome.Failure(string.Create(
            CultureInfo.InvariantCulture,
            $"Selling {sell.Quantity} {sell.CoinId} on {UtcTimestamp.Format(sell.OccurredAt)} would take its holding below zero: only {held} would be held then."));

    private static void EnsureValidName(string name)
    {
        var outcome = NameValidator.Validate(name, nameof(name));
        if (!outcome.IsValid)
        {
            throw new ArgumentException(outcome.ErrorMessage, nameof(name));
        }
    }

    private static void EnsureDefined(CostBasisMethod costBasisMethod)
    {
        if (!Enum.IsDefined(costBasisMethod))
        {
            throw new ArgumentOutOfRangeException(nameof(costBasisMethod), costBasisMethod, "The cost-basis method is not defined.");
        }
    }
}

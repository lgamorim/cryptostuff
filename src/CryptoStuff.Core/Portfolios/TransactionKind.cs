namespace CryptoStuff.Core.Portfolios;

/// <summary>
/// Whether a portfolio transaction adds to or takes from a coin's holding.
/// Travels as the text token <see cref="TransactionKindParser"/> accepts.
/// </summary>
public enum TransactionKind
{
    /// <summary>Adds the quantity to the holding (<c>buy</c>).</summary>
    Buy,

    /// <summary>Takes the quantity from the holding (<c>sell</c>).</summary>
    Sell,
}

using System.Reflection;
using CryptoStuff.Core.Portfolios;

namespace CryptoStuff.Core.UnitTests.TestSupport;

/// <summary>
/// Stands in for the database provider, which writes a portfolio's private
/// state when it saves or loads one. Reflection reaches that state here, so
/// the entities need no wider setters or InternalsVisibleTo just for tests.
/// </summary>
internal static class PortfolioStorage
{
    // The backing field behind Portfolio.Transactions.
    private const string TransactionsField = "_transactions";

    /// <summary>Gives <paramref name="portfolio"/> the id a save assigns.</summary>
    public static void AssignId(Portfolio portfolio, int id) =>
        // `!` is safe: the property is declared on the type, so the lookup cannot return null.
        typeof(Portfolio).GetProperty(nameof(Portfolio.Id))!.SetValue(portfolio, id);

    /// <summary>Gives <paramref name="transaction"/> the id a save assigns.</summary>
    public static void AssignId(PortfolioTransaction transaction, int id) =>
        // `!` is safe: the property is declared on the type, so the lookup cannot return null.
        typeof(PortfolioTransaction).GetProperty(nameof(PortfolioTransaction.Id))!.SetValue(transaction, id);

    /// <summary>
    /// Reverses the order the portfolio holds its transactions in, the way a
    /// load may return rows in any order: nothing orders a collection the
    /// provider fills.
    /// </summary>
    public static void ReverseLoadedTransactionOrder(Portfolio portfolio)
    {
        // `!` is safe: the field is declared on Portfolio and always holds a list.
        var transactions = (List<PortfolioTransaction>)typeof(Portfolio)
            .GetField(TransactionsField, BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(portfolio)!;
        transactions.Reverse();
    }
}

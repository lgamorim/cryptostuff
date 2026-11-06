using CryptoStuff.Core.Alerts;

namespace CryptoStuff.Core.UnitTests.TestSupport;

/// <summary>
/// Stands in for the database provider, which writes an alert's private
/// state when it saves or loads one. Reflection reaches that state here, so
/// the entity needs no wider setters or InternalsVisibleTo just for tests.
/// </summary>
internal static class AlertStorage
{
    /// <summary>Gives <paramref name="alert"/> the id a save assigns.</summary>
    public static void AssignId(PriceAlert alert, int id) =>
        // `!` is safe: the property is declared on the type, so the lookup cannot return null.
        typeof(PriceAlert).GetProperty(nameof(PriceAlert.Id))!.SetValue(alert, id);
}

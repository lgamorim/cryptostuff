namespace CryptoStuff.Core.Alerts;

/// <summary>Maps a <see cref="PriceAlert"/> to an <see cref="AlertView"/>.</summary>
public static class AlertMapper
{
    /// <summary>Maps <paramref name="alert"/> to a view.</summary>
    public static AlertView ToView(PriceAlert alert) =>
        new()
        {
            Id = alert.Id,
            CoinId = alert.CoinId,
            VsCurrency = alert.VsCurrency,
            Direction = alert.Direction,
            Threshold = alert.Threshold,
            State = alert.State,
            TriggeredAt = alert.TriggeredAt,
            TriggeredPrice = alert.TriggeredPrice,
            Version = alert.Version,
        };
}

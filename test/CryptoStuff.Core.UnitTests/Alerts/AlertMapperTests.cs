using CryptoStuff.Core.Alerts;
using CryptoStuff.Core.UnitTests.TestSupport;

namespace CryptoStuff.Core.UnitTests.Alerts;

public class AlertMapperTests
{
    [Fact]
    public void Should_MapEveryField_When_ArmedAlertMapped()
    {
        var alert = PriceAlert.Create("bitcoin", "usd", AlertDirection.Below, 25000.5m);
        AlertStorage.AssignId(alert, 7);

        var view = AlertMapper.ToView(alert);

        view.Id.Should().Be(7);
        view.CoinId.Should().Be("bitcoin");
        view.VsCurrency.Should().Be("usd");
        view.Direction.Should().Be(AlertDirection.Below);
        view.Threshold.Should().Be(25000.5m);
        view.State.Should().Be(AlertState.Armed);
        view.TriggeredAt.Should().BeNull();
        view.TriggeredPrice.Should().BeNull();
        view.Version.Should().Be(1);
    }

    [Fact]
    public void Should_MapStateWhenAndAtWhatPriceItTriggered_When_TriggeredAlertMapped()
    {
        var alert = PriceAlert.Create("bitcoin", "usd", AlertDirection.Above, 100m);
        var at = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);
        alert.Trigger(105.25m, at);

        var view = AlertMapper.ToView(alert);

        view.State.Should().Be(AlertState.Triggered);
        view.TriggeredAt.Should().Be(at);
        view.TriggeredPrice.Should().Be(105.25m);
        view.Version.Should().Be(2);
    }
}

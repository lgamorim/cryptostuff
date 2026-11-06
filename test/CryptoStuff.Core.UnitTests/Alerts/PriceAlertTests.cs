using CryptoStuff.Core.Alerts;

namespace CryptoStuff.Core.UnitTests.Alerts;

public class PriceAlertTests
{
    // The smallest price step the tests move by: a satoshi, 1e-8.
    private const decimal Tick = 0.00000001m;

    private static readonly DateTimeOffset TriggerTime = new(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Should_CreateArmedAlertAtVersionOneWithNoTrigger_When_CreateGivenValidDetails()
    {
        var alert = PriceAlert.Create("bitcoin", "usd", AlertDirection.Above, 100m);

        alert.Id.Should().Be(0);
        alert.CoinId.Should().Be("bitcoin");
        alert.VsCurrency.Should().Be("usd");
        alert.Direction.Should().Be(AlertDirection.Above);
        alert.Threshold.Should().Be(100m);
        alert.State.Should().Be(AlertState.Armed);
        alert.TriggeredAt.Should().BeNull();
        alert.TriggeredPrice.Should().BeNull();
        alert.Version.Should().Be(1);
    }

    [Fact]
    public void Should_StoreCoinIdAndCurrencyLowercased_When_CreateGivenMixedCase()
    {
        var alert = PriceAlert.Create("BitCoin", "USD", AlertDirection.Below, 100m);

        alert.CoinId.Should().Be("bitcoin");
        alert.VsCurrency.Should().Be("usd");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_ThrowArgumentException_When_CreateGivenBlankCoinId(string? coinId)
    {
        // `!` lets the case pass null past the non-nullable parameter to exercise the guard.
        var act = () => PriceAlert.Create(coinId!, "usd", AlertDirection.Above, 100m);

        act.Should().Throw<ArgumentException>().WithParameterName("coinId");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_ThrowArgumentException_When_CreateGivenBlankVsCurrency(string? vsCurrency)
    {
        // `!` lets the case pass null past the non-nullable parameter to exercise the guard.
        var act = () => PriceAlert.Create("bitcoin", vsCurrency!, AlertDirection.Above, 100m);

        act.Should().Throw<ArgumentException>().WithParameterName("vsCurrency");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.00000001)]
    public void Should_ThrowArgumentOutOfRange_When_CreateGivenZeroOrNegativeThreshold(double threshold)
    {
        var act = () => PriceAlert.Create("bitcoin", "usd", AlertDirection.Above, (decimal)threshold);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("threshold");
    }

    [Fact]
    public void Should_AcceptSmallestPositiveThreshold_When_CreateGivenOneTickAboveZero()
    {
        var alert = PriceAlert.Create("bitcoin", "usd", AlertDirection.Above, Tick);

        alert.Threshold.Should().Be(Tick);
    }

    [Fact]
    public void Should_ThrowArgumentOutOfRange_When_CreateGivenUndefinedDirection()
    {
        var act = () => PriceAlert.Create("bitcoin", "usd", (AlertDirection)99, 100m);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("direction");
    }

    // IsMetBy.

    [Fact]
    public void Should_BeMet_When_AboveAlertSeesPriceExactlyAtThreshold()
    {
        var alert = PriceAlert.Create("bitcoin", "usd", AlertDirection.Above, 100m);

        var met = alert.IsMetBy(100m);

        met.Should().BeTrue();
    }

    [Fact]
    public void Should_BeMet_When_BelowAlertSeesPriceExactlyAtThreshold()
    {
        var alert = PriceAlert.Create("bitcoin", "usd", AlertDirection.Below, 100m);

        var met = alert.IsMetBy(100m);

        met.Should().BeTrue();
    }

    [Theory]
    [InlineData(AlertDirection.Above, 1, true)]
    [InlineData(AlertDirection.Above, -1, false)]
    [InlineData(AlertDirection.Below, -1, true)]
    [InlineData(AlertDirection.Below, 1, false)]
    public void Should_BeMetOnlyOnItsSideOfThreshold_When_PriceIsOneTickAwayFromIt(AlertDirection direction, int ticksFromThreshold, bool expected)
    {
        var alert = PriceAlert.Create("bitcoin", "usd", direction, 100m);

        var met = alert.IsMetBy(100m + (ticksFromThreshold * Tick));

        met.Should().Be(expected);
    }

    // Trigger.

    [Fact]
    public void Should_MoveToTriggeredRecordingWhenAndAtWhatPrice_When_TriggerCalledOnArmedAlert()
    {
        var alert = PriceAlert.Create("bitcoin", "usd", AlertDirection.Above, 100m);

        var outcome = alert.Trigger(105m, TriggerTime);

        outcome.IsValid.Should().BeTrue();
        alert.State.Should().Be(AlertState.Triggered);
        alert.TriggeredAt.Should().Be(TriggerTime);
        alert.TriggeredPrice.Should().Be(105m);
    }

    [Fact]
    public void Should_BumpVersion_When_TriggerCalledOnArmedAlert()
    {
        var alert = PriceAlert.Create("bitcoin", "usd", AlertDirection.Above, 100m);

        alert.Trigger(105m, TriggerTime);

        alert.Version.Should().Be(2);
    }

    [Fact]
    public void Should_RecordTriggerTimeAsUtc_When_TriggerGivenAnOffsetTime()
    {
        var alert = PriceAlert.Create("bitcoin", "usd", AlertDirection.Above, 100m);
        var plusTwo = new DateTimeOffset(2024, 6, 1, 14, 0, 0, TimeSpan.FromHours(2));

        alert.Trigger(105m, plusTwo);

        alert.TriggeredAt.Should().Be(TriggerTime);

        // `!` is safe: the assertion above fails the test unless TriggeredAt is set.
        alert.TriggeredAt!.Value.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Should_RejectAndLeaveAlertUnchanged_When_TriggerCalledOnTriggeredAlert()
    {
        var alert = PriceAlert.Create("bitcoin", "usd", AlertDirection.Above, 100m);
        alert.Trigger(105m, TriggerTime);

        var outcome = alert.Trigger(120m, TriggerTime.AddHours(1));

        outcome.IsValid.Should().BeFalse();
        outcome.ErrorMessage.Should().Contain("triggered");
        alert.State.Should().Be(AlertState.Triggered);
        alert.TriggeredAt.Should().Be(TriggerTime);
        alert.TriggeredPrice.Should().Be(105m);
        alert.Version.Should().Be(2);
    }

    // Rearm.

    [Fact]
    public void Should_MoveToArmedAndClearTheTriggerRecord_When_RearmCalledOnTriggeredAlert()
    {
        var alert = PriceAlert.Create("bitcoin", "usd", AlertDirection.Above, 100m);
        alert.Trigger(105m, TriggerTime);

        var outcome = alert.Rearm();

        outcome.IsValid.Should().BeTrue();
        alert.State.Should().Be(AlertState.Armed);
        alert.TriggeredAt.Should().BeNull();
        alert.TriggeredPrice.Should().BeNull();
    }

    [Fact]
    public void Should_BumpVersion_When_RearmCalledOnTriggeredAlert()
    {
        var alert = PriceAlert.Create("bitcoin", "usd", AlertDirection.Above, 100m);
        alert.Trigger(105m, TriggerTime);

        alert.Rearm();

        alert.Version.Should().Be(3);
    }

    [Fact]
    public void Should_RejectAndLeaveAlertUnchanged_When_RearmCalledOnArmedAlert()
    {
        var alert = PriceAlert.Create("bitcoin", "usd", AlertDirection.Above, 100m);

        var outcome = alert.Rearm();

        outcome.IsValid.Should().BeFalse();
        outcome.ErrorMessage.Should().Contain("armed");
        alert.State.Should().Be(AlertState.Armed);
        alert.Version.Should().Be(1);
    }

    [Fact]
    public void Should_TriggerAgainWithTheNewRecord_When_AlertWasRearmed()
    {
        var alert = PriceAlert.Create("bitcoin", "usd", AlertDirection.Above, 100m);
        alert.Trigger(105m, TriggerTime);
        alert.Rearm();

        var outcome = alert.Trigger(110m, TriggerTime.AddHours(1));

        outcome.IsValid.Should().BeTrue();
        alert.State.Should().Be(AlertState.Triggered);
        alert.TriggeredAt.Should().Be(TriggerTime.AddHours(1));
        alert.TriggeredPrice.Should().Be(110m);
        alert.Version.Should().Be(4);
    }

    // Update.

    [Fact]
    public void Should_ReplaceDirectionAndThresholdKeepingItArmedAndBumpVersion_When_UpdateCalledOnArmedAlert()
    {
        var alert = PriceAlert.Create("bitcoin", "usd", AlertDirection.Above, 100m);

        alert.Update(AlertDirection.Below, 50m);

        alert.Direction.Should().Be(AlertDirection.Below);
        alert.Threshold.Should().Be(50m);
        alert.State.Should().Be(AlertState.Armed);
        alert.Version.Should().Be(2);
    }

    [Fact]
    public void Should_RearmAndClearTheTriggerRecord_When_UpdateCalledOnTriggeredAlert()
    {
        var alert = PriceAlert.Create("bitcoin", "usd", AlertDirection.Above, 100m);
        alert.Trigger(105m, TriggerTime);

        alert.Update(AlertDirection.Above, 200m);

        alert.State.Should().Be(AlertState.Armed);
        alert.TriggeredAt.Should().BeNull();
        alert.TriggeredPrice.Should().BeNull();
        alert.Threshold.Should().Be(200m);
        alert.Version.Should().Be(3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.00000001)]
    public void Should_ThrowAndLeaveAlertUnchanged_When_UpdateGivenZeroOrNegativeThreshold(double threshold)
    {
        var alert = PriceAlert.Create("bitcoin", "usd", AlertDirection.Above, 100m);
        alert.Trigger(105m, TriggerTime);

        var act = () => alert.Update(AlertDirection.Below, (decimal)threshold);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("threshold");
        AssertUnchangedTriggeredAlert(alert);
    }

    [Fact]
    public void Should_ThrowAndLeaveAlertUnchanged_When_UpdateGivenUndefinedDirection()
    {
        var alert = PriceAlert.Create("bitcoin", "usd", AlertDirection.Above, 100m);
        alert.Trigger(105m, TriggerTime);

        var act = () => alert.Update((AlertDirection)99, 50m);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("direction");
        AssertUnchangedTriggeredAlert(alert);
    }

    // The alert as it was after Trigger(105, TriggerTime) on Above 100.
    private static void AssertUnchangedTriggeredAlert(PriceAlert alert)
    {
        alert.Direction.Should().Be(AlertDirection.Above);
        alert.Threshold.Should().Be(100m);
        alert.State.Should().Be(AlertState.Triggered);
        alert.TriggeredAt.Should().Be(TriggerTime);
        alert.TriggeredPrice.Should().Be(105m);
        alert.Version.Should().Be(2);
    }
}

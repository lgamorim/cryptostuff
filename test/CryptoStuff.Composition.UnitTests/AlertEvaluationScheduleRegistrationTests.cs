using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoStuff.Composition.UnitTests;

public class AlertEvaluationScheduleRegistrationTests
{
    private static IConfiguration BuildConfiguration(Dictionary<string, string?>? values = null) =>
        new ConfigurationBuilder().AddInMemoryCollection(values ?? []).Build();

    private static AlertEvaluationSchedule ResolveSchedule(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddAlertEvaluationSchedule(configuration);
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<AlertEvaluationSchedule>();
    }

    [Fact]
    public void Should_EvaluateEverySixtySeconds_When_EvaluationSecondsIsAbsent()
    {
        var configuration = BuildConfiguration();

        var schedule = ResolveSchedule(configuration);

        schedule.Interval.Should().Be(TimeSpan.FromSeconds(60));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_EvaluateEverySixtySeconds_When_EvaluationSecondsIsBlank(string evaluationSeconds)
    {
        var configuration = BuildConfiguration(new() { ["Alerts:EvaluationSeconds"] = evaluationSeconds });

        var schedule = ResolveSchedule(configuration);

        schedule.Interval.Should().Be(TimeSpan.FromSeconds(60));
    }

    // 4294967 s is the longest whole-second period a PeriodicTimer accepts:
    // its limit is 4294967294 ms.
    [Theory]
    [InlineData("1", 1)]
    [InlineData("90", 90)]
    [InlineData("4294967", 4294967)]
    public void Should_UseConfiguredInterval_When_EvaluationSecondsIsAPositiveWholeNumber(string evaluationSeconds, int expectedSeconds)
    {
        var configuration = BuildConfiguration(new() { ["Alerts:EvaluationSeconds"] = evaluationSeconds });

        var schedule = ResolveSchedule(configuration);

        schedule.Interval.Should().Be(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Theory]
    [InlineData("soon")]
    [InlineData("1.5")]
    [InlineData("1,000")]
    public void Should_ThrowActionableMessageBeforeRegistering_When_EvaluationSecondsIsNotAWholeNumber(string evaluationSeconds)
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new() { ["Alerts:EvaluationSeconds"] = evaluationSeconds });

        var act = () => services.AddAlertEvaluationSchedule(configuration);

        act.Should().Throw<InvalidOperationException>().WithMessage(ExpectedInvalidMessage(evaluationSeconds));
        services.Should().BeEmpty();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-60")]
    public void Should_ThrowActionableMessageBeforeRegistering_When_EvaluationSecondsIsNotPositive(string evaluationSeconds)
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new() { ["Alerts:EvaluationSeconds"] = evaluationSeconds });

        var act = () => services.AddAlertEvaluationSchedule(configuration);

        act.Should().Throw<InvalidOperationException>().WithMessage(ExpectedInvalidMessage(evaluationSeconds));
        services.Should().BeEmpty();
    }

    [Theory]
    [InlineData("4294968")]
    [InlineData("2147483647")]
    public void Should_ThrowActionableMessageBeforeRegistering_When_EvaluationSecondsExceedsTheTimerLimit(string evaluationSeconds)
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new() { ["Alerts:EvaluationSeconds"] = evaluationSeconds });

        var act = () => services.AddAlertEvaluationSchedule(configuration);

        act.Should().Throw<InvalidOperationException>().WithMessage(ExpectedInvalidMessage(evaluationSeconds));
        services.Should().BeEmpty();
    }

    private static string ExpectedInvalidMessage(string evaluationSeconds) =>
        $"Alerts:EvaluationSeconds is set to '{evaluationSeconds}', which is not a whole number of seconds from 1 to 4294967. "
        + "Set it to the number of seconds between background alert evaluations, or remove it to evaluate every 60 seconds.";
}

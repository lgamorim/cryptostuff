using CryptoStuff.CoinGecko;

namespace CryptoStuff.Core.UnitTests;

public class CoinDeveloperDataMapperTests
{
    private static CoinGeckoCoinHistory CreateHistory(CoinGeckoDeveloperData? developerData = null) =>
        new()
        {
            Id = "bitcoin",
            Symbol = "btc",
            Name = "Bitcoin",
            DeveloperData = developerData,
        };

    [Fact]
    public void Should_MapIdSymbolAndName_When_MappingHistory()
    {
        var history = CreateHistory();

        var view = CoinDeveloperDataMapper.ToView(history);

        view.Id.Should().Be("bitcoin");
        view.Symbol.Should().Be("btc");
        view.Name.Should().Be("Bitcoin");
    }

    [Fact]
    public void Should_ReturnAllNullDeveloperFields_When_DeveloperDataIsNull()
    {
        var history = CreateHistory(developerData: null);

        var view = CoinDeveloperDataMapper.ToView(history);

        view.Forks.Should().BeNull();
        view.Stars.Should().BeNull();
        view.Subscribers.Should().BeNull();
        view.TotalIssues.Should().BeNull();
        view.ClosedIssues.Should().BeNull();
        view.PullRequestsMerged.Should().BeNull();
        view.PullRequestContributors.Should().BeNull();
        view.CommitCount4Weeks.Should().BeNull();
        view.CodeAdditionsLast4Weeks.Should().BeNull();
        view.CodeDeletionsLast4Weeks.Should().BeNull();
    }

    [Fact]
    public void Should_MapAllDeveloperDataFields_When_DeveloperDataIsPresent()
    {
        var history = CreateHistory(developerData: new CoinGeckoDeveloperData
        {
            Forks = 1,
            Stars = 2,
            Subscribers = 3,
            TotalIssues = 4,
            ClosedIssues = 5,
            PullRequestsMerged = 6,
            PullRequestContributors = 7,
            CommitCount4Weeks = 8,
            CodeChurnLast4Weeks = new CoinGeckoCodeChurn { Additions = 9, Deletions = 10 },
        });

        var view = CoinDeveloperDataMapper.ToView(history);

        view.Forks.Should().Be(1);
        view.Stars.Should().Be(2);
        view.Subscribers.Should().Be(3);
        view.TotalIssues.Should().Be(4);
        view.ClosedIssues.Should().Be(5);
        view.PullRequestsMerged.Should().Be(6);
        view.PullRequestContributors.Should().Be(7);
        view.CommitCount4Weeks.Should().Be(8);
        view.CodeAdditionsLast4Weeks.Should().Be(9);
        view.CodeDeletionsLast4Weeks.Should().Be(10);
    }

    [Fact]
    public void Should_ReturnNullCodeChurnFields_When_CodeChurnLast4WeeksIsNull()
    {
        var history = CreateHistory(developerData: new CoinGeckoDeveloperData { CodeChurnLast4Weeks = null });

        var view = CoinDeveloperDataMapper.ToView(history);

        view.CodeAdditionsLast4Weeks.Should().BeNull();
        view.CodeDeletionsLast4Weeks.Should().BeNull();
    }

    [Fact]
    public void Should_MapCodeChurnFields_When_CodeChurnLast4WeeksIsPresent()
    {
        var history = CreateHistory(developerData: new CoinGeckoDeveloperData
        {
            CodeChurnLast4Weeks = new CoinGeckoCodeChurn { Additions = 100, Deletions = 50 },
        });

        var view = CoinDeveloperDataMapper.ToView(history);

        view.CodeAdditionsLast4Weeks.Should().Be(100);
        view.CodeDeletionsLast4Weeks.Should().Be(50);
    }

    [Fact]
    public void Should_ReturnNullIndividualCodeChurnField_When_ThatFieldAloneIsNullWithinAPresentChurnObject()
    {
        var history = CreateHistory(developerData: new CoinGeckoDeveloperData
        {
            CodeChurnLast4Weeks = new CoinGeckoCodeChurn { Additions = 100, Deletions = null },
        });

        var view = CoinDeveloperDataMapper.ToView(history);

        view.CodeAdditionsLast4Weeks.Should().Be(100);
        view.CodeDeletionsLast4Weeks.Should().BeNull();
    }
}

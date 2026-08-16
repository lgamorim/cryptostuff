using CryptoStuff.CoinGecko;

namespace CryptoStuff.Core;

/// <summary>Maps a <c>coins/{id}/history</c> result to a <see cref="CoinDeveloperDataView"/>.</summary>
public static class CoinDeveloperDataMapper
{
    /// <summary>
    /// Maps <paramref name="history"/> to a <see cref="CoinDeveloperDataView"/>.
    /// Every developer-activity field is null when
    /// <see cref="CoinGeckoCoinHistory.DeveloperData"/> is null.
    /// </summary>
    public static CoinDeveloperDataView ToView(CoinGeckoCoinHistory history) =>
        new()
        {
            Id = history.Id,
            Symbol = history.Symbol,
            Name = history.Name,
            Forks = history.DeveloperData?.Forks,
            Stars = history.DeveloperData?.Stars,
            Subscribers = history.DeveloperData?.Subscribers,
            TotalIssues = history.DeveloperData?.TotalIssues,
            ClosedIssues = history.DeveloperData?.ClosedIssues,
            PullRequestsMerged = history.DeveloperData?.PullRequestsMerged,
            PullRequestContributors = history.DeveloperData?.PullRequestContributors,
            CommitCount4Weeks = history.DeveloperData?.CommitCount4Weeks,
            CodeAdditionsLast4Weeks = history.DeveloperData?.CodeChurnLast4Weeks?.Additions,
            CodeDeletionsLast4Weeks = history.DeveloperData?.CodeChurnLast4Weeks?.Deletions,
        };
}

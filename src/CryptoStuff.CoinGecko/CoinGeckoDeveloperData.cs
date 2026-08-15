using System.Text.Json.Serialization;

namespace CryptoStuff.CoinGecko;

/// <summary>
/// A coin's developer repository activity. Every field is independently
/// nullable, since CoinGecko omits or nulls them when data isn't available
/// for the requested coin/date.
/// </summary>
public sealed record CoinGeckoDeveloperData
{
    /// <summary>The repository's fork count.</summary>
    [JsonPropertyName("forks")]
    public int? Forks { get; init; }

    /// <summary>The repository's star count.</summary>
    [JsonPropertyName("stars")]
    public int? Stars { get; init; }

    /// <summary>The repository's subscriber count.</summary>
    [JsonPropertyName("subscribers")]
    public int? Subscribers { get; init; }

    /// <summary>The repository's total issue count.</summary>
    [JsonPropertyName("total_issues")]
    public int? TotalIssues { get; init; }

    /// <summary>The repository's closed issue count.</summary>
    [JsonPropertyName("closed_issues")]
    public int? ClosedIssues { get; init; }

    /// <summary>The repository's merged pull request count.</summary>
    [JsonPropertyName("pull_requests_merged")]
    public int? PullRequestsMerged { get; init; }

    /// <summary>The repository's pull request contributor count.</summary>
    [JsonPropertyName("pull_request_contributors")]
    public int? PullRequestContributors { get; init; }

    /// <summary>The repository's commit count over the last 4 weeks.</summary>
    [JsonPropertyName("commit_count_4_weeks")]
    public int? CommitCount4Weeks { get; init; }

    /// <summary>The repository's code churn over the last 4 weeks. Null when unavailable.</summary>
    [JsonPropertyName("code_additions_deletions_4_weeks")]
    public CoinGeckoCodeChurn? CodeChurnLast4Weeks { get; init; }
}

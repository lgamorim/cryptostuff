namespace CryptoStuff.Core;

/// <summary>A coin's developer repository activity as of a point in time.</summary>
public sealed record CoinDeveloperDataView
{
    /// <summary>The coin's id.</summary>
    public required string Id { get; init; }

    /// <summary>The coin's ticker symbol.</summary>
    public required string Symbol { get; init; }

    /// <summary>The coin's display name.</summary>
    public required string Name { get; init; }

    /// <summary>The repository's fork count. Null when unavailable.</summary>
    public int? Forks { get; init; }

    /// <summary>The repository's star count. Null when unavailable.</summary>
    public int? Stars { get; init; }

    /// <summary>The repository's subscriber count. Null when unavailable.</summary>
    public int? Subscribers { get; init; }

    /// <summary>The repository's total issue count. Null when unavailable.</summary>
    public int? TotalIssues { get; init; }

    /// <summary>The repository's closed issue count. Null when unavailable.</summary>
    public int? ClosedIssues { get; init; }

    /// <summary>The repository's merged pull request count. Null when unavailable.</summary>
    public int? PullRequestsMerged { get; init; }

    /// <summary>The repository's pull request contributor count. Null when unavailable.</summary>
    public int? PullRequestContributors { get; init; }

    /// <summary>The repository's commit count over the last 4 weeks. Null when unavailable.</summary>
    public int? CommitCount4Weeks { get; init; }

    /// <summary>The repository's lines added over the last 4 weeks. Null when unavailable.</summary>
    public int? CodeAdditionsLast4Weeks { get; init; }

    /// <summary>The repository's lines deleted over the last 4 weeks. Null when unavailable.</summary>
    public int? CodeDeletionsLast4Weeks { get; init; }
}

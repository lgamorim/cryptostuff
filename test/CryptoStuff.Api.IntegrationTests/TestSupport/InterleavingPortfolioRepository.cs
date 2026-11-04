using CryptoStuff.Core.Portfolios;

namespace CryptoStuff.Api.IntegrationTests.TestSupport;

/// <summary>
/// The real <see cref="IPortfolioRepository"/> with a seam before its save:
/// once armed, the next <see cref="SaveChangesAsync"/> first runs a competing
/// write, then saves. The request under test has by then loaded the portfolio
/// and passed its <c>If-Match</c> check, so the competitor always commits
/// between that read and the request's own write — a lost race as a fixed
/// sequence of steps, with no threads, timing, or delays.
/// </summary>
internal sealed class InterleavingPortfolioRepository(IPortfolioRepository inner, CompetingWrite competingWrite) : IPortfolioRepository
{
    public Task<IReadOnlyList<Portfolio>> ListAsync(CancellationToken cancellationToken) => inner.ListAsync(cancellationToken);

    public Task<Portfolio?> FindAsync(int id, CancellationToken cancellationToken) => inner.FindAsync(id, cancellationToken);

    public Task<bool> NameExistsAsync(string name, int? excludingId, CancellationToken cancellationToken) =>
        inner.NameExistsAsync(name, excludingId, cancellationToken);

    public void Add(Portfolio portfolio) => inner.Add(portfolio);

    public void Remove(Portfolio portfolio) => inner.Remove(portfolio);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        if (competingWrite.TakeArmed() is { } write)
        {
            await write(cancellationToken);
        }

        await inner.SaveChangesAsync(cancellationToken);
    }
}

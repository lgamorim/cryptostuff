using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CryptoStuff.Api.IntegrationTests.TestSupport;

/// <summary>
/// Completes <see cref="Issued"/> the moment the host sends its first
/// <c>INSERT</c> to the database, before the database has run it, so a test
/// can act while that write is still waiting.
/// </summary>
internal sealed class InsertSignal : DbCommandInterceptor
{
    private readonly TaskCompletionSource _issued = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Issued => _issued.Task;

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains("INSERT", StringComparison.Ordinal))
        {
            _issued.TrySetResult();
        }

        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}

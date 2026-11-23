using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CryptoStuff.Api.IntegrationTests.TestSupport;

/// <summary>
/// Once <see cref="Armed"/>, fails every connection the host opens with an
/// <see cref="InvalidOperationException"/> whose message is
/// <see cref="Detail"/>: an exception no readiness code turns into "not
/// ready". Left unarmed until the host has started, so the start-up
/// migration runs as usual.
/// </summary>
internal sealed class FailingConnectionOpen : DbConnectionInterceptor
{
    /// <summary>The failure's message, which must never reach a response.</summary>
    public const string Detail = @"Cannot open C:\secret\cryptostuff.db";

    private volatile bool _armed;

    public bool Armed
    {
        get => _armed;
        set => _armed = value;
    }

    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
        DbConnection connection,
        ConnectionEventData eventData,
        InterceptionResult result,
        CancellationToken cancellationToken = default) =>
        _armed
            ? throw new InvalidOperationException(Detail)
            : base.ConnectionOpeningAsync(connection, eventData, result, cancellationToken);
}

namespace CryptoStuff.Core;

/// <summary>
/// Thrown by a repository's save when the database stayed locked by another
/// writer for longer than the provider waits. Nothing was written, and the
/// same save can succeed once the other writer finishes, so feature services
/// let it propagate and each host reports it as a failure worth retrying.
/// </summary>
public sealed class PersistenceBusyException : Exception
{
    /// <summary>Creates the exception with a default message.</summary>
    public PersistenceBusyException()
    {
    }

    /// <summary>Creates the exception with the given message.</summary>
    public PersistenceBusyException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with the given message, wrapping the provider failure that caused it.</summary>
    public PersistenceBusyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

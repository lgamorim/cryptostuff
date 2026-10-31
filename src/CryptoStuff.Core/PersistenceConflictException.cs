namespace CryptoStuff.Core;

/// <summary>
/// Thrown by a repository's save when the write conflicts with stored state —
/// a unique constraint was violated or a concurrent write won the race. Feature
/// services translate it to <see cref="ServiceErrorCode.Conflict"/>, so it
/// never reaches a host.
/// </summary>
public sealed class PersistenceConflictException : Exception
{
    /// <summary>Creates the exception with a default message.</summary>
    public PersistenceConflictException()
    {
    }

    /// <summary>Creates the exception with the given message.</summary>
    public PersistenceConflictException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with the given message, wrapping the provider failure that caused it.</summary>
    public PersistenceConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

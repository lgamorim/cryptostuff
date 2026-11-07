using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace CryptoStuff.Api.UnitTests.TestSupport;

/// <summary>An <see cref="ILogger{TCategoryName}"/> that records every entry, at every level.</summary>
internal sealed class SpyLogger<T> : ILogger<T>
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    public IReadOnlyList<LogEntry> Entries => [.. _entries];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        _entries.Enqueue(new LogEntry(logLevel, formatter(state, exception), exception));
}

/// <summary>One entry a <see cref="SpyLogger{T}"/> recorded.</summary>
internal sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

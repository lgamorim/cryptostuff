using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace CryptoStuff.Api.IntegrationTests.TestSupport;

/// <summary>
/// Keeps every log entry the host writes, at every level and from every
/// category, as its formatted message followed by any exception, so a test can
/// search all of them for text that must never be logged.
/// </summary>
internal sealed class CollectingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _entries = new();

    public IReadOnlyCollection<string> Entries => _entries;

    public ILogger CreateLogger(string categoryName) => new CollectingLogger(_entries);

    public void Dispose()
    {
    }

    private sealed class CollectingLogger(ConcurrentQueue<string> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            entries.Enqueue($"{formatter(state, exception)} {exception}");
    }
}

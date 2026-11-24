using Microsoft.Extensions.Logging;

namespace DawaiSehat.Reminder.Tests.Messaging;

// Keeps every formatted log line so tests can check that private data is not logged.
public sealed class CapturingLogger<T> : ILogger<T>
{
    public List<string> Lines { get; } = [];

    public string AllText => string.Join("\n", Lines);

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        Lines.Add(formatter(state, exception));
}

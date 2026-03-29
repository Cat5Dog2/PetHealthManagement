using Microsoft.Extensions.Logging;

namespace PetHealthManagement.Web.Tests.Infrastructure;

public sealed class TestLogger<T> : ILogger<T>
{
    public List<TestLogEntry> Entries { get; } = [];

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull
    {
        _ = state;
        return NullScope.Instance;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        _ = logLevel;
        return true;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var values = state as IEnumerable<KeyValuePair<string, object?>>;
        var properties = values?.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal)
            ?? new Dictionary<string, object?>(StringComparer.Ordinal);

        Entries.Add(new TestLogEntry(logLevel, eventId, formatter(state, exception), exception, properties));
    }

    public sealed record TestLogEntry(
        LogLevel LogLevel,
        EventId EventId,
        string Message,
        Exception? Exception,
        IReadOnlyDictionary<string, object?> Properties);

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}

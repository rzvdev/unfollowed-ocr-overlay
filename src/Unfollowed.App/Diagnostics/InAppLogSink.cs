using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Unfollowed.App.Diagnostics;

public sealed class InAppLogSink
{
    public event EventHandler<InAppLogEventArgs>? LogReceived;

    public void Write(string category, LogLevel level, EventId eventId, string message, Exception? exception, IReadOnlyDictionary<string, object?>? properties)
    {
        var entry = new InAppLogEntry(DateTimeOffset.Now, category, level, message, exception, properties);
        LogReceived?.Invoke(this, new InAppLogEventArgs(entry));
    }
}

public sealed record InAppLogEntry(
    DateTimeOffset Timestamp,
    string Category,
    LogLevel Level,
    string Message,
    Exception? Exception,
    IReadOnlyDictionary<string, object?>? Properties);

public sealed class InAppLogEventArgs : EventArgs
{
    public InAppLogEventArgs(InAppLogEntry entry)
    {
        Entry = entry;
    }

    public InAppLogEntry Entry { get; }
}

public sealed class InAppLoggerProvider : ILoggerProvider
{
    private readonly InAppLogSink _sink;

    public InAppLoggerProvider(InAppLogSink sink)
    {
        _sink = sink;
    }

    public ILogger CreateLogger(string categoryName) => new InAppLogger(categoryName, _sink);

    public void Dispose()
    {
    }
}

internal sealed class InAppLogger : ILogger
{
    private readonly string _category;
    private readonly InAppLogSink _sink;

    public InAppLogger(string category, InAppLogSink sink)
    {
        _category = category;
        _sink = sink;
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);
        var properties = ExtractProperties(state);
        _sink.Write(_category, logLevel, eventId, message, exception, properties);
    }

    private static IReadOnlyDictionary<string, object?>? ExtractProperties<TState>(TState state)
    {
        if (state is IEnumerable<KeyValuePair<string, object?>> pairs)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var pair in pairs)
            {
                dict[pair.Key] = pair.Value;
            }
            return dict;
        }

        return null;
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}

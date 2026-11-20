using System.Collections.Concurrent;
using Doctrina.Core.Events;
using Microsoft.Extensions.Logging;

namespace Doctrina.TestKit.Modules;

/// <summary>Event from the specs, "defined in the contracts of A".</summary>
public sealed record SampleCreated(string ExternalId) : IDomainEvent;

/// <summary>Event for which no module has a handler.</summary>
public sealed record NobodyListens(string Value) : IDomainEvent;

/// <summary>Records which handler received which event; register as a singleton in the test.</summary>
public sealed class EventRecorder
{
    private readonly ConcurrentQueue<(string Handler, IDomainEvent Event)> _received = new();

    public IReadOnlyList<(string Handler, IDomainEvent Event)> Received => [.. _received];

    public void Record(string handler, IDomainEvent domainEvent) => _received.Enqueue((handler, domainEvent));
}

/// <summary>Handler that records every <see cref="SampleCreated"/>.</summary>
public sealed class RecordingSampleCreatedHandler(EventRecorder recorder) : IEventHandler<SampleCreated>
{
    public Task HandleAsync(SampleCreated domainEvent, CancellationToken cancellationToken)
    {
        recorder.Record(nameof(RecordingSampleCreatedHandler), domainEvent);
        return Task.CompletedTask;
    }
}

/// <summary>Handler that always fails.</summary>
public sealed class FailingSampleCreatedHandler : IEventHandler<SampleCreated>
{
    public Task HandleAsync(SampleCreated domainEvent, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Opzettelijk mislukt in de test.");
}

/// <summary>Captures log lines, to check what gets logged.</summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<CapturedLog> _logs = new();

    public IReadOnlyList<CapturedLog> Logs => [.. _logs];

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _logs);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(string category, ConcurrentQueue<CapturedLog> logs) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            logs.Enqueue(new CapturedLog(category, logLevel, formatter(state, exception), exception));
        }
    }
}

/// <summary>A captured log line.</summary>
public sealed record CapturedLog(string Category, LogLevel Level, string Message, Exception? Exception);

using System.Collections.Concurrent;
using Doctrina.Core.Events;
using Microsoft.Extensions.Logging;

namespace Doctrina.TestKit.Modules;

/// <summary>Event uit de specs, "gedefinieerd in de contracts van A".</summary>
public sealed record SampleCreated(string ExternalId) : IDomainEvent;

/// <summary>Event waarvoor geen enkele module een handler heeft.</summary>
public sealed record NobodyListens(string Value) : IDomainEvent;

/// <summary>Legt vast welke handler welk event ontving; registreer als singleton in de test.</summary>
public sealed class EventRecorder
{
    private readonly ConcurrentQueue<(string Handler, IDomainEvent Event)> _received = new();

    public IReadOnlyList<(string Handler, IDomainEvent Event)> Received => [.. _received];

    public void Record(string handler, IDomainEvent domainEvent) => _received.Enqueue((handler, domainEvent));
}

/// <summary>Handler die elk <see cref="SampleCreated"/> vastlegt.</summary>
public sealed class RecordingSampleCreatedHandler(EventRecorder recorder) : IEventHandler<SampleCreated>
{
    public Task HandleAsync(SampleCreated domainEvent, CancellationToken cancellationToken)
    {
        recorder.Record(nameof(RecordingSampleCreatedHandler), domainEvent);
        return Task.CompletedTask;
    }
}

/// <summary>Handler die altijd faalt.</summary>
public sealed class FailingSampleCreatedHandler : IEventHandler<SampleCreated>
{
    public Task HandleAsync(SampleCreated domainEvent, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Opzettelijk mislukt in de test.");
}

/// <summary>Vangt logregels op, om te controleren wat er gelogd wordt.</summary>
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

/// <summary>Een opgevangen logregel.</summary>
public sealed record CapturedLog(string Category, LogLevel Level, string Message, Exception? Exception);

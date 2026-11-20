namespace Doctrina.Core.Events;

/// <summary>Marks a domain event; events live in the <c>.Contracts</c> project of the publishing module.</summary>
public interface IDomainEvent;

/// <summary>
/// In-process event bus between modules (design D6). Events are delivered to all handlers of enabled
/// modules; a failing handler is logged and does not affect the publishing module or other handlers.
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Delivers <paramref name="domainEvent"/> immediately to the handlers of the enabled modules. If the event belongs to
    /// a data change, use <c>Entity.RaiseEvent</c> instead: delivery then follows only after the change has been
    /// saved, and not if saving fails.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="domainEvent">The event.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken)
        where TEvent : IDomainEvent;
}

/// <summary>Handles a domain event; registered per module in the DI container.</summary>
/// <typeparam name="TEvent">The event type.</typeparam>
public interface IEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    /// <summary>Processes the event.</summary>
    /// <param name="domainEvent">The event.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}

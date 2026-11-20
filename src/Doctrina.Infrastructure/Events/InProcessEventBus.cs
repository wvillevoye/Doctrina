using System.Collections.Concurrent;
using Doctrina.Core.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Doctrina.Infrastructure.Events;

/// <summary>
/// In-process event bus (design D6): delivers an event one after another to all registered handlers in the same
/// DI scope. Only enabled modules register handlers, so handlers of disabled modules are never
/// called. A failing handler is logged (event type + handler, no content) and does not affect the rest.
/// </summary>
public sealed partial class InProcessEventBus(IServiceProvider services, ILogger<InProcessEventBus> logger) : IEventBus
{
    private static readonly ConcurrentDictionary<Type, Func<InProcessEventBus, IDomainEvent, CancellationToken, Task>> Dispatchers = new();

    public async Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken)
        where TEvent : IDomainEvent
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        foreach (var handler in services.GetServices<IEventHandler<TEvent>>())
        {
            try
            {
                await handler.HandleAsync(domainEvent, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (!(exception is OperationCanceledException && cancellationToken.IsCancellationRequested))
            {
                LogHandlerFailed(logger, exception, handler.GetType().Name, typeof(TEvent).Name);
            }
        }
    }

    /// <summary>Delivers an event based on its actual type (for events coming from entities).</summary>
    public Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        var dispatch = Dispatchers.GetOrAdd(domainEvent.GetType(), type =>
        {
            var method = typeof(InProcessEventBus).GetMethod(nameof(PublishTyped), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .MakeGenericMethod(type);
            return method.CreateDelegate<Func<InProcessEventBus, IDomainEvent, CancellationToken, Task>>();
        });
        return dispatch(this, domainEvent, cancellationToken);
    }

    private static Task PublishTyped<TEvent>(InProcessEventBus bus, IDomainEvent domainEvent, CancellationToken cancellationToken)
        where TEvent : IDomainEvent =>
        bus.PublishAsync((TEvent)domainEvent, cancellationToken);

    [LoggerMessage(Level = LogLevel.Error, Message = "Eventhandler {Handler} voor event {EventType} is mislukt; de overige handlers worden nog aangeroepen.")]
    private static partial void LogHandlerFailed(ILogger logger, Exception exception, string handler, string eventType);
}

/// <summary>Registration of the event bus and event handlers.</summary>
public static class EventServiceCollectionExtensions
{
    /// <summary>Registers the in-process event bus (per scope).</summary>
    public static IServiceCollection AddDoctrinaEvents(this IServiceCollection services)
    {
        services.TryAddScoped<InProcessEventBus>();
        services.TryAddScoped<IEventBus>(provider => provider.GetRequiredService<InProcessEventBus>());
        return services;
    }

    /// <summary>
    /// Registers <typeparamref name="THandler"/> for <typeparamref name="TEvent"/>. Call from
    /// <c>IModule.ConfigureServices</c>, so only enabled modules receive events.
    /// </summary>
    public static IServiceCollection AddEventHandler<TEvent, THandler>(this IServiceCollection services)
        where TEvent : IDomainEvent
        where THandler : class, IEventHandler<TEvent>
    {
        services.AddDoctrinaEvents();
        return services.AddScoped<IEventHandler<TEvent>, THandler>();
    }
}

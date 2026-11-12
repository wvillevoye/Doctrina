using System.Collections.Concurrent;
using Doctrina.Core.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Doctrina.Infrastructure.Events;

/// <summary>
/// In-process eventbus (design D6): levert een event na elkaar af aan alle geregistreerde handlers in dezelfde
/// DI-scope. Alleen ingeschakelde modules registreren handlers, dus handlers van uitgeschakelde modules worden nooit
/// aangeroepen. Een falende handler wordt gelogd (eventtype + handler, geen inhoud) en raakt de rest niet.
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

    /// <summary>Levert een event af op basis van zijn werkelijke type (voor events uit entiteiten).</summary>
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

/// <summary>Registratie van de eventbus en eventhandlers.</summary>
public static class EventServiceCollectionExtensions
{
    /// <summary>Registreert de in-process eventbus (per scope).</summary>
    public static IServiceCollection AddDoctrinaEvents(this IServiceCollection services)
    {
        services.TryAddScoped<InProcessEventBus>();
        services.TryAddScoped<IEventBus>(provider => provider.GetRequiredService<InProcessEventBus>());
        return services;
    }

    /// <summary>
    /// Registreert <typeparamref name="THandler"/> voor <typeparamref name="TEvent"/>. Roep aan vanuit
    /// <c>IModule.ConfigureServices</c>, zodat alleen ingeschakelde modules events ontvangen.
    /// </summary>
    public static IServiceCollection AddEventHandler<TEvent, THandler>(this IServiceCollection services)
        where TEvent : IDomainEvent
        where THandler : class, IEventHandler<TEvent>
    {
        services.AddDoctrinaEvents();
        return services.AddScoped<IEventHandler<TEvent>, THandler>();
    }
}

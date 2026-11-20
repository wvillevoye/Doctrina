using Doctrina.Core.Entities;
using Doctrina.Infrastructure.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Doctrina.Infrastructure.Persistence;

/// <summary>
/// Delivers the events of entities after a successful <c>SaveChanges</c> (design D6). If saving fails,
/// nothing is delivered and the events stay on the entities for a next attempt.
/// </summary>
/// <remarks>
/// Delivery happens after <c>SaveChanges</c>; do not use an explicit transaction spanning several <c>SaveChanges</c> calls
/// if handlers may only react after the commit. Guaranteed delivery (outbox) follows when a module needs
/// it (see the risks in design D6).
/// </remarks>
public sealed class DomainEventsInterceptor(InProcessEventBus bus) : SaveChangesInterceptor
{
    private readonly Dictionary<DbContext, List<Entity>> _pending = [];

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Remember(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Remember(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        DispatchAsync(eventData.Context, CancellationToken.None).GetAwaiter().GetResult();
        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        await DispatchAsync(eventData.Context, cancellationToken).ConfigureAwait(false);
        return result;
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Forget(eventData.Context);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Forget(eventData.Context);
        return Task.CompletedTask;
    }

    public override void SaveChangesCanceled(DbContextEventData eventData)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Forget(eventData.Context);
    }

    public override Task SaveChangesCanceledAsync(DbContextEventData eventData, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Forget(eventData.Context);
        return Task.CompletedTask;
    }

    // Hold on to the entities before saving: deleted entities are no longer in the change tracker afterwards.
    private void Remember(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var entities = context.ChangeTracker.Entries<Entity>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();
        if (entities.Count > 0)
        {
            _pending[context] = entities;
        }
    }

    private void Forget(DbContext? context)
    {
        if (context is not null)
        {
            _pending.Remove(context);
        }
    }

    private async Task DispatchAsync(DbContext? context, CancellationToken cancellationToken)
    {
        if (context is null || !_pending.Remove(context, out var entities))
        {
            return;
        }

        foreach (var domainEvent in entities.SelectMany(e => e.DequeueDomainEvents()))
        {
            await bus.PublishAsync(domainEvent, cancellationToken).ConfigureAwait(false);
        }
    }
}

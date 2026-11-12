using Doctrina.Core.Entities;
using Doctrina.Infrastructure.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Doctrina.Infrastructure.Persistence;

/// <summary>
/// Levert de events van entiteiten af ná een geslaagde <c>SaveChanges</c> (design D6). Mislukt het opslaan, dan wordt
/// niets afgeleverd en blijven de events op de entiteiten staan voor een volgende poging.
/// </summary>
/// <remarks>
/// Aflevering gebeurt na <c>SaveChanges</c>; gebruik geen expliciete transactie om meerdere <c>SaveChanges</c>-aanroepen
/// heen als handlers pas na de commit mogen reageren. Gegarandeerde aflevering (outbox) volgt wanneer een module die
/// nodig heeft (zie de risico's in design D6).
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

    // Entiteiten vóór het opslaan vasthouden: verwijderde entiteiten zijn daarna niet meer in de change tracker.
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

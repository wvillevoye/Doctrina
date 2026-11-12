using Doctrina.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Doctrina.Infrastructure.Persistence;

/// <summary>Zet <see cref="ExchangeableEntity.DateLastModified"/> (UTC) bij elke invoeging en wijziging (design D4/D5).</summary>
public sealed class DateLastModifiedInterceptor(TimeProvider time) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Stamp(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Stamp(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = time.GetUtcNow();
        foreach (var entry in context.ChangeTracker.Entries<ExchangeableEntity>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            entry.Property(e => e.DateLastModified).CurrentValue = now;
        }
    }
}

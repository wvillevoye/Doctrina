using Doctrina.Core.Events;

namespace Doctrina.Core.Entities;

/// <summary>
/// Base type for entities. <see cref="Id"/> is a <c>uniqueidentifier</c> that EF Core generates sequentially on insert
/// (design D4) — never fill it yourself with <see cref="Guid.NewGuid"/> or
/// <see cref="Guid.CreateVersion7()"/>.
/// </summary>
public abstract class Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>Internal key; <see cref="Guid.Empty"/> until the entity has been saved.</summary>
    public Guid Id { get; private set; }

    /// <summary>Events belonging to the current change that have not been delivered yet (not a database column).</summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    /// <summary>Returns the pending events and clears the list; for the persistence layer only.</summary>
    public IReadOnlyList<IDomainEvent> DequeueDomainEvents()
    {
        var events = _domainEvents.ToList();
        _domainEvents.Clear();
        return events;
    }

    /// <summary>
    /// Raises an event that is only delivered after the change has been saved; if saving fails, it is not delivered
    /// (design D6).
    /// </summary>
    /// <param name="domainEvent">The event.</param>
    protected void RaiseEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }
}

/// <summary>Status of an exchangeable record (OneRoster/OOAPI: <c>active</c> / <c>tobedeleted</c>).</summary>
public enum EntityStatus
{
    /// <summary>The record is active.</summary>
    Active,

    /// <summary>The source system has deleted the record; it is still exchanged for delta sync.</summary>
    ToBeDeleted,
}

/// <summary>
/// Base type for entities that are exchanged with other systems (proposal §4.1, design D4).
/// <see cref="ExternalId"/> is unique per <see cref="Source"/>; <see cref="DateLastModified"/> is maintained
/// automatically.
/// </summary>
public abstract class ExchangeableEntity : Entity
{
    /// <summary>Maximum length of <see cref="ExternalId"/> (<c>nvarchar(255)</c>).</summary>
    public const int ExternalIdMaxLength = 255;

    /// <summary>Maximum length of <see cref="Source"/> (<c>nvarchar(64)</c>).</summary>
    public const int SourceMaxLength = 64;

    /// <summary>Stable key from the source system; the basis for linking and upserts.</summary>
    public required string ExternalId { get; set; }

    /// <summary>System that owns this record, e.g. <c>SIS</c> or <c>Manual</c>.</summary>
    public required string Source { get; set; }

    /// <summary>Status of the record.</summary>
    public EntityStatus Status { get; set; } = EntityStatus.Active;

    /// <summary>Moment of the last change (UTC); set automatically on save.</summary>
    public DateTimeOffset DateLastModified { get; private set; }

    /// <summary>Sets <see cref="DateLastModified"/>; for the save interceptor only.</summary>
    /// <param name="utcNow">The current moment in UTC.</param>
    public void MarkModified(DateTimeOffset utcNow) => DateLastModified = utcNow.ToUniversalTime();
}

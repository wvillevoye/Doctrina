using Doctrina.Core.Events;

namespace Doctrina.Core.Entities;

/// <summary>
/// Basistype voor entiteiten. <see cref="Id"/> is een <c>uniqueidentifier</c> die EF Core bij het invoegen
/// sequentieel genereert (design D4) — vul hem nooit zelf met <see cref="Guid.NewGuid"/> of
/// <see cref="Guid.CreateVersion7()"/>.
/// </summary>
public abstract class Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>Interne sleutel; <see cref="Guid.Empty"/> tot de entiteit is opgeslagen.</summary>
    public Guid Id { get; private set; }

    /// <summary>Events die bij de huidige wijziging horen en nog niet zijn afgeleverd (geen kolom in de database).</summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    /// <summary>Haalt de openstaande events op en leegt de lijst; alleen voor de opslaglaag.</summary>
    public IReadOnlyList<IDomainEvent> DequeueDomainEvents()
    {
        var events = _domainEvents.ToList();
        _domainEvents.Clear();
        return events;
    }

    /// <summary>
    /// Laat een event ontstaan dat pas wordt afgeleverd nadat de wijziging is opgeslagen; mislukt het opslaan, dan wordt
    /// het niet afgeleverd (design D6).
    /// </summary>
    /// <param name="domainEvent">Het event.</param>
    protected void RaiseEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }
}

/// <summary>Status van een uitwisselbaar record (OneRoster/OOAPI: <c>active</c> / <c>tobedeleted</c>).</summary>
public enum EntityStatus
{
    /// <summary>Het record is actief.</summary>
    Active,

    /// <summary>Het bronsysteem heeft het record verwijderd; het wordt nog uitgewisseld voor delta-sync.</summary>
    ToBeDeleted,
}

/// <summary>
/// Basistype voor entiteiten die met andere systemen worden uitgewisseld (proposal §4.1, design D4).
/// <see cref="ExternalId"/> is uniek per <see cref="Source"/>; <see cref="DateLastModified"/> wordt automatisch
/// bijgehouden.
/// </summary>
public abstract class ExchangeableEntity : Entity
{
    /// <summary>Maximale lengte van <see cref="ExternalId"/> (<c>nvarchar(255)</c>).</summary>
    public const int ExternalIdMaxLength = 255;

    /// <summary>Maximale lengte van <see cref="Source"/> (<c>nvarchar(64)</c>).</summary>
    public const int SourceMaxLength = 64;

    /// <summary>Stabiele sleutel uit het bronsysteem; basis voor koppelen en upserts.</summary>
    public required string ExternalId { get; set; }

    /// <summary>Systeem dat eigenaar is van dit record, bv. <c>SIS</c> of <c>Manual</c>.</summary>
    public required string Source { get; set; }

    /// <summary>Status van het record.</summary>
    public EntityStatus Status { get; set; } = EntityStatus.Active;

    /// <summary>Moment van de laatste wijziging (UTC); wordt bij opslaan automatisch gezet.</summary>
    public DateTimeOffset DateLastModified { get; private set; }

    /// <summary>Zet <see cref="DateLastModified"/>; alleen voor de opslag-interceptor.</summary>
    /// <param name="utcNow">Huidig moment in UTC.</param>
    public void MarkModified(DateTimeOffset utcNow) => DateLastModified = utcNow.ToUniversalTime();
}

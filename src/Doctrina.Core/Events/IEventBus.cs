namespace Doctrina.Core.Events;

/// <summary>Markeert een domeinevent; events staan in het <c>.Contracts</c>-project van de publicerende module.</summary>
public interface IDomainEvent;

/// <summary>
/// In-process eventbus tussen modules (design D6). Events worden afgeleverd aan alle handlers van ingeschakelde
/// modules; een falende handler wordt gelogd en raakt de publicerende module of andere handlers niet.
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Levert <paramref name="domainEvent"/> direct af aan de handlers van de ingeschakelde modules. Hoort het event bij
    /// een gegevenswijziging, gebruik dan <c>Entity.RaiseEvent</c>: dan volgt de aflevering pas nadat de wijziging is
    /// opgeslagen, en niet als het opslaan mislukt.
    /// </summary>
    /// <typeparam name="TEvent">Het eventtype.</typeparam>
    /// <param name="domainEvent">Het event.</param>
    /// <param name="cancellationToken">Annulering.</param>
    Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken)
        where TEvent : IDomainEvent;
}

/// <summary>Handelt een domeinevent af; wordt per module in de DI-container geregistreerd.</summary>
/// <typeparam name="TEvent">Het eventtype.</typeparam>
public interface IEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    /// <summary>Verwerkt het event.</summary>
    /// <param name="domainEvent">Het event.</param>
    /// <param name="cancellationToken">Annulering.</param>
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}

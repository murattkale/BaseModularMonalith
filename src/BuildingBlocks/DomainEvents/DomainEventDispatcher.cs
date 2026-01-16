using MediatR;
using SharedKernel;

namespace BuildingBlocks.DomainEvents;

/// <summary>
/// Domain event'leri dispatch etmek için arayüz.
/// Event'ler transaction içinde, SaveChanges sonrası dispatch edilir.
/// </summary>
public interface IDomainEventDispatcher
{
    /// <summary>
    /// Verilen entity'lerdeki tüm domain event'leri dispatch eder.
    /// </summary>
    Task DispatchEventsAsync(IEnumerable<Entity> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tek bir domain event'i dispatch eder.
    /// </summary>
    Task DispatchEventAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
}

/// <summary>
/// MediatR tabanlı domain event dispatcher implementasyonu.
/// </summary>
public sealed class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IPublisher _publisher;

    /// <summary>
    /// DomainEventDispatcher sınıfı için yeni bir örnek oluşturur.
    /// </summary>
    /// <param name="publisher">Event'leri yayınlamak için MediatR yayıncısı (publisher).</param>
    public DomainEventDispatcher(IPublisher publisher)
    {
        _publisher = publisher;
    }

    /// <summary>
    /// Verilen varlık koleksiyonundaki tüm domain olaylarını (event) yayınlar.
    /// </summary>
    public async Task DispatchEventsAsync(IEnumerable<Entity> entities, CancellationToken cancellationToken = default)
    {
        foreach (var entity in entities)
        {
            var events = entity.DomainEvents;
            if (events.Count == 0) continue;

            // Kopyalama zorunlu çünkü Clear işlemi listeyi etkiler. 
            // Aggressive performance: ToArray() daha az memory allocation yapar.
            var eventsToDispatch = events.ToArray();

            foreach (var domainEvent in eventsToDispatch)
            {
                await _publisher.Publish(domainEvent, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Tek bir domain olayını yayınlar.
    /// </summary>
    public async Task DispatchEventAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        await _publisher.Publish(domainEvent, cancellationToken);
    }
}

namespace SharedKernel;

/// <summary>
/// Domain event'ler için temel record sınıfı.
/// Immutability için record olarak tanımlanmıştır.
/// </summary>
public abstract record DomainEventBase : IDomainEvent
{
    /// <summary>
    /// Event'in oluşturulma zamanı (UTC).
    /// </summary>
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

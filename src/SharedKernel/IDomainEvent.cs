using MediatR;

namespace SharedKernel;

/// <summary>
/// Domain event'ler için işaret arayüzü.
/// Event'ler güçlü tutarlılık için yalnızca in-memory dispatch edilir.
/// </summary>
public interface IDomainEvent : INotification
{
    /// <summary>
    /// Event'in oluşturulma zamanı (UTC).
    /// </summary>
    DateTime OccurredAt { get; }
}

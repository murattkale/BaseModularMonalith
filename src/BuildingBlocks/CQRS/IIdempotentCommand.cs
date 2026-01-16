using SharedKernel;

namespace BuildingBlocks.CQRS;

/// <summary>
/// Tekrar önleme (Idempotency) gerektiren command'lar için arayüz.
/// </summary>
public interface IIdempotentCommand
{
    /// <summary>
    /// Her bir istek için benzersiz olan talep ID'si.
    /// </summary>
    Guid RequestId { get; }
}

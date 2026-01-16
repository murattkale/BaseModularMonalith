using SharedKernel;

namespace BuildingBlocks.Behaviors;

/// <summary>
/// Idempotency kontrolü yapan servis arayüzü.
/// </summary>
public interface IIdempotencyService
{
    /// <summary>
    /// Belirtilen istek ID'sinin daha önce işlenip işlenmediğini kontrol eder.
    /// </summary>
    ValueTask<bool> ExistsAsync(Guid requestId, CancellationToken ct = default);

    /// <summary>
    /// Yeni bir idempotent istek kaydı oluşturur.
    /// </summary>
    ValueTask CreateAsync(Guid requestId, string name, CancellationToken ct = default);
}

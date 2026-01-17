namespace SharedKernel;

/// <summary>
/// Transaction yönetimi için Unit of Work arayüzü.
/// Bir command = bir transaction garantisi sağlar.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Bekleyen değişiklikleri veritabanına kaydeder.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Yeni bir transaction başlatır.
    /// </summary>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Mevcut transaction'ı onaylar.
    /// </summary>
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Veritabanı execution strategy'sini (retry logic) kullanarak işlem yürütür.
    /// </summary>
    Task<T> ExecuteStrategyAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default);

    /// <summary>
    /// Değişiklikleri geri alır (Rollback).
    /// </summary>
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}

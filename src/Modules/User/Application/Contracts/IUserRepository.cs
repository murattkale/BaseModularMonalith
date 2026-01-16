using User.Domain.Entities;

namespace User.Application.Contracts;

/// <summary>
/// User repository interface (write operations - EF Core).
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// ID'ye göre user getirir (tracking aktif).
    /// </summary>
    ValueTask<UserEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Email'e göre user getirir (login için).
    /// </summary>
    ValueTask<UserEntity?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Email mevcut mu kontrol eder.
    /// </summary>
    ValueTask<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Yeni user ekler.
    /// </summary>
    ValueTask<UserEntity> AddAsync(UserEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// User'ı günceller.
    /// </summary>
    void Update(UserEntity entity);

    /// <summary>
    /// User'ı soft delete yapar.
    /// </summary>
    void Delete(UserEntity entity);
}

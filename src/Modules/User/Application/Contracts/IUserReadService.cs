namespace User.Application.Contracts;

/// <summary>
/// Kullanıcı okuma servis arayüzü (Okuma işlemleri - Dapper kullanarak yüksek performans sağlar).
/// </summary>
public interface IUserReadService
{
    /// <summary>
    /// ID'ye göre user getirir.
    /// </summary>
    ValueTask<Queries.UserDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Email'e göre user getirir.
    /// </summary>
    ValueTask<Queries.UserDto?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sayfalanmış user listesi getirir.
    /// </summary>
    ValueTask<IReadOnlyList<Queries.UserListItemDto>> GetAllAsync(
        int page,
        int pageSize,
        bool? isActive = null,
        DateTime? afterCreatedAt = null,
        Guid? afterId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Toplam user sayısı.
    /// </summary>
    ValueTask<int> GetCountAsync(bool? isActive = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Email mevcut mu (hızlı check).
    /// </summary>
    ValueTask<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);
}

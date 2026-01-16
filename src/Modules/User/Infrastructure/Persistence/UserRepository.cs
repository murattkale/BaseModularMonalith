using System.Runtime.CompilerServices;
using User.Application.Contracts;
using User.Domain.Entities;

namespace User.Infrastructure.Persistence;

/// <summary>
/// Kullanıcı veri erişim katmanı - Derlenmiş sorgular (compiled queries) ile EF Core yazma işlemlerini gerçekleştirir.
/// </summary>
public sealed class UserRepository : IUserRepository
{
    private readonly UserDbContext _context;

    /// <summary>
    /// UserRepository sınıfı için yeni bir örnek oluşturur.
    /// </summary>
    /// <param name="context">Veritabanı bağlamı (context).</param>
    public UserRepository(UserDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// ID değerine göre kullanıcıyı getirir. Dayanıklılık politikaları (resilience) devrededir.
    /// </summary>
    public async ValueTask<UserEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await BuildingBlocks.Resilience.ResiliencePipelines.DbPipeline.ExecuteAsync(
            async ct => await _context.GetByIdAsync(id, ct), 
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// E-posta adresine göre kullanıcıyı getirir.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async ValueTask<UserEntity?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await BuildingBlocks.Resilience.ResiliencePipelines.DbPipeline.ExecuteAsync(
            async ct => await _context.GetByEmailAsync(email, ct), 
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// E-posta adresinin kullanımda olup olmadığını kontrol eder.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async ValueTask<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        return await BuildingBlocks.Resilience.ResiliencePipelines.DbPipeline.ExecuteAsync(
            async ct => await _context.EmailExistsAsync(email, ct), 
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Yeni bir kullanıcıyı asenkron olarak ekler.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<UserEntity> AddAsync(UserEntity entity, CancellationToken cancellationToken = default)
    {
        // GUID'ler bizde üretildiği için senkron Add metodu daha hızlıdır (Task overhead'i yok)
        _context.Users.Add(entity);
        return ValueTask.FromResult(entity);
    }

    /// <summary>
    /// Kullanıcı bilgilerini günceller.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update(UserEntity entity)
    {
        _context.Users.Update(entity);
    }

    /// <summary>
    /// Kullanıcıyı silinmiş olarak işaretler (Soft delete).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Delete(UserEntity entity)
    {
        entity.MarkAsDeleted();
        _context.Users.Update(entity);
    }
}

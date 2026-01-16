using System.Runtime.CompilerServices;
using SharedKernel;
using User.Domain.Events;
using User.Domain.ValueObjects;

namespace User.Domain.Entities;

/// <summary>
/// Kullanıcı Aggreggate Root - Yüksek performanslı, değişmez değer nesneleri kullanır.
/// </summary>
public sealed class UserEntity : Entity
{
    // EF Core için
    private UserEntity() { }

    private UserEntity(
        Guid id,
        Email email,
        HashedPassword passwordHash,
        string firstName,
        string lastName)
    {
        Id = id;
        Email = email;
        PasswordHash = passwordHash;
        FirstName = firstName;
        LastName = lastName;
        Roles = UserRoles.User; // Varsayılan rol
        IsActive = true;
        LastLoginAt = null;
    }

    /// <summary>
    /// Bitwise Roller (Join gerektirmez, O(1) kontrol).
    /// </summary>
    public UserRoles Roles { get; private set; }

    /// <summary>
    /// Email (unique, indexed).
    /// </summary>
    public Email Email { get; private set; }

    /// <summary>
    /// Hashed password.
    /// </summary>
    public HashedPassword PasswordHash { get; private set; }

    /// <summary>
    /// Ad.
    /// </summary>
    public string FirstName { get; private set; } = string.Empty;

    /// <summary>
    /// Soyad.
    /// </summary>
    public string LastName { get; private set; } = string.Empty;

    /// <summary>
    /// Aktif mi?
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Son giriş zamanı.
    /// </summary>
    public DateTime? LastLoginAt { get; private set; }

    /// <summary>
    /// Veritabanı çakışma koruması (Concurrency token) için RowVersion.
    /// </summary>
    public byte[] RowVersion { get; private set; } = [];

    /// <summary>
    /// Tam ad (hesaplanmış, cache'lendiğinde ek bellek ayırmaz).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string GetFullName() => string.Concat(FirstName, " ", LastName);

    /// <summary>
    /// Factory method - yeni user oluşturur.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UserEntity Create(
        string email,
        string password,
        string firstName,
        string lastName)
    {
        var emailVO = Email.Create(email);
        var passwordHash = HashedPassword.Create(password.AsSpan());

        var entity = new UserEntity(
            Guid.CreateVersion7(),
            emailVO,
            passwordHash,
            firstName.Trim(),
            lastName.Trim());

        entity.AddDomainEvent(new UserCreatedEvent(entity.Id, entity.Email.Value));
        return entity;
    }

    /// <summary>
    /// Factory method (Async) - yeni user oluşturur (CPU offloading için).
    /// </summary>
    public static async Task<UserEntity> CreateAsync(
        string email,
        string password,
        string firstName,
        string lastName,
        CancellationToken ct = default)
    {
        var emailVO = Email.Create(email);
        var passwordHash = await HashedPassword.CreateAsync(password, ct);

        var entity = new UserEntity(
            Guid.CreateVersion7(),
            emailVO,
            passwordHash,
            firstName.Trim(),
            lastName.Trim());

        entity.AddDomainEvent(new UserCreatedEvent(entity.Id, entity.Email.Value));
        return entity;
    }

    /// <summary>
    /// Password doğrular.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool VerifyPassword(ReadOnlySpan<char> password)
    {
        return PasswordHash.Verify(password);
    }

    /// <summary>
    /// Password doğrular (Async) - CPU offloading için.
    /// </summary>
    public ValueTask<bool> VerifyPasswordAsync(string password, CancellationToken ct = default)
    {
        return PasswordHash.VerifyAsync(password, ct);
    }

    /// <summary>
    /// Login kaydeder.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordLogin()
    {
        LastLoginAt = DateTime.UtcNow;
        AddDomainEvent(new UserLoggedInEvent(Id, Email.Value));
    }

    /// <summary>
    /// Password değiştirir.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ChangePassword(ReadOnlySpan<char> newPassword)
    {
        PasswordHash = HashedPassword.Create(newPassword);
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new UserPasswordChangedEvent(Id));
    }

    /// <summary>
    /// Profil günceller.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UpdateProfile(string firstName, string lastName)
    {
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new UserProfileUpdatedEvent(Id));
    }

    /// <summary>
    /// Deaktif eder.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deactivate()
    {
        if (!IsActive) return;
        
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new UserDeactivatedEvent(Id));
    }

    /// <summary>
    /// Aktif eder.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Activate()
    {
        if (IsActive) return;
        
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new UserActivatedEvent(Id));
    }

    /// <summary>
    /// Rol ekler.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddRole(UserRoles role)
    {
        Roles |= role;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Rol kaldırır.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RemoveRole(UserRoles role)
    {
        Roles &= ~role;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Role sahip mi?
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasRole(UserRoles role) => (Roles & role) == role;

    /// <summary>
    /// Soft delete override.
    /// </summary>
    public override void MarkAsDeleted()
    {
        DeletedAt = DateTime.UtcNow;
        IsActive = false;
        AddDomainEvent(new UserDeletedEvent(Id, Email.Value));
    }
}

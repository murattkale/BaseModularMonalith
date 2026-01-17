using System.Runtime.CompilerServices;
using BuildingBlocks.DomainEvents;
using Microsoft.EntityFrameworkCore;
using SharedKernel;
using User.Domain.Entities;
using User.Domain.ValueObjects;

namespace User.Infrastructure.Persistence;

/// <summary>
/// Kullanıcı Veritabanı Bağlamı (Context) - Derlenmiş sorgular (compiled queries) ve optimize edilmiş yapılandırma içerir.
/// </summary>
public sealed class UserDbContext : DbContext, IUnitOfWork
{
    // Compiled queries - EF Core query compilation overhead'ını ortadan kaldırır
    private static readonly Func<UserDbContext, Guid, CancellationToken, Task<UserEntity?>> GetByIdCompiledQuery =
        EF.CompileAsyncQuery((UserDbContext ctx, Guid id, CancellationToken ct) =>
            ctx.Users.FirstOrDefault(u => u.Id == id));

    private static readonly Func<UserDbContext, Email, CancellationToken, Task<UserEntity?>> GetByEmailCompiledQuery =
        EF.CompileAsyncQuery((UserDbContext ctx, Email email, CancellationToken ct) =>
            ctx.Users.FirstOrDefault(u => u.Email == email));

    private static readonly Func<UserDbContext, Guid, CancellationToken, Task<bool>> RequestIdExistsQuery =
        EF.CompileAsyncQuery((UserDbContext ctx, Guid requestId, CancellationToken ct) =>
            ctx.IdempotentRequests.Any(x => x.Id == requestId));

    private static readonly Func<UserDbContext, Email, CancellationToken, Task<bool>> EmailExistsCompiledQuery =
        EF.CompileAsyncQuery((UserDbContext ctx, Email email, CancellationToken ct) =>
            ctx.Users.Any(u => u.Email == email));

    /// <summary>
    /// UserDbContext sınıfı için yeni bir örnek oluşturur.
    /// Performance: Değişiklik takibi (Change Tracker) ve sorgu takibi (Query Tracking) kapatılarak performans artırılır.
    /// </summary>
    /// <param name="options">Bağlam yapılandırma seçenekleri.</param>
    public UserDbContext(DbContextOptions<UserDbContext> options) : base(options)
    {
        ChangeTracker.AutoDetectChangesEnabled = false;
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<IdempotentRequest> IdempotentRequests => Set<IdempotentRequest>();

    public Task<bool> RequestIdExistsAsync(Guid id, CancellationToken ct) => 
        RequestIdExistsQuery(this, id, ct);

    /// <summary>
    /// Compiled query ile ID'ye göre user getirir.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<UserEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return GetByIdCompiledQuery(this, id, cancellationToken);
    }

    /// <summary>
    /// Compiled query ile email'e göre user getirir.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<UserEntity?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return GetByEmailCompiledQuery(this, Email.Create(email), cancellationToken);
    }

    /// <summary>
    /// Compiled query ile email mevcut mu kontrol eder.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        return EmailExistsCompiledQuery(this, Email.Create(email), cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .ValueGeneratedNever();

            // Email - Value Object conversion
            entity.Property(e => e.Email)
                .HasConversion(
                    v => v.Value,
                    v => Email.Create(v))
                .HasMaxLength(254)
                .IsRequired();

            // Unique index on Email (case-insensitive)
            entity.HasIndex(e => e.Email)
                .IsUnique()
                .HasDatabaseName("IX_Users_Email");

            // Password hash - JSON serialization for struct
            entity.Property(e => e.PasswordHash)
                .HasConversion(
                    v => Convert.ToBase64String(v.Hash) + ":" + Convert.ToBase64String(v.Salt),
                    v => ParsePasswordHash(v))
                .HasColumnName("PasswordHash")
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(e => e.FirstName)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.LastName)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.Roles)
                .HasColumnType("bigint")
                .IsRequired()
                .HasDefaultValue(UserRoles.User);

            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                .IsRequired();

            entity.Property(e => e.UpdatedAt);
            entity.Property(e => e.DeletedAt);
            entity.Property(e => e.LastLoginAt);

            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();

            entity.HasIndex(e => e.RowVersion)
                .HasDatabaseName("IX_Users_RowVersion");

            entity.Ignore(e => e.DomainEvents);
            entity.Ignore(e => e.IsDeleted);

            // Global query filter - soft delete
            entity.HasQueryFilter(e => e.DeletedAt == null);

            // Indexes for common queries
            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("IX_Users_CreatedAt");

            entity.HasIndex(e => e.IsActive)
                .HasDatabaseName("IX_Users_IsActive")
                .HasFilter("[DeletedAt] IS NULL");

            entity.HasIndex(e => e.LastLoginAt)
                .HasDatabaseName("IX_Users_LastLoginAt")
                .HasFilter("[DeletedAt] IS NULL");

            // Composite covering index for deterministic keyset pagination
            entity.HasIndex(e => new { e.IsActive, e.CreatedAt, e.Id })
                .HasDatabaseName("IX_Users_IsActive_CreatedAt_Id")
                .HasFilter("[DeletedAt] IS NULL")
                .IsDescending(false, true, true)
                .IncludeProperties(e => new { e.Email, e.FirstName, e.LastName, e.Roles, e.LastLoginAt });
        });

        // OutboxMessages Index - processing efficiency
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("OutboxMessages");
            entity.HasKey(e => e.Id);
            
            entity.HasIndex(e => new { e.ProcessedAtUtc, e.CreatedAtUtc })
                .HasDatabaseName("IX_OutboxMessages_Processing")
                .HasFilter("[ProcessedAtUtc] IS NULL");
        });

        // IdempotentRequests Index - pruning/TTL support
        modelBuilder.Entity<IdempotentRequest>(entity =>
        {
            entity.ToTable("IdempotentRequests");
            entity.HasKey(e => e.Id);
            
            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("IX_IdempotentRequests_CreatedAt");
        });
    }

    private static HashedPassword ParsePasswordHash(string value)
    {
        // Zero-allocation parsing via Span
        ReadOnlySpan<char> span = value.AsSpan();
        int colonIndex = span.IndexOf(':');
 
        if (colonIndex == -1)
        {
            throw new InvalidOperationException($"Geçersiz password hash formatı: '{value}'.");
        }
 
        var hashPart = span[..colonIndex];
        var saltPart = span[(colonIndex + 1)..];
 
        // Span-based allocation-free parsing
        byte[] hash = new byte[32]; // Argon2id hash size
        byte[] salt = new byte[16]; // Salt size

        if (!Convert.TryFromBase64Chars(hashPart, hash, out _))
            throw new FormatException("Invalid hash base64.");
            
        if (!Convert.TryFromBase64Chars(saltPart, salt, out _))
            throw new FormatException("Invalid salt base64.");

        return HashedPassword.FromStorage(hash, salt);
    }

    private Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? _currentTransaction;

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _currentTransaction ??= await Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await SaveChangesAsync(cancellationToken);
            if (_currentTransaction is not null)
            {
                await _currentTransaction.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (_currentTransaction is not null)
            {
                _currentTransaction.Dispose();
                _currentTransaction = null;
            }
        }
    }

    public Task<T> ExecuteStrategyAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
    {
        return Database.CreateExecutionStrategy().ExecuteAsync(ct => action(ct), cancellationToken);
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_currentTransaction is not null)
            {
                await _currentTransaction.RollbackAsync(cancellationToken);
            }
        }
        finally
        {
            if (_currentTransaction is not null)
            {
                _currentTransaction.Dispose();
                _currentTransaction = null;
            }
        }
    }

    /// <summary>
    /// Değişiklikleri veritabanına kaydeder. Domain olaylarını Outbox desenine göre kuyruğa alır.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // 1. Domain Event'leri topla ve Outbox'a çevir
        // Aggressive optimization: LINQ yerine manuel döngü ile allocation azaltımı
        List<OutboxMessage>? outboxMessages = null;

        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            if (entry.Entity.DomainEvents.Count > 0)
            {
                outboxMessages ??= new List<OutboxMessage>();
                
                foreach (var domainEvent in entry.Entity.DomainEvents)
                {
                    // Aggressive performance: Manuel type mapping ile doğru context'e yönlendiriyoruz
                    // Reflection-free serialization
                    string content = domainEvent switch
                    {
                        User.Domain.Events.UserCreatedEvent e => System.Text.Json.JsonSerializer.Serialize(e, Serialization.UserJsonContext.Default.UserCreatedEvent),
                        User.Domain.Events.UserLoggedInEvent e => System.Text.Json.JsonSerializer.Serialize(e, Serialization.UserJsonContext.Default.UserLoggedInEvent),
                        User.Domain.Events.UserPasswordChangedEvent e => System.Text.Json.JsonSerializer.Serialize(e, Serialization.UserJsonContext.Default.UserPasswordChangedEvent),
                        User.Domain.Events.UserProfileUpdatedEvent e => System.Text.Json.JsonSerializer.Serialize(e, Serialization.UserJsonContext.Default.UserProfileUpdatedEvent),
                        User.Domain.Events.UserDeactivatedEvent e => System.Text.Json.JsonSerializer.Serialize(e, Serialization.UserJsonContext.Default.UserDeactivatedEvent),
                        User.Domain.Events.UserActivatedEvent e => System.Text.Json.JsonSerializer.Serialize(e, Serialization.UserJsonContext.Default.UserActivatedEvent),
                        User.Domain.Events.UserDeletedEvent e => System.Text.Json.JsonSerializer.Serialize(e, Serialization.UserJsonContext.Default.UserDeletedEvent),
                        _ => System.Text.Json.JsonSerializer.Serialize(domainEvent, domainEvent.GetType()) // Fallback
                    };

                    outboxMessages.Add(new OutboxMessage
                    {
                        Id = Guid.NewGuid(),
                        Type = domainEvent.GetType().Name,
                        Content = content,
                        CreatedAtUtc = DateTime.UtcNow
                    });
                }
                
                entry.Entity.ClearDomainEvents();
            }
        }

        // 2. Outbox mesajlarını veritabanına ekle
        if (outboxMessages is not null)
        {
            await OutboxMessages.AddRangeAsync(outboxMessages, cancellationToken);
        }

        // 3. Tek bir transaction içinde tüm değişiklikleri kaydet
        return await base.SaveChangesAsync(cancellationToken);
    }
}

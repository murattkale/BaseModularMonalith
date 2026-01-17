using System.Data;
using System.Runtime.CompilerServices;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using User.Application.Contracts;
using User.Application.Queries;

namespace User.Infrastructure.Persistence;

/// <summary>
/// Yüksek performanslı kullanıcı okuma servisi (User Read Service).
/// Doğrudan ADO.NET ve Dapper kullanarak, EF Core overhead'ini atlar.
/// Optimization: Native Connection Pooling ve NOLOCK kullanımı.
/// </summary>
public sealed class UserReadService : IUserReadService
{
    private readonly string _connectionString;

    /// <summary>
    /// Ham SQL sorguları (Hardcoded for performance optimization - No string interpolation overhead).
    /// </summary>
    private static class Sql
    {
        // Temel alan seçimi
        public const string GetById = "SELECT Id, Email, FirstName, LastName, IsActive, Roles, CreatedAt, LastLoginAt FROM Users WITH (NOLOCK) WHERE Id = @Id AND DeletedAt IS NULL";
        public const string GetByEmail = "SELECT Id, Email, FirstName, LastName, IsActive, Roles, CreatedAt, LastLoginAt FROM Users WITH (NOLOCK) WHERE Email = @Email AND DeletedAt IS NULL";
        public const string GetAllBase = "SELECT Id, Email, CONCAT(FirstName, ' ', LastName) AS FullName, IsActive, Roles, CreatedAt, LastLoginAt FROM Users WITH (NOLOCK) WHERE DeletedAt IS NULL";
        
        // Pagination sorguları
        public const string GetAllActiveOnly = GetAllBase + " AND IsActive = 1 ORDER BY CreatedAt DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
        public const string GetAllInactiveOnly = GetAllBase + " AND IsActive = 0 ORDER BY CreatedAt DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
        public const string GetAllRaw = GetAllBase + " ORDER BY CreatedAt DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
        
        // Count sorguları (Hızlı sayım)
        public const string GetCountActive = "SELECT COUNT(1) FROM Users WITH (NOLOCK) WHERE DeletedAt IS NULL AND IsActive = 1";
        public const string GetCountInactive = "SELECT COUNT(1) FROM Users WITH (NOLOCK) WHERE DeletedAt IS NULL AND IsActive = 0";
        public const string GetCountRaw = "SELECT COUNT(1) FROM Users WITH (NOLOCK) WHERE DeletedAt IS NULL";
        
        // Varlık kontrolü (Exists - 1/0 döner, en hızlı yöntem)
        public const string EmailExists = "SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM Users WITH (NOLOCK) WHERE Email = @Email AND DeletedAt IS NULL) THEN 1 ELSE 0 END AS BIT)";
    }

    public UserReadService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("ReadConnection")
                          ?? configuration.GetConnectionString("DefaultConnection")
                          ?? throw new InvalidOperationException("Connection string not configured.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private SqlConnection CreateConnection() => new(_connectionString);

    /// <summary>
    /// ID'ye göre kullanıcı detaylarını getirir (NOLOCK ile kilitlenmeden).
    /// </summary>
    public async ValueTask<UserDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await BuildingBlocks.Resilience.ResiliencePipelines.DbPipeline.ExecuteAsync(async ct =>
        {
            using var connection = CreateConnection();
            return await connection.QueryFirstOrDefaultAsync<UserDto>(
                new CommandDefinition(
                    Sql.GetById,
                    new { Id = id },
                    commandType: CommandType.Text,
                    cancellationToken: ct,
                    flags: CommandFlags.None)).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Email adresine göre kullanıcı detaylarını getirir.
    /// </summary>
    public async ValueTask<UserDto?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await BuildingBlocks.Resilience.ResiliencePipelines.DbPipeline.ExecuteAsync(async ct =>
        {
            using var connection = CreateConnection();
            return await connection.QueryFirstOrDefaultAsync<UserDto>(
                new CommandDefinition(
                    Sql.GetByEmail,
                    new { Email = email.ToLowerInvariant() },
                    commandType: CommandType.Text,
                    cancellationToken: ct)).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<IReadOnlyList<UserListItemDto>> GetAllAsync(
        int page,
        int pageSize,
        bool? isActive = null,
        DateTime? afterCreatedAt = null,
        Guid? afterId = null,
        CancellationToken cancellationToken = default)
    {
        return await BuildingBlocks.Resilience.ResiliencePipelines.DbPipeline.ExecuteAsync(async ct =>
        {
            using var connection = CreateConnection();
            
            string sql;
            object parameters;

            // Keyset Pagination (Extremely fast for deep paging)
            if (afterCreatedAt.HasValue && afterId.HasValue)
            {
                var filterSql = isActive switch
                {
                    true => "AND IsActive = 1",
                    false => "AND IsActive = 0",
                    _ => ""
                };

                // Keyset Pagination - Parametreli sorgu ile SQL Injection önlendi
                sql = $@"SELECT Id, Email, CONCAT(FirstName, ' ', LastName) AS FullName, IsActive, Roles, CreatedAt, LastLoginAt 
                        FROM Users WITH (NOLOCK) 
                        WHERE DeletedAt IS NULL {filterSql} 
                        AND (CreatedAt < @AfterCreatedAt OR (CreatedAt = @AfterCreatedAt AND Id < @AfterId))
                        ORDER BY CreatedAt DESC, Id DESC 
                        OFFSET 0 ROWS FETCH NEXT @PageSize ROWS ONLY";
                
                parameters = new { AfterCreatedAt = afterCreatedAt.Value, AfterId = afterId.Value, PageSize = pageSize };
            }
            else
            {
                // Classic Offset Pagination
                var offset = (page - 1) * pageSize;
                var baseSql = isActive switch
                {
                    true => Sql.GetAllActiveOnly,
                    false => Sql.GetAllInactiveOnly,
                    _ => Sql.GetAllRaw
                };
                sql = baseSql;
                parameters = new { Offset = offset, PageSize = pageSize };
            }

            var result = await connection.QueryAsync<UserListItemDto>(
                new CommandDefinition(
                    sql,
                    parameters,
                    commandType: CommandType.Text,
                    cancellationToken: ct)).ConfigureAwait(false);

            return (IReadOnlyList<UserListItemDto>)result.AsList();
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<int> GetCountAsync(bool? isActive = null, CancellationToken cancellationToken = default)
    {
        return await BuildingBlocks.Resilience.ResiliencePipelines.DbPipeline.ExecuteAsync(async ct =>
        {
            using var connection = CreateConnection();
            var sql = isActive switch
            {
                true => Sql.GetCountActive,
                false => Sql.GetCountInactive,
                _ => Sql.GetCountRaw
            };

            return await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    sql,
                    cancellationToken: ct)).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Verilen email adresinin kullanımda olup olmadığını kontrol eder.
    /// Exists sorgusu ile sadece true/false döner, veri çekmez (Network I/O tasarrufu).
    /// </summary>
    public async ValueTask<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        return await BuildingBlocks.Resilience.ResiliencePipelines.DbPipeline.ExecuteAsync(async ct =>
        {
            using var connection = CreateConnection();
            return await connection.ExecuteScalarAsync<bool>(
                new CommandDefinition(
                    Sql.EmailExists,
                    new { Email = email.ToLowerInvariant() },
                    cancellationToken: ct)).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }
}

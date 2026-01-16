using Microsoft.EntityFrameworkCore;
using SharedKernel;
using BuildingBlocks.Behaviors;

namespace User.Infrastructure.Persistence;

/// <summary>
/// Veritabanı tabanlı Idempotency servisi.
/// Cache kullanılmadığı için doğrudan SQL tablosu üzerinden kontrol yapar.
/// </summary>
public sealed class IdempotencyService : IIdempotencyService
{
    private readonly UserDbContext _context;

    public IdempotencyService(UserDbContext context)
    {
        _context = context;
    }

    public async ValueTask<bool> ExistsAsync(Guid requestId, CancellationToken ct = default)
    {
        return await _context.RequestIdExistsAsync(requestId, ct);
    }

    public ValueTask CreateAsync(Guid requestId, string name, CancellationToken ct = default)
    {
        _context.IdempotentRequests.Add(new IdempotentRequest
        {
            Id = requestId,
            Name = name,
            CreatedAt = DateTime.UtcNow
        });
        
        return ValueTask.CompletedTask;
    }
}

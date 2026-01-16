using System.Text.Json;
using BuildingBlocks.DomainEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SharedKernel;
using Microsoft.Extensions.Configuration;
using User.Infrastructure.Persistence;

namespace User.Infrastructure.Services;

/// <summary>
/// Veritabanındaki Outbox mesajlarını işleyen arka plan servisi.
/// "At Least Once" (En az bir kez) teslimat garantisi sağlar.
/// </summary>
public sealed class OutboxProcessor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxProcessor> _logger;
    private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;
    private const int BatchSize = 100;

    public OutboxProcessor(
        IServiceProvider serviceProvider, 
        ILogger<OutboxProcessor> logger,
        Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox Processor başlatıldı.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox işlenirken beklenmedik hata oluştu.");
            }

            // High-scale için bekleme süresi yapılandırılabilir
            var interval = _configuration.GetValue<int>("Workers:OutboxPollingIntervalSeconds", 2);
            await Task.Delay(TimeSpan.FromSeconds(interval), stoppingToken);
        }
    }

    private async Task ProcessMessagesAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<UserDbContext>();

        // Atomic "Claim" mechanism using SQL Server OUTPUT clause
        // This prevents multiple replicas from processing the same messages.
        // We select messages where ProcessedAtUtc is null AND update their ProcessedAtUtc to a "lock" value.
        // For simplicity here, we mark them as processed with a very old date or a specific Lock ID if we had one.
        // Better: Use a dedicated 'ProcessingLockedUntil' column.
        // For now, we'll use a transaction with RowLock and SkipLocked for high-concurrency efficiency.
        
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
            
            try
            {
                // SQL Server specific "SKIP LOCKED" to allow multiple processors to work on different batches
                var messages = await dbContext.OutboxMessages
                    .FromSqlRaw(@"
                        SELECT TOP (@p0) * 
                        FROM OutboxMessages WITH (UPDLOCK, READPAST) 
                        WHERE ProcessedAtUtc IS NULL 
                        ORDER BY CreatedAtUtc", new Microsoft.Data.SqlClient.SqlParameter("@p0", BatchSize))
                    .ToListAsync(ct);

                if (messages.Count == 0) return;

                foreach (var message in messages)
                {
                    try
                    {
                        IDomainEvent? domainEvent = message.Type switch
                        {
                            nameof(User.Domain.Events.UserCreatedEvent) => JsonSerializer.Deserialize(message.Content, User.Infrastructure.Serialization.UserJsonContext.Default.UserCreatedEvent),
                            nameof(User.Domain.Events.UserLoggedInEvent) => JsonSerializer.Deserialize(message.Content, User.Infrastructure.Serialization.UserJsonContext.Default.UserLoggedInEvent),
                            nameof(User.Domain.Events.UserPasswordChangedEvent) => JsonSerializer.Deserialize(message.Content, User.Infrastructure.Serialization.UserJsonContext.Default.UserPasswordChangedEvent),
                            nameof(User.Domain.Events.UserProfileUpdatedEvent) => JsonSerializer.Deserialize(message.Content, User.Infrastructure.Serialization.UserJsonContext.Default.UserProfileUpdatedEvent),
                            nameof(User.Domain.Events.UserDeactivatedEvent) => JsonSerializer.Deserialize(message.Content, User.Infrastructure.Serialization.UserJsonContext.Default.UserDeactivatedEvent),
                            nameof(User.Domain.Events.UserActivatedEvent) => JsonSerializer.Deserialize(message.Content, User.Infrastructure.Serialization.UserJsonContext.Default.UserActivatedEvent),
                            nameof(User.Domain.Events.UserDeletedEvent) => JsonSerializer.Deserialize(message.Content, User.Infrastructure.Serialization.UserJsonContext.Default.UserDeletedEvent),
                            _ => null
                        };

                        if (domainEvent != null)
                        {
                            var dispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();
                            await dispatcher.DispatchEventAsync(domainEvent, ct);
                        }
                        
                        message.ProcessedAtUtc = DateTime.UtcNow;
                    }
                    catch (Exception ex)
                    {
                        message.Error = ex.Message;
                        _logger.LogError(ex, "Outbox mesajı işlenemedi: {Id}", message.Id);
                    }
                }

                await dbContext.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox batch processing failed. Transaction rolled back.");
                await transaction.RollbackAsync(ct);
                throw;
            }
        });
    }
}

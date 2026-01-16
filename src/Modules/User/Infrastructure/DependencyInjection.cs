using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;
using User.Application.Commands;
using User.Application.Contracts;
using User.Infrastructure.Persistence;
using User.Infrastructure.Services;

namespace User.Infrastructure;

/// <summary>
/// User modülü dependency injection.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddUserModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Dedicated Server Optimizasyonu (32GB RAM)
        // Yüksek eşzamanlılık (Extreme Load) desteği için Max Pool Size artırıldı
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        var maxPoolSize = configuration.GetValue<int>("Database:MaxPoolSize", 1024);
        if (!string.IsNullOrEmpty(connectionString) && !connectionString.Contains("Max Pool Size", StringComparison.OrdinalIgnoreCase))
        {
            connectionString += $";Max Pool Size={maxPoolSize};Pooling=true;";
        }

        var dbPoolSize = configuration.GetValue<int>("Database:DbContextPoolSize", maxPoolSize);

        services.AddDbContextPool<UserDbContext>((sp, options) =>
        {
            options.UseSqlServer(
                connectionString,
                sqlOptions =>
                {
                    // NOT: Retry (tekrar deneme) mantığı repository seviyesinde Polly ResiliencePipelines ile yönetilir.
                    // Çift retry (EF + Polly) işlem (transaction) sorunlarına yol açabilir.
                    sqlOptions.CommandTimeout(30);
                    sqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                });

            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);

            #if DEBUG
            options.EnableSensitiveDataLogging();
            options.EnableDetailedErrors();
            #endif
        }, poolSize: dbPoolSize); // Donanıma göre yapılandırılabilir havuz boyutu

        // Repository ve servisler
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<UserDbContext>());
        
        // Read service - Scoped (Standard lifecycle management)
        services.AddScoped<IUserReadService, UserReadService>();

        // JWT token generator
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

        // Idempotency
        services.AddScoped<BuildingBlocks.Behaviors.IIdempotencyService, IdempotencyService>();

        // Diagnostics
        services.AddSingleton<IUserMetrics, User.Infrastructure.Diagnostics.UserMetrics>();

        // Background Workers
        services.AddHostedService<OutboxProcessor>();

        return services;
    }
}

using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace Api.RateLimiting;

/// <summary>
/// Yüksek trafik senaryoları için rate limiting yapılandırması.
/// In-memory fixed/sliding window limiter kullanır (hafif, harici bağımlılık yok).
/// </summary>
public static class RateLimitingConfiguration
{
    /// <summary>
    /// Varsayılan policy (anonim kullanıcılar için).
    /// </summary>
    public const string DefaultPolicy = "default";
    
    /// <summary>
    /// Kimlik doğrulanmış kullanıcılar için policy.
    /// </summary>
    public const string AuthenticatedPolicy = "authenticated";

    /// <summary>
    /// Rate limiting policy'lerini servislere ekler.
    /// </summary>
    public static IServiceCollection AddRateLimitingPolicies(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            
            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.ContentType = "application/json";
                
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = retryAfter.TotalSeconds.ToString("0");
                }

                await context.HttpContext.Response.WriteAsJsonAsync(new SharedKernel.ApiError(
                    Code: "RateLimitExceeded",
                    Message: "Çok fazla istek gönderildi. Lütfen daha sonra tekrar deneyin."
                ), token);
            };

                // Anonim kullanıcılar için - Dakikada 60 istek
                options.AddFixedWindowLimiter(DefaultPolicy, limiterOptions =>
                {
                    limiterOptions.PermitLimit = 60;
                    limiterOptions.Window = TimeSpan.FromMinutes(1);
                    limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    limiterOptions.QueueLimit = 2;
                });

            // Kimlik doğrulanmış kullanıcılar için - Saniyede 10 / Dakikada 600 istek
            options.AddTokenBucketLimiter(AuthenticatedPolicy, limiterOptions =>
            {
                limiterOptions.TokenLimit = 600;
                limiterOptions.TokensPerPeriod = 10;
                limiterOptions.ReplenishmentPeriod = TimeSpan.FromSeconds(1);
                limiterOptions.QueueLimit = 10;
            });

            // Global limiter - sliding window (IP bazlı) - Maksimum seviyeye çekildi
            options.GlobalLimiter = PartitionedRateLimiter.Create<Microsoft.AspNetCore.Http.HttpContext, string>(context =>
            {
                // Reverse proxy arkasında doğru IP için X-Forwarded-For kullan
                var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
                var clientIp = !string.IsNullOrEmpty(forwardedFor) 
                    ? forwardedFor.Split(',')[0].Trim() 
                    : context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                
                return RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: clientIp,
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 1000,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 6, // 10 saniyelik segmentler
                        QueueLimit = 0
                    });
            });
        });

        return services;
    }
}

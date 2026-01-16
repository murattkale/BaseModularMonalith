using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using BuildingBlocks.CQRS;
using System.Text.Json;
using System.Security.Claims;
using SharedKernel;

namespace BuildingBlocks.Behaviors;

/// <summary>
/// IAuditable arayüzü ile işaretlenmiş istekleri AuditLogs tablosuna/loguna kaydeder.
/// </summary>
public sealed class AuditLoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditLoggingBehavior<TRequest, TResponse>> _logger;

    /// <summary>
    /// AuditLoggingBehavior sınıfı için yeni bir örnek oluşturur.
    /// </summary>
    /// <param name="httpContextAccessor">HTTP bağlamına erişim sağlar.</param>
    /// <param name="logger">Loglama işlemlerini gerçekleştirir.</param>
    public AuditLoggingBehavior(IHttpContextAccessor httpContextAccessor, ILogger<AuditLoggingBehavior<TRequest, TResponse>> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <summary>
    /// Pipeline üzerindeki isteği işler ve IAuditable olanları loglar.
    /// </summary>
    /// <param name="request">İşlenecek istek.</param>
    /// <param name="next">Pipeline'daki bir sonraki işlem.</param>
    /// <param name="cancellationToken">İptal token'ı.</param>
    /// <returns>İşlem sonucu.</returns>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not IAuditable)
        {
            return await next();
        }

        // Performans için sadece logluyoruz
        if (_logger.IsEnabled(LogLevel.Information))
        {
            var context = _httpContextAccessor.HttpContext;
            var userId = context?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Anonymous";
            var ipAddress = context?.Connection?.RemoteIpAddress?.ToString() ?? "Unknown";
            var userAgent = context?.Request?.Headers["User-Agent"].ToString() ?? "Unknown";

            // Aggressive performance: Sadece gerekli alanlar veya limitli serileştirme
            // JsonSerializerOptions statik context'ten gelmeli veya varsayılan kullanılmalı
            var data = _logger.IsEnabled(LogLevel.Debug) 
                ? JsonSerializer.Serialize(request, request.GetType()) 
                : "{hidden}";

            _logger.LogInformation(
                "[Audit] User: {UserId}, Op: {Operation}, IP: {Ip}, UA: {UserAgent}, Data: {Data}",
                userId,
                typeof(TRequest).Name,
                ipAddress,
                userAgent,
                data);
        }

        return await next();
    }
}

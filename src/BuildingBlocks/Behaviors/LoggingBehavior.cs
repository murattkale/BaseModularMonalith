using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Behaviors;

/// <summary>
/// Request/Response loglama için MediatR pipeline behavior'ı.
/// Not: Loglama log level'a göre koşullu yapılır (hot path overhead'ini önlemek için).
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;
    private const int SlowRequestThresholdMs = 500;
    
    // Yüksek performanslı loglama için statik delegeler (template ayrıştırma maliyetini önlemek için)
    private static readonly Action<ILogger, string, Exception?> LogRequestStarted =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(1, nameof(Handle)), "[Başlangıç] {RequestName}");

    private static readonly Action<ILogger, string, double, Exception?> LogRequestCompleted =
        LoggerMessage.Define<string, double>(LogLevel.Debug, new EventId(2, nameof(Handle)), "[Tamamlandı] {RequestName} - {ElapsedMilliseconds}ms");

    private static readonly Action<ILogger, string, double, Exception?> LogSlowRequest =
        LoggerMessage.Define<string, double>(LogLevel.Warning, new EventId(3, nameof(Handle)), "[Yavaş Request] {RequestName} - {ElapsedMilliseconds}ms");

    // Tip adını önbelleğe al - her request'te hesaplanmasın
    private static readonly string RequestName = typeof(TRequest).Name;

    /// <summary>
    /// LoggingBehavior sınıfı için yeni bir örnek oluşturur.
    /// </summary>
    /// <param name="logger">Loglama işlemlerini gerçekleştirir.</param>
    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// İsteği loglar ve çalışma süresini ölçer.
    /// </summary>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            LogRequestStarted(_logger, RequestName, null);
        }

        var startTimestamp = Stopwatch.GetTimestamp();
        var response = await next();
        var elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;

        if (elapsedMs > SlowRequestThresholdMs)
        {
            LogSlowRequest(_logger, RequestName, elapsedMs, null);
        }
        else if (_logger.IsEnabled(LogLevel.Debug))
        {
            LogRequestCompleted(_logger, RequestName, elapsedMs, null);
        }

        return response;
    }
}

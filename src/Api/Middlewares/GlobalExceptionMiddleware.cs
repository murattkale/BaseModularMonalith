using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Api.Middlewares;

/// <summary>
/// Global exception handling middleware.
/// Standartlaştırılmış hata yanıtları döndürür.
/// Stack trace'ler client'lara gösterilmez (güvenlik).
/// </summary>
public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    
    // Source generated JSON context - reflection-free serialization
    private static readonly JsonSerializerOptions JsonOptions = Api.Serialization.AppJsonContext.Default.Options;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client bağlantıyı kesti - hata olarak loglama
            context.Response.StatusCode = 499; // Client Closed Request
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        _logger.LogError(exception, 
            "[Hata] İşlenmeyen exception. TraceId: {TraceId}", 
            context.TraceIdentifier);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = GetStatusCode(exception);

        var response = new ApiError(
            Code: GetErrorCode(exception),
            Message: GetUserSafeMessage(exception),
            TraceId: context.TraceIdentifier);

        await context.Response.WriteAsJsonAsync(response, JsonOptions);
    }

    private static int GetStatusCode(Exception exception) => exception switch
    {
        ArgumentException => (int)HttpStatusCode.BadRequest,
        UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
        InvalidOperationException => (int)HttpStatusCode.Conflict,
        KeyNotFoundException => (int)HttpStatusCode.NotFound,
        _ => (int)HttpStatusCode.InternalServerError
    };

    private static string GetErrorCode(Exception exception) => exception switch
    {
        ArgumentException => "BadRequest",
        UnauthorizedAccessException => "Unauthorized",
        InvalidOperationException => "Conflict",
        KeyNotFoundException => "NotFound",
        _ => "InternalError"
    };

    private static string GetUserSafeMessage(Exception exception) => exception switch
    {
        ArgumentException ex => ex.Message,
        UnauthorizedAccessException => "Bu işlem için yetkiniz yok.",
        KeyNotFoundException => "İstenen kaynak bulunamadı.",
        _ => "Beklenmeyen bir hata oluştu. Lütfen daha sonra tekrar deneyin."
    };
}

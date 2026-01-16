using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Api.Middlewares;

/// <summary>
/// Şüpheli aktiviteleri (SQL Injection, XSS denemeleri) tespit eden ve 
/// güvenlik ekibine/loguna raporlayan middleware.
/// Regex-based pattern matching for O(1) performance.
/// </summary>
public sealed partial class SecurityAuditMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SecurityAuditMiddleware> _logger;

    // Source-generated Regex - Compile-time optimization, zero allocation
    [GeneratedRegex(@"SELECT\s|DROP\s|INSERT\s|UNION\s|--|<script|alert\(|javascript:", 
        RegexOptions.IgnoreCase | RegexOptions.Compiled, matchTimeoutMilliseconds: 100)]
    private static partial Regex DangerousPatternRegex();

    public SecurityAuditMiddleware(RequestDelegate next, ILogger<SecurityAuditMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var query = context.Request.QueryString.Value ?? string.Empty;
        var path = context.Request.Path.Value ?? string.Empty;

        // Single regex match instead of 8 separate Contains calls
        var input = string.Concat(path, query);
        
        try
        {
            if (DangerousPatternRegex().IsMatch(input))
            {
                var match = DangerousPatternRegex().Match(input);
                _logger.LogWarning(
                    "[GUVENLIK] Supheli istek tespit edildi! IP: {Ip}, Pattern: {Pattern}, Path: {Path}{Query}",
                    context.Connection.RemoteIpAddress,
                    match.Value,
                    path,
                    query);
                
                // İsteği burada bloklayabiliriz (isteğe bağlı)
                // context.Response.StatusCode = 403;
                // return;
            }
        }
        catch (RegexMatchTimeoutException)
        {
            // Regex timeout - potansiyel ReDoS saldırısı
            _logger.LogWarning("[GUVENLIK] Regex timeout - potansiyel ReDoS! IP: {Ip}", 
                context.Connection.RemoteIpAddress);
        }

        await _next(context);
    }
}

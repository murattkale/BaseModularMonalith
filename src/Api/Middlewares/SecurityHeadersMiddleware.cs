using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Api.Middlewares;

/// <summary>
/// Adds security headers to responses to protect against common attacks.
/// Implements Owasp recommendations.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        // 1. Anti-MIME-Sniffing: Prevents browser from interpreting files as something else than declared
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");

        // 2. XSS Protection: Enables browser's built-in XSS filter
        context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");

        // 3. Clickjacking Protection: Deny rendering within a frame/iframe
        context.Response.Headers.Append("X-Frame-Options", "DENY");

        // 4. Strict Transport Security (HSTS): Enforce HTTPS (1 year)
        // Note: Only active in Production usually, but good to have.
        // Uncomment if you have HTTPS set up.
        // context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");

        // 5. Content Security Policy (CSP): Restrict resources (scripts, styles, etc.)
        // This is a strict policy. Adjust strict-dynamic/hashes if needed.
        context.Response.Headers.Append("Content-Security-Policy", 
            "default-src 'self'; " +
            "img-src 'self' data: https:; " +
            "font-src 'self'; " +
            "style-src 'self' 'unsafe-inline'; " +
            "script-src 'self' 'unsafe-inline'; " + // 'unsafe-inline' for swagger/dev usually needed
            "frame-ancestors 'none';"
        );

        // 5.1 Clear-Site-Data
        context.Response.Headers.Append("Clear-Site-Data", "\"cache\"");

        // 6. Referrer Policy: Control how much referrer info is sent
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");

        // 7. Permissions Policy: Disable unused browser features
        context.Response.Headers.Append("Permissions-Policy", 
            "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()");

        // 8. Remove Server Header (Obfuscation)
        context.Response.Headers.Remove("Server");
        context.Response.Headers.Remove("X-Powered-By");

        return _next(context);
    }
}

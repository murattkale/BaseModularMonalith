using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace Api.Middlewares;

/// <summary>
/// Server-Timing header middleware.
/// API yanıtına "Server-Timing" header'ı ekleyerek backend işlem süresini milisaniye cinsinden döner.
/// </summary>
public sealed class ServerTimingMiddleware
{
    private readonly RequestDelegate _next;

    public ServerTimingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();

        context.Response.OnStarting(() =>
        {
            sw.Stop();
            var elapsedMs = sw.Elapsed.TotalMilliseconds;
            
            // Server-Timing: total;dur=123.45;desc="Total Processing Time"
            context.Response.Headers.Append("Server-Timing", $"total;dur={elapsedMs:F2};desc=\"Total Processing Time\"");
            
            return Task.CompletedTask;
        });

        await _next(context);
    }
}

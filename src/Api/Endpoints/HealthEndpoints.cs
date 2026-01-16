using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;

namespace Api.Endpoints;

/// <summary>
/// Sağlık kontrolü endpoint'leri.
/// Load balancer ve monitoring sistemleri için kullanılır.
/// </summary>
public static class HealthEndpoints
{
    /// <summary>
    /// Health endpoint'lerini route'lara ekler.
    /// </summary>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        // Standart health check ayarları
        var options = new HealthCheckOptions
        {
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";
                var result = JsonSerializer.Serialize(new
                {
                    Status = report.Status.ToString(),
                    Duration = report.TotalDuration,
                    Info = report.Entries.Select(e => new
                    {
                        Key = e.Key,
                        Status = e.Value.Status.ToString(),
                        Description = e.Value.Description,
                        Data = e.Value.Data
                    })
                });
                await context.Response.WriteAsync(result);
            }
        };

        // Liveness probe - uygulama çalışıyor mu? (Sadece canlılık)
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false // Hiçbir check çalıştırma, sadece 200 döner
        });

        // Readiness probe - uygulama tüm bağımlılıkları ile hazır mı?
        app.MapHealthChecks("/health/ready", options);

        return app;
    }
}

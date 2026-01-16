using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using User.Infrastructure;

namespace User.Api;

/// <summary>
/// User modülü extension'ları.
/// </summary>
public static class UserModule
{
    public static IServiceCollection AddUserServices(this IServiceCollection services, IConfiguration configuration)
    {
        return services.AddUserModule(configuration);
    }

    public static IEndpointRouteBuilder MapUserModule(this IEndpointRouteBuilder app)
    {
        return app.MapUserEndpoints();
    }
}

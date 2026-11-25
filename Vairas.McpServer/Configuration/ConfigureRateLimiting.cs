using System.Security.Claims;
using System.Threading.RateLimiting;

namespace Vairas.McpServer.Configuration;

public static class ConfigureRateLimiting
{
    public static IServiceCollection AddToolRateLimiting(this IServiceCollection services)
    {
        services.AddAuthorization();
        services.AddRateLimiter(o => o.AddPolicy("mcp", http => RateLimitPartition.GetFixedWindowLimiter(http.User.FindFirstValue("sub") ?? "unknown", _ => new() { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })));
        return services;
    }
}

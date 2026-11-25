using Vairas.McpServer.Business;
using Vairas.McpServer.Integrations;

namespace Vairas.McpServer.Configuration;

/// <summary>Application services; infrastructure registrations live in the dedicated Configure* classes.</summary>
public static class ConfigureDependencyInjection
{
    public static IServiceCollection AddMcpServices(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<DelegatedTokens>();
        services.AddScoped<ConfirmedLessonTransport>();
        services.AddScoped<IMcpOwners, McpOwners>();
        services.AddScoped<ToolExecutor>();
        return services;
    }
}

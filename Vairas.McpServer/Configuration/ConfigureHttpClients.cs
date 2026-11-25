using Vairas.McpServer.Integrations;

namespace Vairas.McpServer.Configuration;

public static class ConfigureHttpClients
{
    public static IServiceCollection AddOwnerHttpClients(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddHttpClient("owners", client => client.Timeout = TimeSpan.FromSeconds(5))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });
        services.AddSingleton<IWorkloadTokens, WorkloadTokens>();
        services.AddScoped<OwnerTransport>();
        return services;
    }
}

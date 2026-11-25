using Microsoft.Extensions.Options;
using Vairas.McpServer.Configuration.Options;

namespace Vairas.McpServer.Configuration;

/// <summary>Binds and validates every strongly typed options section; invalid required settings fail at startup.</summary>
public static class ConfigureOptions
{
    public static IServiceCollection AddMcpOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<McpOptions>().Bind(configuration.GetSection("Mcp")).ValidateOnStart();
        services.AddSingleton<IValidateOptions<McpOptions>, McpOptionsValidator>();
        services.AddOptions<DependencyOptions>().Bind(configuration.GetSection("Dependencies")).ValidateOnStart();
        services.AddSingleton<IValidateOptions<DependencyOptions>, DependencyOptionsValidator>();
        // Adapters take the plain settings objects; hosts and tests may replace them.
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<McpOptions>>().Value);
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<DependencyOptions>>().Value);
        return services;
    }
}

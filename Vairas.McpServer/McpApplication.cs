using Vairas.McpServer.Configuration;

namespace Vairas.McpServer;

/// <summary>Composition root. Tests build the same host through <see cref="Build"/> and may adjust the builder first.</summary>
public static class McpApplication
{
    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        configure?.Invoke(builder);

        builder.Logging.AddJsonLogging();
        builder.WebHost.AddHardenedKestrel();
        builder.Services
            .AddMcpOptions(builder.Configuration)
            .AddMcpServices()
            .AddOwnerHttpClients()
            .AddBearerAuthentication()
            .AddToolRateLimiting()
            .AddMcpTools()
            .AddServiceTelemetry(builder.Configuration);

        var app = builder.Build();
        app.UseMiddleware<Boundary>();
        app.UseRouting();
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();
        app.MapMcp("/mcp").RequireAuthorization().RequireRateLimiting("mcp");
        app.MapServiceHealthChecks();
        return app;
    }
}

namespace Vairas.McpServer.Configuration;

/// <summary>Liveness and configuration readiness; owner connectivity is deliberately not probed.</summary>
public static class ConfigureHealthChecks
{
    public static WebApplication MapServiceHealthChecks(this WebApplication app)
    {
        app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
        app.MapGet("/health/ready", () => Results.Ok(new { status = "configured", ownerConnectivity = "not-probed" }));
        return app;
    }
}

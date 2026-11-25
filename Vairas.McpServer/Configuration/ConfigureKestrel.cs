namespace Vairas.McpServer.Configuration;

public static class ConfigureKestrel
{
    /// <summary>No server header and a 64 KiB request body limit: MCP calls carry short JSON arguments.</summary>
    public static IWebHostBuilder AddHardenedKestrel(this IWebHostBuilder webHost) =>
        webHost.ConfigureKestrel(kestrel =>
        {
            kestrel.AddServerHeader = false;
            kestrel.Limits.MaxRequestBodySize = 65536;
        });
}

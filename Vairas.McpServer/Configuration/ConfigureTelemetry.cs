using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Vairas.McpServer.Configuration;

public static class ConfigureTelemetry
{
    public static IServiceCollection AddServiceTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        var exportToCollector = !string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);
        services.AddOpenTelemetry().ConfigureResource(resource => resource.AddService("vairas-mcp-server"))
            .WithTracing(tracing => { tracing.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation(); if (exportToCollector) tracing.AddOtlpExporter(); })
            .WithMetrics(metrics => { metrics.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation(); if (exportToCollector) metrics.AddOtlpExporter(); });
        return services;
    }
}

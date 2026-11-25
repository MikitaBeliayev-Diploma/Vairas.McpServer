using System.Diagnostics;
using System.Net;
using System.Text.Json;
namespace Vairas.McpServer;

public sealed class Boundary(RequestDelegate next, ILogger<Boundary> logger)
{
    public async Task InvokeAsync(HttpContext http)
    {
        var health = http.Request.Path.StartsWithSegments("/health");
        if (!health && (!http.Request.IsHttps && (http.Connection.RemoteIpAddress is null || !IPAddress.IsLoopback(http.Connection.RemoteIpAddress)) || http.Request.Headers.ContainsKey("Origin"))) { http.Response.StatusCode = 403; return; }
        var incoming = http.Request.Headers["X-Correlation-ID"];
        var correlation = incoming.Count == 1 && System.Text.RegularExpressions.Regex.IsMatch(incoming[0]!, "^[A-Za-z0-9._-]{1,64}$") ? incoming[0]! : Guid.NewGuid().ToString("D");
        http.TraceIdentifier = correlation; http.Response.Headers["X-Correlation-ID"] = correlation;
        var started = Stopwatch.GetTimestamp();
        try
        {
            if (http.Request.Path == "/mcp" && HttpMethods.IsPost(http.Request.Method) && http.Request.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) == true)
            {
                http.Request.EnableBuffering(65536, 65536);
                try { using var json = await JsonDocument.ParseAsync(http.Request.Body, new JsonDocumentOptions { MaxDepth = 32 }, http.RequestAborted); Unique(json.RootElement); }
                catch (Exception e) when (e is JsonException or IOException or InvalidOperationException) { http.Response.StatusCode = 400; return; }
                finally { http.Request.Body.Position = 0; }
            }
            await next(http);
        }
        catch (OperationCanceledException) when (http.RequestAborted.IsCancellationRequested) { http.Abort(); }
        catch (Exception e)
        {
            logger.LogError("MCP request failed with {ExceptionType}", e.GetType().Name);
            if (http.Response.HasStarted) http.Abort(); else http.Response.StatusCode = 503;
        }
        finally { logger.LogInformation("MCP {Method} returned {StatusCode} in {ElapsedMs} ms; correlation {CorrelationId}", http.Request.Method, http.Response.StatusCode, Stopwatch.GetElapsedTime(started).TotalMilliseconds, correlation); }
    }
    private static void Unique(JsonElement json)
    {
        if (json.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in json.EnumerateObject()) { if (!names.Add(property.Name)) throw new JsonException(); Unique(property.Value); }
        }
        else if (json.ValueKind == JsonValueKind.Array) foreach (var value in json.EnumerateArray()) Unique(value);
    }
}

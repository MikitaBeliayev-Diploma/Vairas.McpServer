using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Vairas.McpServer.Business;

namespace Vairas.McpServer.Integrations;


public sealed class OwnerTransport(IHttpClientFactory clients, IWorkloadTokens workload, IHttpContextAccessor context)
{
    public async Task<JsonElement> SendAsync(string origin, string path, HttpMethod method, object? body, string? userBearer, CancellationToken ct, bool concealForbidden = true)
    {
        try
        {
            using var request = new HttpRequestMessage(method, new Uri(new Uri(origin), path));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userBearer ?? await workload.GetAsync(ct));
            request.Headers.Add("X-Correlation-ID", context.HttpContext?.Items["CorrelationId"]?.ToString() ?? Guid.NewGuid().ToString("D"));
            if (body is not null) request.Content = JsonContent.Create(body, options: Wire.Json);
            using var response = await clients.CreateClient("owners").SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
                throw (int)response.StatusCode switch { 401 => new UnauthorizedException("SESSION_INVALID"), 403 when !concealForbidden => new ForbiddenException("PROFILE_INACTIVE"), 403 or 404 => McpErrors.Missing(), 409 => McpErrors.Conflict("OWNER_CONFLICT"), 400 or 422 => new UnprocessableEntityException("OWNER_INPUT_REJECTED"), _ => McpErrors.Dependency() };
            await using var stream = await response.Content.ReadAsStreamAsync(ct); using var bytes = new MemoryStream(); var buffer = new byte[8192];
            for (var length = await stream.ReadAsync(buffer, ct); length != 0; length = await stream.ReadAsync(buffer, ct))
            { if (bytes.Length + length > 524288) throw McpErrors.Dependency("OWNER_RESPONSE_TOO_LARGE"); await bytes.WriteAsync(buffer.AsMemory(0, length), ct); }
            using var document = JsonDocument.Parse(bytes.ToArray(), new JsonDocumentOptions { MaxDepth = 32 });
            ValidateObject(document.RootElement); return document.RootElement.Clone();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw McpErrors.Dependency("OWNER_TIMEOUT"); }
        catch (HttpRequestException) { throw McpErrors.Dependency("OWNER_UNAVAILABLE"); }
        catch (JsonException) { throw McpErrors.Dependency("OWNER_RESPONSE_INVALID"); }
    }
    private static void ValidateObject(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var properties = element.EnumerateObject().ToArray();
            if (properties.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != properties.Length) throw McpErrors.Dependency("OWNER_RESPONSE_INVALID");
            foreach (var property in properties) ValidateObject(property.Value);
        }
        else if (element.ValueKind == JsonValueKind.Array) foreach (var item in element.EnumerateArray()) ValidateObject(item);
    }
}

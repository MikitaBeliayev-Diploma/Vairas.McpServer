using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Vairas.McpServer.Business;
using Vairas.McpServer.Configuration.Options;

namespace Vairas.McpServer.Integrations;


public sealed class WorkloadTokens(IHttpClientFactory clients, DependencyOptions options, TimeProvider clock) : IWorkloadTokens, IDisposable
{
    private readonly SemaphoreSlim gate = new(1); private string token = ""; private DateTimeOffset until;
    public async Task<string> GetAsync(CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            if (until > clock.GetUtcNow().AddSeconds(30)) return token;
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(options.AuthBaseUrl), "connect/token"))
            { Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials", ["client_id"] = options.ClientId, ["client_secret"] = options.ClientSecret, ["scope"] = options.Scope }) };
            using var response = await clients.CreateClient("owners").SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) throw McpErrors.Dependency("WORKLOAD_TOKEN_REJECTED");
            var result = await response.Content.ReadFromJsonAsync<TokenReply>(cancellationToken: ct) ?? throw McpErrors.Dependency("WORKLOAD_TOKEN_REJECTED");
            if (!result.TokenType.Equals("Bearer", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(result.AccessToken) || result.ExpiresIn is < 31 or > 3600 || options.Scope.Split(' ').Except(result.Scope.Split(' '), StringComparer.Ordinal).Any()) throw McpErrors.Dependency("WORKLOAD_TOKEN_REJECTED");
            token = result.AccessToken; until = clock.GetUtcNow().AddSeconds(result.ExpiresIn); return token;
        }
        finally { gate.Release(); }
    }
    public void Dispose() => gate.Dispose();
    private sealed record TokenReply([property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string AccessToken, [property: System.Text.Json.Serialization.JsonPropertyName("token_type")] string TokenType, [property: System.Text.Json.Serialization.JsonPropertyName("expires_in")] int ExpiresIn, string Scope);
}

using System.Net.Http.Json;
using Vairas.Delegation;
using Vairas.McpServer.Business;
using Vairas.McpServer.Configuration.Options;
namespace Vairas.McpServer.Integrations;

public sealed class DelegatedTokens(IHttpClientFactory clients, DependencyOptions options)
{
    public async Task<string> ForOwnerAsync(McpActor actor, string owner, string scope, CancellationToken ct)
    {
        if (!ReadPreviewDelegation.ResourceScopes(owner).Contains(scope, StringComparer.Ordinal) || !options.Resources.TryGetValue(owner, out var resource)) throw McpErrors.Invalid("TOOL_NOT_ALLOWED");
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(options.AuthBaseUrl), "connect/token"))
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = ReadPreviewDelegation.Grant, ["client_id"] = options.ClientId, ["client_secret"] = options.ClientSecret,
                ["subject_token"] = actor.UserBearer, ["subject_token_type"] = ReadPreviewDelegation.AccessTokenType,
                ["resource"] = resource, ["scope"] = scope, ["school_id"] = actor.SchoolId.ToString("D")
            })
        };
        using var response = await clients.CreateClient("owners").SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) throw McpErrors.Dependency("DELEGATION_REJECTED");
        var reply = await response.Content.ReadFromJsonAsync<Reply>(cancellationToken: ct);
        if (reply is null || reply.TokenType != "Bearer" || reply.Scope != scope || reply.ExpiresIn is < 1 or > 60 || reply.IssuedTokenType != ReadPreviewDelegation.AccessTokenType || string.IsNullOrWhiteSpace(reply.AccessToken)) throw McpErrors.Dependency("DELEGATION_REJECTED");
        return reply.AccessToken;
    }
    private sealed record Reply([property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string AccessToken, [property: System.Text.Json.Serialization.JsonPropertyName("issued_token_type")] string IssuedTokenType, [property: System.Text.Json.Serialization.JsonPropertyName("token_type")] string TokenType, [property: System.Text.Json.Serialization.JsonPropertyName("expires_in")] int ExpiresIn, string Scope);
}

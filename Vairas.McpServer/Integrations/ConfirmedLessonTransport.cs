using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Vairas.ConfirmedActions;
using Vairas.McpServer.Business;
using Vairas.McpServer.Configuration.Options;
namespace Vairas.McpServer.Integrations;

public sealed class ConfirmedLessonTransport(IHttpClientFactory clients, DependencyOptions options, McpOptions settings, OwnerTransport transport, TimeProvider clock)
{
    public async Task<LessonActionReceipt> CallAsync(string tool, LessonAction action, ClaimsPrincipal principal, string bearer, CancellationToken ct)
    {
        LessonActions.Validate(action); var b = action.Binding;
        var recovery = tool == "resolve_lesson_action";
        string token;
        string origin;
        string path;
        if (recovery)
        {
            if (principal.FindFirstValue("sub") != "chat-backend" || !principal.HasClaim("scope", LessonActions.RecoveryScope)) throw new ForbiddenException("RECOVERY_DENIED");
            token = await TokenAsync(new() { ["grant_type"] = "client_credentials", ["scope"] = LessonActions.OwnerRecoveryScope }, ct);
            origin = options.Resources["scheduling"]; path = "internal/v1/lesson-actions/resolve";
        }
        else
        {
            if (!settings.ConfirmedLessonActionsEnabled) throw McpErrors.Dependency("LESSON_ACTIONS_DISABLED");
            if (LessonActions.Parse(principal, 1) != b || tool != LessonActions.Tool(b.Kind)) throw new ForbiddenException("ACTION_BINDING_MISMATCH");
            var session = await transport.SendAsync(options.AuthBaseUrl, "internal/v1/sessions/validate", HttpMethod.Post, new { identityId = b.IdentityId, sessionId = b.SessionId }, null, ct);
            if (!session.GetProperty("active").GetBoolean() || session.GetProperty("identityId").GetGuid() != b.IdentityId || session.GetProperty("sessionId").GetGuid() != b.SessionId || session.GetProperty("validUntil").GetDateTimeOffset() <= clock.GetUtcNow()) throw new UnauthorizedException("SESSION_INVALID");
            var access = await transport.SendAsync(options.SchoolBaseUrl, "internal/v1/assistant/access", HttpMethod.Post, new { schoolId = b.SchoolId, actorUserId = b.UserId }, null, ct);
            if (!access.GetProperty("allowed").GetBoolean() || access.GetProperty("schoolId").GetGuid() != b.SchoolId || access.GetProperty("actorUserId").GetGuid() != b.UserId || access.GetProperty("validUntil").GetDateTimeOffset() <= clock.GetUtcNow()) throw McpErrors.Missing();
            token = await TokenAsync(new() { ["grant_type"] = "urn:ietf:params:oauth:grant-type:token-exchange", ["subject_token"] = bearer, ["subject_token_type"] = "urn:ietf:params:oauth:token-type:access_token", ["resource"] = options.Resources["scheduling"], ["scope"] = LessonActions.Scope(b.Kind), ["school_id"] = b.SchoolId.ToString("D") }, ct);
            origin = options.BusinessApiBaseUrl; path = b.Kind == "Booking" ? "api/v1/practical-lessons/confirmed-bookings" : "api/v1/practical-lessons/confirmed-reschedulings";
        }
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(origin), path)) { Content = JsonContent.Create(action, options: LessonActions.Json) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token); request.Headers.Add("Idempotency-Key", b.OperationId.ToString("D"));
        using var response = await clients.CreateClient("owners").SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        // Rejected commands are typed owner receipts, not a transport failure.
        if (!response.IsSuccessStatusCode && (int)response.StatusCode != 409) throw McpErrors.Dependency("EXECUTION_OUTCOME_UNKNOWN");
        await using var stream = await response.Content.ReadAsStreamAsync(ct); using var bytes = new MemoryStream();
        var buffer = new byte[4096]; int read;
        while ((read = await stream.ReadAsync(buffer, ct)) > 0) { if (bytes.Length + read > 32768) throw McpErrors.Dependency("OWNER_RESPONSE_TOO_LARGE"); bytes.Write(buffer, 0, read); }
        var receipt = JsonSerializer.Deserialize<LessonActionReceipt>(bytes.ToArray(), LessonActions.Json) ?? throw McpErrors.Dependency("OWNER_RESPONSE_INVALID");
        if (receipt.OperationId != b.OperationId || receipt.Status is not ("Committed" or "Rejected" or "Unknown") || receipt.Status == "Committed" && (receipt.LessonId is null || receipt.Version is null or < 1)) throw McpErrors.Dependency("OWNER_RESPONSE_INVALID");
        return receipt;
    }
    private async Task<string> TokenAsync(Dictionary<string, string> fields, CancellationToken ct)
    {
        fields["client_id"] = options.ClientId; fields["client_secret"] = options.ClientSecret;
        using var response = await clients.CreateClient("owners").PostAsync(new Uri(new Uri(options.AuthBaseUrl), "connect/token"), new FormUrlEncodedContent(fields), ct);
        if (!response.IsSuccessStatusCode) throw McpErrors.Dependency("DELEGATION_REJECTED");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)); var root = json.RootElement;
        if (root.GetProperty("token_type").GetString() != "Bearer" || root.GetProperty("scope").GetString() != fields["scope"] || root.GetProperty("expires_in").GetInt32() is < 1 or > 3600) throw McpErrors.Dependency("DELEGATION_REJECTED");
        if (fields["grant_type"] != "client_credentials" && (root.GetProperty("expires_in").GetInt32() > 60 || root.GetProperty("issued_token_type").GetString() != "urn:ietf:params:oauth:token-type:access_token")) throw McpErrors.Dependency("DELEGATION_REJECTED");
        return root.GetProperty("access_token").GetString() ?? throw McpErrors.Dependency("DELEGATION_REJECTED");
    }
}

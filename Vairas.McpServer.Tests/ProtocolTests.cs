using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Vairas.McpServer.Business;
using Xunit;
namespace Vairas.McpServer.Tests;
[Trait("Category", "Http")]
public sealed class ProtocolTests
{
    [Fact]
    public async Task Duplicate_JSON_and_server_deadline_are_enforced_before_disclosure()
    {
        await using var factory = new McpFactory { ToolSeconds = 1 }; using var http = factory.Http(); await using var client = await factory.Sdk(http);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp"); request.Headers.Authorization = new("Bearer", factory.Token()); request.Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/call\",\"params\":{\"name\":\"get_my_enrollments\",\"arguments\":{\"x\":1,\"x\":2}}}", System.Text.Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); Assert.Equal(0, factory.Owners.Reads);
        factory.Owners.DuringRead = ct => Task.Delay(Timeout.Infinite, ct);
        var result = await client.CallToolAsync("get_my_enrollments"); Assert.True(result.IsError); Assert.Null(result.StructuredContent); Assert.Contains("TOOL_TIMEOUT", result.Content.OfType<TextContentBlock>().Single().Text);
    }
    [Fact]
    public async Task Actual_SDK_discovers_exactly_six_stateless_read_preview_tools_and_structured_results()
    {
        await using var factory = new McpFactory(); using var http = factory.Http(); await using var client = await factory.Sdk(http);
        var tools = await client.ListToolsAsync(); Assert.Equal(6, tools.Count);
        Assert.Equal(ToolCatalog.All.Select(t => t.Name).Order(), tools.Select(t => t.Name).Order());
        Assert.All(tools, t => { Assert.True(t.ProtocolTool.Annotations!.ReadOnlyHint); Assert.False(t.ProtocolTool.Annotations.DestructiveHint); Assert.NotNull(t.ProtocolTool.OutputSchema); });
        var result = await client.CallToolAsync("get_my_enrollments"); Assert.False(result.IsError == true); Assert.NotNull(result.StructuredContent); Assert.Equal(0, result.StructuredContent!.Value.GetProperty("enrollments").GetArrayLength()); Assert.Equal(2, factory.Owners.Admissions);
        if (Environment.GetEnvironmentVariable("VAIRAS_MCP_EXPORT_CONTRACTS") == "on")
        {
            var directory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../api")); Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, "tools.json"), JsonSerializer.Serialize(new { toolContractVersion = "chat-tools/1", executionProfile = "PreviewOnlyV1", tools = tools.Select(t => t.ProtocolTool) }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
    [Theory][InlineData("audience")][InlineData("actor")][InlineData("binding")]
    public async Task Wrong_audience_actor_and_duplicate_identity_binding_are_denied_before_dispatch(string variant)
    {
        await using var factory = new McpFactory(); using var http = factory.Http(); using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp"); request.Headers.Authorization = new("Bearer", factory.Token(variant)); request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request); Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode); Assert.Equal(0, factory.Owners.Reads);
    }
    [Theory][InlineData("scope")][InlineData("school")][InlineData("extra")][InlineData("missing")][InlineData("unknown")][InlineData("revoked")][InlineData("membership")]
    public async Task Tool_errors_are_safe_and_never_execute_a_business_command(string variant)
    {
        await using var factory = new McpFactory(); using var http = factory.Http();
        if (variant == "revoked") factory.Owners.Active = false; if (variant == "membership") factory.Owners.Allowed = false;
        await using var client = await factory.Sdk(http, factory.Token(scope: variant == "scope" ? "mcp.context.read" : null));
        var name = variant == "unknown" ? "create_lesson_booking" : variant == "missing" ? "get_my_lessons" : "get_my_enrollments";
        var args = variant switch { "school" => new Dictionary<string, object?> { ["schoolId"] = Guid.NewGuid().ToString() }, "extra" => new Dictionary<string, object?> { ["url"] = "private-sentinel" }, _ => new Dictionary<string, object?>() };
        var result = await client.CallToolAsync(name, args); Assert.True(result.IsError); Assert.Null(result.StructuredContent); Assert.DoesNotContain("private-sentinel", result.Content.OfType<TextContentBlock>().Single().Text); Assert.Equal(0, factory.Owners.Reads);
    }
    [Fact]
    public async Task Access_change_during_owner_read_prevents_disclosure_and_cancellation_reaches_owner()
    {
        await using var factory = new McpFactory(); using var http = factory.Http(); await using var client = await factory.Sdk(http);
        factory.Owners.DuringRead = _ => { factory.Owners.Stamp = new('b', 64); return Task.CompletedTask; };
        var result = await client.CallToolAsync("get_my_enrollments"); Assert.True(result.IsError);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        factory.Owners.DuringRead = async ct => { try { await Task.Delay(Timeout.Infinite, ct); } finally { cancelled.TrySetResult(); } };
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.CallToolAsync("get_my_enrollments", cancellationToken: timeout.Token).AsTask());
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }
    [Fact]
    public async Task Browser_origin_anonymous_and_legacy_SSE_do_not_offer_an_alternate_entry()
    {
        await using var factory = new McpFactory(); using var http = factory.Http();
        using var anonymous = await http.PostAsync("/mcp", new StringContent("{}", System.Text.Encoding.UTF8, "application/json")); Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp"); request.Headers.Add("Origin", "https://localhost:4200"); request.Headers.Authorization = new("Bearer", factory.Token()); request.Content = new StringContent("{}");
        using var origin = await http.SendAsync(request); Assert.Equal(HttpStatusCode.Forbidden, origin.StatusCode);
        using var legacy = await http.GetAsync("/sse"); Assert.Equal(HttpStatusCode.NotFound, legacy.StatusCode);
    }
}

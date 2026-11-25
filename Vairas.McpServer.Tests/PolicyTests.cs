using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Vairas.Delegation;
using Xunit;
namespace Vairas.McpServer.Tests;

[Trait("Category", "Unit")]
public sealed class PolicyTests
{
    private readonly Guid school = Guid.NewGuid();
    private DefaultHttpContext Context(string owner, string scope, string path, string method = "GET")
    {
        var http = new DefaultHttpContext(); http.Request.Path = path; http.Request.Method = method;
        http.User = new(new ClaimsIdentity(new[] { new Claim("sub", Guid.NewGuid().ToString()), new("sid", Guid.NewGuid().ToString()), new("school_id", school.ToString()), new("scope", scope), new("vairas_delegation", "ReadPreviewV1"), new("aud", "vairas-" + owner), new("act", "{\"sub\":\"mcp-server\",\"act\":{\"sub\":\"chat-backend\"}}"), new("exp", DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds().ToString()) }, "synthetic"));
        return http;
    }
    [Theory]
    [InlineData("user-management", "mcp.context.read", "/api/v1/users/me")]
    [InlineData("school-management", "mcp.get_my_enrollments", "/api/v1/users/me/enrollments")]
    [InlineData("scheduling", "mcp.get_my_lessons", "/api/v1/users/me/lessons")]
    [InlineData("learning-tracking", "mcp.get_student_training_progress", "/api/v1/student-trainings/11111111-1111-4111-8111-111111111111/progress")]
    public async Task Only_exact_read_routes_and_bound_school_are_permitted(string owner, string scope, string path)
    {
        var http = Context(owner, scope, path); http.Request.QueryString = new("?schoolId=" + school);
        Assert.True(await ReadPreviewDelegation.AllowedAsync(http, owner, true));
        http.Request.Path = path + "/unpublished"; Assert.False(await ReadPreviewDelegation.AllowedAsync(http, owner, true));
    }
    [Theory][InlineData("scheduling", "/api/v1/practical-lessons")][InlineData("scheduling", "/api/v1/practical-lessons/11111111-1111-4111-8111-111111111111/rescheduling")][InlineData("user-management", "/api/v1/users")][InlineData("chat", "/api/v1/chat/action-previews/11111111-1111-4111-8111-111111111111/confirmations")]
    public async Task All_business_writes_are_denied_even_with_every_read_preview_scope(string owner, string path)
    {
        var http = Context(owner, string.Join(' ', ReadPreviewDelegation.Scopes), path, "POST");
        Assert.False(await ReadPreviewDelegation.AllowedAsync(http, owner, false)); Assert.False(await ReadPreviewDelegation.AllowedAsync(http, owner, true));
    }
    [Theory][InlineData("school")][InlineData("scope")][InlineData("audience")][InlineData("actor")][InlineData("expired")][InlineData("duplicate")][InlineData("profile")]
    public async Task Invalid_delegation_cannot_bypass_owner_admission(string variant)
    {
        var http = Context("scheduling", "mcp.get_my_lessons", "/api/v1/users/me/lessons"); http.Request.QueryString = new("?schoolId=" + school);
        var id = (ClaimsIdentity)http.User.Identity!;
        switch (variant)
        {
            case "school": http.Request.QueryString = new("?schoolId=" + Guid.NewGuid()); break;
            case "scope": id.RemoveClaim(id.FindFirst("scope")); id.AddClaim(new("scope", "mcp.context.read")); break;
            case "audience": id.RemoveClaim(id.FindFirst("aud")); id.AddClaim(new("aud", "vairas-school-management")); break;
            case "actor": id.RemoveClaim(id.FindFirst("act")); id.AddClaim(new("act", "{\"sub\":\"chat-backend\"}")); break;
            case "expired": id.RemoveClaim(id.FindFirst("exp")); id.AddClaim(new("exp", "1")); break;
            case "duplicate": id.AddClaim(new("sid", Guid.NewGuid().ToString())); break;
            case "profile": id.RemoveClaim(id.FindFirst("vairas_delegation")); break;
        }
        Assert.False(await ReadPreviewDelegation.AllowedAsync(http, "scheduling", true));
    }
    [Fact]
    public async Task Preview_POST_checks_the_owner_body_and_preserves_it_for_model_binding()
    {
        var http = Context("scheduling", "mcp.preview_lesson_booking", "/api/v1/practical-lessons/booking-previews", "POST");
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{\"schoolId\":\"" + school + "\"}"));
        Assert.True(await ReadPreviewDelegation.AllowedAsync(http, "scheduling", true)); Assert.Equal(0, http.Request.Body.Position);
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{\"schoolId\":\"" + Guid.NewGuid() + "\"}"));
        Assert.True(await ReadPreviewDelegation.AllowedAsync(http, "scheduling", false)); Assert.False(await ReadPreviewDelegation.AllowedAsync(http, "scheduling", true));
    }
    [Fact]
    public async Task Availability_uses_signed_school_from_path_and_normal_users_keep_existing_authorization()
    {
        var http = Context("scheduling", "mcp.find_available_lessons", $"/api/v1/schools/{school}/availability"); Assert.True(await ReadPreviewDelegation.AllowedAsync(http, "scheduling", true));
        http.Request.Path = $"/api/v1/schools/{Guid.NewGuid()}/availability"; Assert.False(await ReadPreviewDelegation.AllowedAsync(http, "scheduling", true));
        http.User = new(new ClaimsIdentity([new("sub", Guid.NewGuid().ToString())], "user")); Assert.True(await ReadPreviewDelegation.AllowedAsync(http, "scheduling", true));
    }
}

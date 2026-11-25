using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Vairas.ConfirmedActions;
using Vairas.Delegation;
using Xunit;
namespace Vairas.McpServer.Tests;

[Trait("Category","Unit")]
public sealed class ConfirmedActionPolicyTests
{
    private static (DefaultHttpContext Http,LessonAction Action) Request(string kind)
    {
        var args=JsonSerializer.SerializeToElement(new {schoolId=Guid.NewGuid(),reason="Точный перенос"});var source=JsonSerializer.SerializeToElement(new {revision=1});
        var b=new LessonActionBinding(args.GetProperty("schoolId").GetGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),kind,new('a',64),LessonActions.Digest(kind,args,source));
        var http=new DefaultHttpContext();http.Request.Method="POST";http.Request.Path=kind=="Booking"?"/api/v1/practical-lessons/confirmed-bookings":"/api/v1/practical-lessons/confirmed-reschedulings";
        http.User=new(new ClaimsIdentity(new[] {new Claim("sub",b.IdentityId.ToString()),new("sid",b.SessionId.ToString()),new("school_id",b.SchoolId.ToString()),new("scope",LessonActions.Scope(kind)),new("vairas_delegation",LessonActions.Profile),new("aud","vairas-scheduling"),new("act","{\"sub\":\"mcp-server\",\"act\":{\"sub\":\"chat-backend\"}}"),new("exp",DateTimeOffset.UtcNow.AddSeconds(30).ToUnixTimeSeconds().ToString()),new(LessonActions.Claim,JsonSerializer.Serialize(b,LessonActions.Json))},"fixture"));
        var action=new LessonAction(b,args,source);http.Request.Body=new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(action,LessonActions.Json));http.Request.Headers["Idempotency-Key"]=b.OperationId.ToString();return(http,action);
    }
    [Theory][InlineData("Booking")][InlineData("Rescheduling")]
    public async Task Only_exact_bound_command_reaches_owner_and_body_is_preserved(string kind)
    {
        var (http,_)=Request(kind);Assert.True(await ReadPreviewDelegation.AllowedAsync(http,"scheduling",false));Assert.True(await ReadPreviewDelegation.AllowedAsync(http,"scheduling",true));Assert.Equal(0,http.Request.Body.Position);
        http.Request.Path="/api/v1/practical-lessons";Assert.False(await ReadPreviewDelegation.AllowedAsync(http,"scheduling",true));
    }
    [Theory][InlineData("body")][InlineData("operation")][InlineData("actor")][InlineData("recipient")][InlineData("scope")][InlineData("read")]
    public async Task Execution_token_does_not_authorize_other_payloads_routes_or_profiles(string change)
    {
        var (http,action)=Request("Booking");var identity=(ClaimsIdentity)http.User.Identity!;
        switch(change)
        {
            case "body":http.Request.Body=new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(action with {Arguments=JsonSerializer.SerializeToElement(new {schoolId=Guid.NewGuid()})},LessonActions.Json));break;
            case "operation":http.Request.Headers["Idempotency-Key"]=Guid.NewGuid().ToString();break;
            case "actor":identity.RemoveClaim(identity.FindFirst("act"));identity.AddClaim(new("act","{\"sub\":\"chat-backend\"}"));break;
            case "recipient":identity.RemoveClaim(identity.FindFirst("aud"));identity.AddClaim(new("aud","vairas-user-management"));break;
            case "scope":identity.RemoveClaim(identity.FindFirst("scope"));identity.AddClaim(new("scope","mcp.reschedule_lesson_booking"));break;
            case "read":identity.RemoveClaim(identity.FindFirst("vairas_delegation"));identity.AddClaim(new("vairas_delegation","ReadPreviewV1"));break;
        }
        Assert.False(await ReadPreviewDelegation.AllowedAsync(http,"scheduling",true));
    }
}

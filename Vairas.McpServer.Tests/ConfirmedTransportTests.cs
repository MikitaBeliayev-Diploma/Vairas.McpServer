using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Vairas.ConfirmedActions;
using Vairas.McpServer.Business;
using Vairas.McpServer.Integrations;
using Xunit;
using Vairas.McpServer.Configuration.Options;
namespace Vairas.McpServer.Tests;

[Trait("Category","Unit")]
public sealed class ConfirmedTransportTests
{
    private sealed class Tokens : IWorkloadTokens { public Task<string> GetAsync(CancellationToken ct)=>Task.FromResult("workload-admission-only"); }
    private sealed class TransportFixture : HttpMessageHandler,IHttpClientFactory
    {
        public LessonAction Action {get;}
        public string Variant="";
        public int Admissions,Commands;
        public Dictionary<string,string>? TokenFields;
        public string? OwnerBearer;
        public TransportFixture(string kind="Booking")
        {
            var arguments=JsonSerializer.SerializeToElement(new {schoolId=Guid.NewGuid()});var source=JsonSerializer.SerializeToElement(new {revision=1});
            Action=new(new(arguments.GetProperty("schoolId").GetGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),kind,new('a',64),LessonActions.Digest(kind,arguments,source)),arguments,source);
        }
        public HttpClient CreateClient(string name)=>new(this,false);
        public ClaimsPrincipal Principal(bool recovery=false)
        {
            var b=Action.Binding;
            return new(new ClaimsIdentity(recovery?[new Claim("sub","chat-backend"),new("scope",LessonActions.RecoveryScope)]:new[] {new Claim("sub",b.IdentityId.ToString()),new("sid",b.SessionId.ToString()),new("school_id",b.SchoolId.ToString()),new("scope",LessonActions.Scope(b.Kind)),new("vairas_delegation",LessonActions.Profile),new(LessonActions.Claim,JsonSerializer.Serialize(b,LessonActions.Json)),new("act","{\"sub\":\"chat-backend\"}"),new("exp",DateTimeOffset.UtcNow.AddSeconds(30).ToUnixTimeSeconds().ToString())},"fixture"));
        }
        public ConfirmedLessonTransport Service(bool enabled=true)
        {
            var options=new DependencyOptions{ClientSecret="synthetic-secret"};return new(this,options,new(){ConfirmedLessonActionsEnabled=enabled},new(this,new Tokens(),new HttpContextAccessor()),TimeProvider.System);
        }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            var b=Action.Binding;var path=request.RequestUri!.AbsolutePath;
            if(path=="/connect/token")
            {
                TokenFields=(await request.Content!.ReadAsStringAsync(ct)).Split('&').Select(p=>p.Split('=',2)).ToDictionary(p=>Uri.UnescapeDataString(p[0]),p=>Uri.UnescapeDataString(p[1].Replace('+',' ')));
                return new(HttpStatusCode.OK){Content=JsonContent.Create(new {access_token="owner-specific-token",token_type=Variant=="token"?"invalid":"Bearer",scope=TokenFields["scope"],expires_in=30,issued_token_type="urn:ietf:params:oauth:token-type:access_token"})};
            }
            if(path=="/internal/v1/sessions/validate")
            {Admissions++;return new(HttpStatusCode.OK){Content=JsonContent.Create(new{identityId=b.IdentityId,sessionId=b.SessionId,active=Variant!="session",validUntil=DateTimeOffset.UtcNow.AddSeconds(30)})};}
            if(path=="/internal/v1/assistant/access")
            {Admissions++;return new(HttpStatusCode.OK){Content=JsonContent.Create(new{schoolId=b.SchoolId,actorUserId=b.UserId,allowed=Variant!="school",validUntil=DateTimeOffset.UtcNow.AddSeconds(30)})};}
            Commands++;OwnerBearer=request.Headers.Authorization?.Parameter;
            Assert.Equal(b.OperationId.ToString(),request.Headers.GetValues("Idempotency-Key").Single());
            var actual=await request.Content!.ReadFromJsonAsync<LessonAction>(LessonActions.Json,ct);Assert.Equal(LessonActions.Hash(Action.Arguments),LessonActions.Hash(actual!.Arguments));Assert.Equal(b,actual.Binding);
            if(Variant=="unavailable")return new(HttpStatusCode.ServiceUnavailable);
            if(Variant=="large")return new(HttpStatusCode.OK){Content=new StringContent(new string('x',32769))};
            return new(Variant=="rejected"?HttpStatusCode.Conflict:HttpStatusCode.OK){Content=JsonContent.Create(new LessonActionReceipt(Variant=="operation"?Guid.NewGuid():b.OperationId,Variant=="rejected"?"Rejected":"Committed",Variant=="rejected"?null:Guid.NewGuid(),Variant=="rejected"?null:1,"OWNER_OUTCOME",Variant=="rejected"?409:b.Kind=="Booking"?201:200))};
        }
    }
    [Theory][InlineData("Booking")][InlineData("Rescheduling")]
    public async Task Exact_command_uses_two_fresh_admission_checks_and_a_distinct_owner_token(string kind)
    {
        using var fixture=new TransportFixture(kind);
        var receipt=await fixture.Service().CallAsync(LessonActions.Tool(kind),fixture.Action,fixture.Principal(),"mcp-specific-token",default);
        Assert.Equal("Committed",receipt.Status);Assert.Equal(2,fixture.Admissions);Assert.Equal(1,fixture.Commands);Assert.Equal("owner-specific-token",fixture.OwnerBearer);
        Assert.Equal("mcp-specific-token",fixture.TokenFields!["subject_token"]);Assert.Equal("https://localhost:7300/",fixture.TokenFields["resource"]);
    }
    [Fact]
    public async Task Recovery_uses_only_its_workload_scope_and_remains_available_with_execution_disabled()
    {
        using var fixture=new TransportFixture();await fixture.Service(false).CallAsync("resolve_lesson_action",fixture.Action,fixture.Principal(true),"recovery-token",default);
        Assert.Equal(0,fixture.Admissions);Assert.Equal("client_credentials",fixture.TokenFields!["grant_type"]);Assert.Equal(LessonActions.OwnerRecoveryScope,fixture.TokenFields["scope"]);
        Assert.False(fixture.TokenFields.ContainsKey("subject_token"));
    }
    [Theory][InlineData("session")][InlineData("school")][InlineData("token")][InlineData("unavailable")][InlineData("operation")][InlineData("large")]
    public async Task Admission_transport_and_receipt_failures_never_fabricate_success(string failure)
    {
        using var fixture=new TransportFixture{Variant=failure};await Assert.ThrowsAnyAsync<Exception>(()=>fixture.Service().CallAsync("create_lesson_booking",fixture.Action,fixture.Principal(),"mcp-token",default));
        if(failure is "session" or "school" or "token")Assert.Equal(0,fixture.Commands);
    }
    [Fact]
    public async Task Rejection_is_a_typed_owner_receipt_and_execution_switch_and_tool_binding_fail_closed()
    {
        using var fixture=new TransportFixture{Variant="rejected"};Assert.Equal("Rejected",(await fixture.Service().CallAsync("create_lesson_booking",fixture.Action,fixture.Principal(),"mcp-token",default)).Status);
        await Errors.ThrowsBusinessAsync(()=>fixture.Service(false).CallAsync("create_lesson_booking",fixture.Action,fixture.Principal(),"mcp-token",default));
        await Errors.ThrowsBusinessAsync(()=>fixture.Service().CallAsync("reschedule_lesson_booking",fixture.Action,fixture.Principal(),"mcp-token",default));
        await Errors.ThrowsBusinessAsync(()=>fixture.Service().CallAsync("resolve_lesson_action",fixture.Action,fixture.Principal(),"mcp-token",default));
    }
}

using System.Globalization;
using System.Text.Json;
using Vairas.McpServer.Business;
using Vairas.McpServer.Configuration.Options;

namespace Vairas.McpServer.Integrations;

public sealed class McpOwners(OwnerTransport transport, DependencyOptions options, DelegatedTokens delegated, TimeProvider clock) : IMcpOwners
{
    public async Task<McpActor> ReadmitAsync(McpActor actor, CancellationToken ct)
    {
        var start = clock.GetUtcNow();
        var session = await transport.SendAsync(options.AuthBaseUrl, "internal/v1/sessions/validate", HttpMethod.Post, new { identityId = actor.IdentityId, sessionId = actor.SessionId }, null, ct);
        if (session.GetProperty("identityId").GetGuid() != actor.IdentityId || session.GetProperty("sessionId").GetGuid() != actor.SessionId) throw McpErrors.Dependency("IDENTITY_BINDING_INVALID");
        if (!session.GetProperty("active").GetBoolean()) throw new UnauthorizedException("SESSION_INVALID");
        var until = session.GetProperty("validUntil").GetDateTimeOffset(); McpRules.Fresh(until, clock.GetUtcNow());
        var profile = await transport.SendAsync(options.BusinessApiBaseUrl, "api/v1/users/me", HttpMethod.Get, null, await delegated.ForOwnerAsync(actor, "user-management", "mcp.context.read", ct), ct, concealForbidden: false);
        var user = profile.GetProperty("userId").GetGuid();
        if (user == Guid.Empty || actor.UserId != Guid.Empty && actor.UserId != user) throw McpErrors.Dependency("IDENTITY_BINDING_INVALID");
        if (profile.GetProperty("status").GetString() != "Active") throw new ForbiddenException("PROFILE_INACTIVE");
        return actor with { UserId = user, ValidUntil = McpRules.Instant(until < start.AddSeconds(30) ? until : start.AddSeconds(30)) };
    }
    public async Task<SchoolAccess> AccessAsync(Guid school, McpActor actor, CancellationToken ct)
    {
        var result = await transport.SendAsync(options.SchoolBaseUrl, "internal/v1/assistant/access", HttpMethod.Post, new { schoolId = school, actorUserId = actor.UserId }, null, ct);
        return result.Deserialize<SchoolAccess>(Wire.Json) ?? throw McpErrors.Dependency("OWNER_RESPONSE_INVALID");
    }
    public async Task<JsonElement> ReadToolAsync(string name, JsonElement arguments, Guid school, McpActor actor, CancellationToken ct)
    {
        var args = ToolCatalog.Normalize(name, arguments, school);
        var owner = name switch { "get_my_enrollments" => "school-management", "get_student_training_progress" => "learning-tracking", "get_my_lessons" or "find_available_lessons" => "scheduling", _ => throw McpErrors.Invalid("TOOL_NOT_ALLOWED") };
        actor = actor with { UserBearer = await delegated.ForOwnerAsync(actor, owner, "mcp." + name, ct) };
        string Query(string key) => Uri.EscapeDataString(args.GetProperty(key).ToString());
        if (name == "get_my_enrollments")
        {
            var result = await transport.SendAsync(options.BusinessApiBaseUrl, "api/v1/users/me/enrollments", HttpMethod.Get, null, actor.UserBearer, ct);
            return Wire.Element(new { enrollments = result.EnumerateArray().Where(e => e.GetProperty("schoolId").GetGuid() == school).Take(50).ToArray() });
        }
        if (name == "get_my_lessons")
        {
            var result = await transport.SendAsync(options.BusinessApiBaseUrl, $"api/v1/users/me/lessons?schoolId={school:D}&from={Query("from")}&to={Query("to")}", HttpMethod.Get, null, actor.UserBearer, ct);
            return Wire.Element(new { lessons = result.EnumerateArray().Where(e => e.GetProperty("schoolId").GetGuid() == school).Take(50).ToArray() });
        }
        if (name == "get_student_training_progress")
        {
            var enrollment = args.GetProperty("enrollmentId").GetGuid();
            return await transport.SendAsync(options.BusinessApiBaseUrl, $"api/v1/student-trainings/{enrollment:D}/progress?schoolId={school:D}", HttpMethod.Get, null, actor.UserBearer, ct);
        }
        if (name == "find_available_lessons")
        {
            var query = string.Join('&', args.EnumerateObject().Where(p => p.Name != "schoolId").Select(p => Uri.EscapeDataString(p.Name) + "=" + Uri.EscapeDataString(p.Value.ToString())));
            return await transport.SendAsync(options.BusinessApiBaseUrl, $"api/v1/schools/{school:D}/availability?{query}", HttpMethod.Get, null, actor.UserBearer, ct);
        }
        throw McpErrors.Invalid("TOOL_NOT_ALLOWED");
    }
    public async Task<OwnerPreview> PreviewAsync(string name, JsonElement arguments, Guid school, McpActor actor, CancellationToken ct)
    {
        var args = ToolCatalog.Normalize(name, arguments, school);
        actor = actor with { UserBearer = await delegated.ForOwnerAsync(actor, "scheduling", "mcp." + name, ct) };
        JsonElement result;
        if (name == "preview_lesson_booking")
        {
            var body = new { schoolId = school, branchId = args.GetProperty("branchId").GetGuid(), enrollmentId = args.GetProperty("enrollmentId").GetGuid(), studentId = args.GetProperty("studentId").GetGuid(), teacherId = args.GetProperty("teacherId").GetGuid(), vehicleId = args.GetProperty("vehicleId").GetGuid(), category = args.GetProperty("category").GetString(), startsAt = args.GetProperty("startsAt").GetDateTimeOffset().ToUniversalTime(), endsAt = args.GetProperty("endsAt").GetDateTimeOffset().ToUniversalTime() };
            result = await transport.SendAsync(options.BusinessApiBaseUrl, "api/v1/practical-lessons/booking-previews", HttpMethod.Post, body, actor.UserBearer, ct);
        }
        else if (name == "preview_lesson_rescheduling")
        {
            var lesson = args.GetProperty("lessonId").GetGuid();
            var body = new { expectedVersion = args.GetProperty("expectedVersion").GetInt32(), startsAt = args.GetProperty("startsAt").GetDateTimeOffset().ToUniversalTime(), endsAt = args.GetProperty("endsAt").GetDateTimeOffset().ToUniversalTime(), reason = args.GetProperty("reason").GetString() };
            result = await transport.SendAsync(options.BusinessApiBaseUrl, $"api/v1/practical-lessons/{lesson:D}/rescheduling-previews?schoolId={school:D}", HttpMethod.Post, body, actor.UserBearer, ct);
            if (result.GetProperty("arguments").GetProperty("enrollmentId").GetGuid() != args.GetProperty("enrollmentId").GetGuid()) throw McpErrors.Missing();
        }
        else throw McpErrors.Invalid("TOOL_NOT_ALLOWED");
        if (result.GetProperty("arguments").GetProperty("schoolId").GetGuid() != school) throw McpErrors.Dependency("OWNER_PREVIEW_INVALID");
        return result.Deserialize<OwnerPreview>(Wire.Json) ?? throw McpErrors.Dependency("OWNER_PREVIEW_INVALID");
    }
}

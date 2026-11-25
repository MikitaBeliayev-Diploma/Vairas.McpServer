using System.Text.Json;
using Vairas.McpServer.Business;
using Xunit;
namespace Vairas.McpServer.Tests;

[Trait("Category", "Unit")]
public sealed class BusinessTests
{
    private static McpActor Actor(Guid school) => new(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, "synthetic-not-persisted", DateTimeOffset.MinValue, school, ToolCatalog.All.Select(t => "mcp." + t.Name).ToArray());
    internal static JsonElement Arguments(string name)
    {
        var id = Guid.NewGuid(); var starts = DateTimeOffset.UtcNow.AddDays(1);
        return name switch
        {
            "get_my_enrollments" => Wire.Element(new { }),
            "get_my_lessons" => Wire.Element(new { from = starts, to = starts.AddDays(1) }),
            "get_student_training_progress" => Wire.Element(new { enrollmentId = id }),
            "find_available_lessons" => Wire.Element(new { branchId = id, teacherId = id, category = "B", from = starts, to = starts.AddHours(1), durationMinutes = 45 }),
            "preview_lesson_booking" => Wire.Element(new { branchId = id, enrollmentId = id, studentId = id, teacherId = id, vehicleId = id, category = "B", startsAt = starts, endsAt = starts.AddMinutes(45) }),
            _ => Wire.Element(new { lessonId = id, enrollmentId = id, expectedVersion = 1, startsAt = starts, endsAt = starts.AddMinutes(45), reason = "Synthetic advisory move" })
        };
    }
    [Theory][InlineData("get_my_enrollments")][InlineData("get_my_lessons")][InlineData("get_student_training_progress")][InlineData("find_available_lessons")][InlineData("preview_lesson_booking")][InlineData("preview_lesson_rescheduling")]
    public async Task Every_tool_enforces_two_fresh_admissions_and_returns_its_owner_schema(string name)
    {
        var owners = new FakeOwners(); var executor = new ToolExecutor(owners, TimeProvider.System); var actor = Actor(Guid.NewGuid());
        var result = await executor.ExecuteAsync(name, Arguments(name), actor, default);
        Assert.True(OutputContracts.Valid(name, result)); Assert.Equal(2, owners.Admissions); Assert.Equal(1, owners.Reads);
        if (name == "get_student_training_progress") { Assert.Equal(JsonValueKind.Null, result.GetProperty("percent").ValueKind); Assert.False(result.GetProperty("sourcesVerified").GetBoolean()); }
    }
    [Theory][InlineData("scope")][InlineData("school")][InlineData("membership")][InlineData("session")][InlineData("schema")][InlineData("changed")]
    public async Task Missing_authority_and_malformed_owner_facts_are_rejected(string variant)
    {
        var owners = new FakeOwners(); var actor = Actor(Guid.NewGuid());
        switch (variant)
        {
            case "scope": actor = actor with { Scopes = [] }; break; case "school": actor = actor with { SchoolId = Guid.Empty }; break;
            case "membership": owners.Allowed = false; break; case "session": owners.Active = false; break; case "schema": owners.InvalidResult = true; break;
            case "changed": owners.DuringRead = _ => { owners.Stamp = new('b', 64); return Task.CompletedTask; }; break;
        }
        await Errors.ThrowsBusinessAsync(() => new ToolExecutor(owners, TimeProvider.System).ExecuteAsync("get_my_enrollments", Wire.Element(new { }), actor, default));
    }
    [Theory][InlineData("uuid")][InlineData("school")][InlineData("unknown")][InlineData("type")][InlineData("version")][InlineData("interval")][InlineData("window")][InlineData("duration")][InlineData("category")][InlineData("reason")]
    public void Closed_arguments_reject_invalid_ids_scope_types_and_unbounded_queries(string variant)
    {
        var school = Guid.NewGuid(); var name = "preview_lesson_rescheduling"; var data = System.Text.Json.Nodes.JsonNode.Parse(Arguments(name).GetRawText())!.AsObject();
        switch (variant)
        {
            case "uuid": data["lessonId"] = Guid.Empty; break; case "school": data["schoolId"] = Guid.NewGuid(); break; case "unknown": data["url"] = "https://attacker.invalid/"; break; case "type": data["expectedVersion"] = "1"; break; case "version": data["expectedVersion"] = 0; break;
            case "interval": data["endsAt"] = data["startsAt"]!.GetValue<string>(); break;
            case "window": name = "get_my_lessons"; data = new() { ["from"] = DateTimeOffset.UtcNow.ToString("O"), ["to"] = DateTimeOffset.UtcNow.AddDays(32).ToString("O") }; break;
            case "duration": name = "find_available_lessons"; data = System.Text.Json.Nodes.JsonNode.Parse(Arguments(name).GetRawText())!.AsObject(); data["durationMinutes"] = 0; break;
            case "category": name = "preview_lesson_booking"; data = System.Text.Json.Nodes.JsonNode.Parse(Arguments(name).GetRawText())!.AsObject(); data["category"] = ""; break;
            case "reason": data["reason"] = new string('x', 501); break;
        }
        Errors.ThrowsBusiness(() => ToolCatalog.Normalize(name, Wire.Element(data), school));
    }
}

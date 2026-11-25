using System.Text.Json;
using System.Text.Json.Serialization;
namespace Vairas.McpServer.Business;

public sealed class ToolExecutor(IMcpOwners owners, TimeProvider clock)
{
    public async Task<JsonElement> ExecuteAsync(string name, JsonElement arguments, McpActor actor, CancellationToken ct)
    {
        var tool = ToolCatalog.Definition(name);
        if (!actor.Scopes.Contains("mcp." + name, StringComparer.Ordinal)) throw new ForbiddenException("TOOL_SCOPE_REQUIRED");
        McpRules.Id(actor.SchoolId);
        var normalized = ToolCatalog.Normalize(name, arguments, actor.SchoolId);
        actor = await owners.ReadmitAsync(actor, ct);
        var before = await owners.AccessAsync(actor.SchoolId, actor, ct); Check(before, actor);
        var result = tool.Preview ? Wire.Element(await owners.PreviewAsync(name, normalized, actor.SchoolId, actor, ct)) : await owners.ReadToolAsync(name, normalized, actor.SchoolId, actor, ct);
        if (System.Text.Encoding.UTF8.GetByteCount(result.GetRawText()) > 32768 || result.ValueKind != JsonValueKind.Object || !OutputContracts.Valid(name, result)) throw McpErrors.Dependency("TOOL_RESULT_INVALID");
        actor = await owners.ReadmitAsync(actor, ct);
        var after = await owners.AccessAsync(actor.SchoolId, actor, ct); Check(after, actor);
        if (after.AccessStamp != before.AccessStamp) throw McpErrors.Missing();
        return result;
    }
    private void Check(SchoolAccess access, McpActor actor)
    {
        if (access.SchoolId != actor.SchoolId || access.ActorUserId != actor.UserId || string.IsNullOrWhiteSpace(access.AccessStamp)) throw McpErrors.Dependency("INVALID_AUTHORITY");
        McpRules.Fresh(actor.ValidUntil, clock.GetUtcNow()); McpRules.Fresh(access.ValidUntil, clock.GetUtcNow());
        if (!access.Allowed) throw McpErrors.Missing();
    }
}

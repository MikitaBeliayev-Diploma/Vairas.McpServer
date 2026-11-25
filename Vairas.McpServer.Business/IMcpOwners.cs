using System.Text.Json;
using System.Text.Json.Serialization;
namespace Vairas.McpServer.Business;

public interface IMcpOwners
{
    Task<McpActor> ReadmitAsync(McpActor actor, CancellationToken ct);
    Task<SchoolAccess> AccessAsync(Guid school, McpActor actor, CancellationToken ct);
    Task<JsonElement> ReadToolAsync(string name, JsonElement arguments, Guid school, McpActor actor, CancellationToken ct);
    Task<OwnerPreview> PreviewAsync(string name, JsonElement arguments, Guid school, McpActor actor, CancellationToken ct);
}

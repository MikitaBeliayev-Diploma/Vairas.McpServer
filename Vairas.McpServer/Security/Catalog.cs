using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using Vairas.McpServer.Business;
namespace Vairas.McpServer.Security;

public static class Catalog
{
    public static readonly IList<Tool> Tools = ToolCatalog.All.Select(t => new Tool
    {
        Name = t.Name, Description = t.Description, InputSchema = t.Schema,
        OutputSchema = OutputContracts.Schema(t.Name),
        Annotations = new ToolAnnotations { ReadOnlyHint = true, DestructiveHint = false, IdempotentHint = true, OpenWorldHint = false },
        Meta = new JsonObject { ["toolContractVersion"] = "chat-tools/1", ["executionProfile"] = "PreviewOnlyV1" }
    }).ToList();
}

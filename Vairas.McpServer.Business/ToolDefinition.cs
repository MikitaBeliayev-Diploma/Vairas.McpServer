using System.Text.Json;
using System.Text.Json.Serialization;
namespace Vairas.McpServer.Business;

/// <summary>Closed tool definition; previews hold no resource.</summary>
public sealed record ToolDefinition(string Name, string Description, JsonElement Schema, bool Preview);

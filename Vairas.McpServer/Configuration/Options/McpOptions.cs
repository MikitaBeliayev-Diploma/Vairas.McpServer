namespace Vairas.McpServer.Configuration.Options;

/// <summary>Bound from "Mcp": token authority, exact resource (audience) and tool limits.</summary>
public sealed class McpOptions
{
    public bool ConfirmedLessonActionsEnabled { get; set; }
    public int ToolSeconds { get; set; } = 20;
    public string Authority { get; set; } = "https://localhost:7100";
    public string Resource { get; set; } = "https://localhost:7900/mcp";
}

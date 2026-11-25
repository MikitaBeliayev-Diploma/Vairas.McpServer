namespace Vairas.McpServer.Configuration.Options;

public sealed class DependencyOptions
{
    public Dictionary<string, string> Resources { get; set; } = new(StringComparer.Ordinal) { ["user-management"] = "https://localhost:7001/", ["school-management"] = "https://localhost:7200/", ["scheduling"] = "https://localhost:7300/", ["learning-tracking"] = "https://localhost:7500/" };
    public string AuthBaseUrl { get; set; } = "https://localhost:7100/";
    public string SchoolBaseUrl { get; set; } = "https://localhost:7200/";
    public string BusinessApiBaseUrl { get; set; } = "https://localhost:7000/";
    public string ClientId { get; set; } = "mcp-server";
    public string ClientSecret { get; set; } = "";
    public string Scope { get; set; } = "identity.validate school.assistant.read";
}


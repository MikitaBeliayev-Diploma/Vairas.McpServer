using System.Text.Json;
using System.Text.Json.Serialization;
namespace Vairas.McpServer.Business;

/// <summary>Native Scheduling advisory basis; Chat allocates consent separately.</summary>
public sealed record OwnerPreview(string PreviewVersion, string Kind, JsonElement Arguments, JsonElement SourceRevisions, JsonElement Display);

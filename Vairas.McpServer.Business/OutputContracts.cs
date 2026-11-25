using System.Text.Json;
using Json.Schema;
namespace Vairas.McpServer.Business;

public static class OutputContracts
{
    private static readonly Dictionary<string, JsonElement> Schemas = Load();
    private static readonly Dictionary<string, JsonSchema> Validators = Schemas.ToDictionary(p => p.Key, p => JsonSchema.Build(p.Value));
    private static Dictionary<string, JsonElement> Load()
    {
        using var stream = typeof(OutputContracts).Assembly.GetManifestResourceStream("Vairas.McpServer.Business.Contracts.tool-outputs.json")!;
        return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(stream)!;
    }
    public static JsonElement Schema(string name) => Schemas[name];
    public static bool Valid(string name, JsonElement value) => Validators[name].Evaluate(value).IsValid;
}

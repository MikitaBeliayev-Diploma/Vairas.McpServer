using System.Text.Json;
using System.Text.Json.Serialization;
namespace Vairas.McpServer.Business;

public static class Wire
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = false, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, AllowDuplicateProperties = false, Converters = { new JsonStringEnumConverter(allowIntegerValues: false) } };
    public static JsonElement Element<T>(T value) => JsonSerializer.SerializeToElement(value, Json);
    public static T Read<T>(string value) => JsonSerializer.Deserialize<T>(value, Json) ?? throw McpErrors.Invalid();
}

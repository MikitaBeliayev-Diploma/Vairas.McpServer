using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Vairas.McpServer.Business;

public static class ToolCatalog
{
    private static JsonElement Schema(string[] required, params (string Name, string Type)[] fields) => Wire.Element(new
    {
        type = "object", additionalProperties = false, required,
        properties = fields.ToDictionary(p => p.Name, p => p.Type == "integer" ? (object)new { type = "integer" } : new { type = "string" })
    });
    public static readonly IReadOnlyList<ToolDefinition> All = [
        new("get_my_enrollments", "Read my enrollments in the selected school.", Schema([]), false),
        new("get_my_lessons", "Read my own lessons in a UTC window of at most 31 days.", Schema(["from","to"], ("from","string"),("to","string")), false),
        new("get_student_training_progress", "Read owner-authorized training progress. Unknown values remain unknown.", Schema(["enrollmentId"],("enrollmentId","string")), false),
        new("find_available_lessons", "Read provisional slots. Never holds a reservation.", Schema(["branchId","teacherId","category","from","to","durationMinutes"],("schoolId","string"),("branchId","string"),("teacherId","string"),("category","string"),("from","string"),("to","string"),("durationMinutes","integer"),("enrollmentId","string"),("studentId","string"),("vehicleId","string")), false),
        new("preview_lesson_booking", "Prepare an exact advisory booking preview. Only users with the owner's booking permission can use it.", Schema(["branchId","enrollmentId","studentId","teacherId","vehicleId","category","startsAt","endsAt"],("schoolId","string"),("branchId","string"),("enrollmentId","string"),("studentId","string"),("teacherId","string"),("vehicleId","string"),("category","string"),("startsAt","string"),("endsAt","string")), true),
        new("preview_lesson_rescheduling", "Prepare an exact advisory move preview. No lesson is moved.", Schema(["lessonId","enrollmentId","expectedVersion","startsAt","endsAt","reason"],("schoolId","string"),("lessonId","string"),("enrollmentId","string"),("expectedVersion","integer"),("startsAt","string"),("endsAt","string"),("reason","string")), true)
    ];
    public static ToolDefinition Definition(string name) => All.SingleOrDefault(t => t.Name == name) ?? throw McpErrors.Invalid("TOOL_NOT_ALLOWED");
    public static JsonElement Normalize(string name, JsonElement value, Guid school)
    {
        var definition = Definition(name);
        if (value.ValueKind != JsonValueKind.Object) throw McpErrors.Invalid("INVALID_TOOL_ARGUMENTS");
        _ = McpRules.Canonical(value);
        var allowed = definition.Schema.GetProperty("properties");
        foreach (var property in value.EnumerateObject())
        {
            if (!allowed.TryGetProperty(property.Name, out var schema)) throw McpErrors.Invalid("INVALID_TOOL_ARGUMENTS");
            var type = schema.GetProperty("type").GetString();
            if (type == "string" && property.Value.ValueKind != JsonValueKind.String || type == "integer" && (property.Value.ValueKind != JsonValueKind.Number || !property.Value.TryGetInt32(out _))) throw McpErrors.Invalid("INVALID_TOOL_ARGUMENTS");
        }
        foreach (var required in definition.Schema.GetProperty("required").EnumerateArray())
            if (!value.TryGetProperty(required.GetString()!, out _)) throw McpErrors.Invalid("MISSING_TOOL_ARGUMENT");
        var normalized = JsonNode.Parse(value.GetRawText())!.AsObject();
        foreach (var property in value.EnumerateObject())
        {
            if (property.Name.EndsWith("Id", StringComparison.Ordinal))
            {
                if (!Guid.TryParse(property.Value.GetString(), out var id) || id == Guid.Empty) throw McpErrors.Invalid("INVALID_TOOL_ARGUMENTS");
                if (property.Name == "schoolId" && id != school) throw McpErrors.Missing();
                normalized[property.Name] = id.ToString("D");
            }
            if (property.Name is "from" or "to" or "startsAt" or "endsAt")
            {
                if (!DateTimeOffset.TryParse(property.Value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var time)) throw McpErrors.Invalid("INVALID_TOOL_ARGUMENTS");
                normalized[property.Name] = McpRules.Instant(time).ToString("O", CultureInfo.InvariantCulture);
            }
        }
        if (normalized["reason"] is JsonNode reason && (string.IsNullOrWhiteSpace(reason.GetValue<string>()) || reason.GetValue<string>().Length > 500)) throw McpErrors.Invalid("INVALID_TOOL_ARGUMENTS");
        if (normalized["expectedVersion"] is JsonNode version && version.GetValue<int>() < 1) throw McpErrors.Invalid("INVALID_TOOL_ARGUMENTS");
        if (normalized["durationMinutes"] is JsonNode duration && duration.GetValue<int>() is < 1 or > 1440) throw McpErrors.Invalid("INVALID_TOOL_ARGUMENTS");
        if (normalized["category"] is JsonNode category && (string.IsNullOrWhiteSpace(category.GetValue<string>()) || category.GetValue<string>().Length > 8)) throw McpErrors.Invalid("INVALID_TOOL_ARGUMENTS");
        if (normalized["from"] is not null && normalized["to"] is not null)
        {
            var from = DateTimeOffset.Parse(normalized["from"]!.GetValue<string>(), CultureInfo.InvariantCulture); var to = DateTimeOffset.Parse(normalized["to"]!.GetValue<string>(), CultureInfo.InvariantCulture);
            if (to <= from || to - from > TimeSpan.FromDays(31)) throw McpErrors.Invalid("QUERY_WINDOW_EXCEEDED");
        }
        if (normalized["startsAt"] is not null && normalized["endsAt"] is not null && DateTimeOffset.Parse(normalized["endsAt"]!.GetValue<string>(), CultureInfo.InvariantCulture) <= DateTimeOffset.Parse(normalized["startsAt"]!.GetValue<string>(), CultureInfo.InvariantCulture)) throw McpErrors.Invalid("INVALID_TOOL_ARGUMENTS");
        return JsonSerializer.SerializeToElement(normalized);
    }
}

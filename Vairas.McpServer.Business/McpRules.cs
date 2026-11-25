using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Vairas.McpServer.Business;

public static class McpRules
{
    public static DateTimeOffset Instant(DateTimeOffset value) => new(value.UtcTicks - value.UtcTicks % 10, TimeSpan.Zero);
    public static void Id(Guid value) { if (value == Guid.Empty) throw McpErrors.Invalid(); }
    public static void Text(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || Encoding.UTF8.GetByteCount(text) > 16384) throw McpErrors.Invalid("MESSAGE_SIZE_INVALID");
        ValidUnicode(text);
    }
    public static void Fresh(DateTimeOffset until, DateTimeOffset now) { if (until <= now) throw McpErrors.Dependency("ADMISSION_EXPIRED"); }
    public static string Hash(JsonElement value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Canonical(value))));
    public static string Canonical(JsonElement value)
    {
        var buffer = new StringBuilder(); Write(buffer, value); return buffer.ToString();
    }
    private static void Write(StringBuilder b, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                var properties = value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal).ToArray();
                if (properties.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != properties.Length) throw McpErrors.Invalid("DUPLICATE_PROPERTY");
                b.Append('{'); for (var i = 0; i < properties.Length; i++) { if (i > 0) b.Append(','); String(b, properties[i].Name); b.Append(':'); Write(b, properties[i].Value); } b.Append('}'); break;
            case JsonValueKind.Array:
                b.Append('['); var first = true; foreach (var item in value.EnumerateArray()) { if (!first) b.Append(','); first = false; Write(b, item); } b.Append(']'); break;
            case JsonValueKind.String: String(b, value.GetString()!); break;
            case JsonValueKind.Number:
                if (!value.TryGetInt64(out var number) || number is < -9007199254740991 or > 9007199254740991) throw McpErrors.Invalid("UNSAFE_NUMBER");
                b.Append(number.ToString(CultureInfo.InvariantCulture)); break;
            case JsonValueKind.True: b.Append("true"); break;
            case JsonValueKind.False: b.Append("false"); break;
            case JsonValueKind.Null: b.Append("null"); break;
            default: throw McpErrors.Invalid();
        }
    }
    private static void String(StringBuilder b, string value)
    {
        ValidUnicode(value); b.Append('"');
        foreach (var c in value)
            switch (c)
            {
                case '"': b.Append("\\\""); break;
                case '\\': b.Append("\\\\"); break;
                case '\b': b.Append("\\b"); break;
                case '\t': b.Append("\\t"); break;
                case '\n': b.Append("\\n"); break;
                case '\f': b.Append("\\f"); break;
                case '\r': b.Append("\\r"); break;
                default: if (c < 0x20) b.Append("\\u" + ((int)c).ToString("x4", CultureInfo.InvariantCulture)); else b.Append(c); break;
            }
        b.Append('"');
    }
    private static void ValidUnicode(string value)
    {
        for (var i = 0; i < value.Length; i++)
            if (char.IsSurrogate(value[i]))
            {
                if (!char.IsHighSurrogate(value[i]) || i + 1 == value.Length || !char.IsLowSurrogate(value[i + 1])) throw McpErrors.Invalid("INVALID_UNICODE");
                i++;
            }
    }
}

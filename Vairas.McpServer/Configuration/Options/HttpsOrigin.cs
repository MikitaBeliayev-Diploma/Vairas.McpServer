namespace Vairas.McpServer.Configuration.Options;

internal static class HttpsOrigin
{
    /// <summary>An absolute HTTPS URL without credentials, query or fragment.</summary>
    public static bool IsValid(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0;
}

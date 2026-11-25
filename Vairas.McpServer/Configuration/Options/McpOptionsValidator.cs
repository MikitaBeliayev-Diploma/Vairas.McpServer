using Microsoft.Extensions.Options;

namespace Vairas.McpServer.Configuration.Options;

public sealed class McpOptionsValidator : IValidateOptions<McpOptions>
{
    public ValidateOptionsResult Validate(string? name, McpOptions options) =>
        options.ToolSeconds is >= 1 and <= 20 && HttpsOrigin.IsValid(options.Authority) && HttpsOrigin.IsValid(options.Resource)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("Configure private HTTPS MCP and owner resources.");
}

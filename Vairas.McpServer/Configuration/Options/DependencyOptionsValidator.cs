using Microsoft.Extensions.Options;
using Vairas.McpServer.Security;

namespace Vairas.McpServer.Configuration.Options;

public sealed class DependencyOptionsValidator(IHostEnvironment environment) : IValidateOptions<DependencyOptions>
{
    public ValidateOptionsResult Validate(string? name, DependencyOptions dependencies)
    {
        if (!HttpsOrigin.IsValid(dependencies.AuthBaseUrl) || !HttpsOrigin.IsValid(dependencies.SchoolBaseUrl) || !HttpsOrigin.IsValid(dependencies.BusinessApiBaseUrl)
            || dependencies.Resources.Count != 4
            || dependencies.Resources.Any(p => Vairas.Delegation.ReadPreviewDelegation.ResourceScopes(p.Key).Length == 0 || !HttpsOrigin.IsValid(p.Value))
            || dependencies.ClientId != "mcp-server")
            return ValidateOptionsResult.Fail("Configure private HTTPS MCP and owner resources.");
        if (!environment.IsEnvironment("Testing") && string.IsNullOrWhiteSpace(dependencies.ClientSecret))
            return ValidateOptionsResult.Fail("Configure the MCP workload credential.");
        return ValidateOptionsResult.Success;
    }
}

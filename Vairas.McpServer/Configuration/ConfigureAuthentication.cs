using System.Security.Claims;
using System.Text.Json;
using Vairas.Delegation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Vairas.McpServer.Configuration.Options;
using Vairas.McpServer.Security;

namespace Vairas.McpServer.Configuration;

/// <summary>
/// Exact-recipient bearer authentication: tokens for this MCP resource, plus the separate single-scope recovery
/// audience used by Chat to reconcile confirmed lesson actions.
/// </summary>
public static class ConfigureAuthentication
{
    public static IServiceCollection AddBearerAuthentication(this IServiceCollection services)
    {
            services.AddAuthentication("McpEntry").AddPolicyScheme("McpEntry", "Exact recipient authentication", o => o.ForwardDefaultSelector = http =>
            {
                var header=http.Request.Headers.Authorization.ToString();
                if(header.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase) && header.Length<20000)
                    try { var token=new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(header[7..]); if(token.Audiences.SequenceEqual(new[]{"vairas-mcp-internal"})) return "Recovery"; } catch(ArgumentException) { }
                return JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer();
            services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<McpOptions>((o, settings) =>
            {
                o.Authority = settings.Authority; o.RequireHttpsMetadata = true; o.MapInboundClaims = false; o.IncludeErrorDetails = false;
                o.TokenValidationParameters = new() { ValidateIssuer = true, ValidIssuer = settings.Authority, ValidateAudience = true, ValidAudience = settings.Resource, RequireSignedTokens = true, ValidateIssuerSigningKey = true, ValidAlgorithms = [SecurityAlgorithms.RsaSha256], RequireExpirationTime = true, ValidateLifetime = true, ClockSkew = TimeSpan.Zero };
                o.Events = new() { OnTokenValidated = c => { try { if (Vairas.ConfirmedActions.LessonActions.IsExecution(c.Principal!)) _ = Vairas.ConfirmedActions.LessonActions.Parse(c.Principal!, 1); else _ = ReadPreviewDelegation.Parse(c.Principal!, 1); if (c.Principal!.FindAll("aud").Count() != 1) c.Fail("Invalid audience binding."); } catch (Exception e) when (e is InvalidOperationException or JsonException or KeyNotFoundException or FormatException) { c.Fail("Invalid delegation."); } return Task.CompletedTask; } };
            });
            services.AddAuthentication().AddJwtBearer("Recovery", _ => { });
            services.AddOptions<JwtBearerOptions>("Recovery").Configure<McpOptions>((o, settings) =>
            {
                o.Authority = settings.Authority; o.MapInboundClaims = false; o.IncludeErrorDetails = false;
                o.TokenValidationParameters = new() { ValidateIssuer = true, ValidIssuer = settings.Authority, ValidateAudience = true, ValidAudience = "vairas-mcp-internal", RequireSignedTokens = true, ValidateIssuerSigningKey = true, ValidAlgorithms = [SecurityAlgorithms.RsaSha256], RequireExpirationTime = true, ValidateLifetime = true, ClockSkew = TimeSpan.Zero };
                o.Events = new() { OnTokenValidated = c => { if (c.Principal?.FindFirstValue("sub") != "chat-backend" || !c.Principal.HasClaim("scope", Vairas.ConfirmedActions.LessonActions.RecoveryScope) || c.Principal.FindAll("aud").Count() != 1 || ReadPreviewDelegation.Marked(c.Principal)) c.Fail("Invalid recovery caller."); return Task.CompletedTask; } };
            });
        return services;
    }
}

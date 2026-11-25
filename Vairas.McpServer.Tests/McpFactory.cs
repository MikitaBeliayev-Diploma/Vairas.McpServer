using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Vairas.McpServer.Business;
using Xunit;
namespace Vairas.McpServer.Tests;

public sealed class McpFactory : WebApplicationFactory<Program>
{
    private readonly RSA rsa = RSA.Create(2048);
    public FakeOwners Owners { get; } = new(); public Guid School { get; } = Guid.NewGuid(); public Guid Identity { get; } = Guid.NewGuid(); public Guid Session { get; } = Guid.NewGuid();
    public int ToolSeconds { get; set; } = 20;
    protected override void ConfigureWebHost(IWebHostBuilder b)
    {
        b.UseEnvironment("Testing").UseContentRoot(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Vairas.McpServer")));
        b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["Mcp:ToolSeconds"] = ToolSeconds.ToString() }));
        b.ConfigureServices(s =>
        {
            s.AddSingleton<IMcpOwners>(Owners);
            s.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, o => o.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(new() { Issuer = "https://localhost:7100", SigningKeys = { new RsaSecurityKey(rsa) { KeyId = "synthetic" } } }));
        });
    }
    public string Token(string variant = "valid", string? scope = null)
    {
        var claims = new List<Claim> { new("sub", Identity.ToString()), new("sid", Session.ToString()), new("school_id", School.ToString()), new("vairas_delegation", "ReadPreviewV1"), new("scope", scope ?? string.Join(' ', ToolCatalog.All.Select(t => "mcp." + t.Name).Prepend("mcp.context.read"))), new("act", variant == "actor" ? "{\"sub\":\"attacker\"}" : "{\"sub\":\"chat-backend\"}", JsonClaimValueTypes.Json) };
        if (variant == "binding") claims.Add(new("sid", Guid.NewGuid().ToString()));
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("https://localhost:7100", variant == "audience" ? "vairas-chat" : "https://localhost:7900/mcp", claims, expires: DateTime.UtcNow.AddMinutes(1), signingCredentials: new(new RsaSecurityKey(rsa) { KeyId = "synthetic" }, SecurityAlgorithms.RsaSha256)));
    }
    public HttpClient Http() => CreateClient(new() { BaseAddress = new("https://localhost:7900"), AllowAutoRedirect = false });
    public async Task<McpClient> Sdk(HttpClient http, string? token = null)
    {
        var transport = new HttpClientTransport(new() { Endpoint = new("https://localhost:7900/mcp"), EnableStandaloneGetStream = false, AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = "Bearer " + (token ?? Token()) } }, http, ownsHttpClient: false);
        return await McpClient.CreateAsync(transport);
    }
}

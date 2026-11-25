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

public sealed class FakeOwners : IMcpOwners
{
    public Guid User { get; } = Guid.NewGuid();
    public bool Active { get; set; } = true; public bool Allowed { get; set; } = true;
    public string Stamp { get; set; } = new('a', 64);
    public int Reads { get; private set; } public int Admissions { get; private set; }
    public Func<CancellationToken, Task>? DuringRead { get; set; }
    public bool InvalidResult { get; set; }
    public Task<McpActor> ReadmitAsync(McpActor actor, CancellationToken ct)
    {
        Admissions++; if (!Active) throw new UnauthorizedException("SESSION_INVALID");
        return Task.FromResult(actor with { UserId = User, ValidUntil = DateTimeOffset.UtcNow.AddSeconds(30) });
    }
    public Task<SchoolAccess> AccessAsync(Guid school, McpActor actor, CancellationToken ct) => Task.FromResult(new SchoolAccess(school, actor.UserId, Allowed, true, Stamp, DateTimeOffset.UtcNow.AddSeconds(30)));
    public async Task<JsonElement> ReadToolAsync(string name, JsonElement arguments, Guid school, McpActor actor, CancellationToken ct)
    {
        Reads++; if (DuringRead is not null) await DuringRead(ct);
        if (InvalidResult) return Wire.Element(new { malformed = true });
        return name switch
        {
            "get_my_enrollments" => Wire.Element(new { enrollments = Array.Empty<object>() }),
            "get_my_lessons" => Wire.Element(new { lessons = Array.Empty<object>() }),
            "find_available_lessons" => Wire.Element(new { schoolId = school, slots = Array.Empty<object>() }),
            _ => Wire.Element(new { enrollmentId = arguments.GetProperty("enrollmentId").GetGuid(), theoryMinutes = 0, practicalMinutes = 0, percent = (double?)null, outstandingRequirements = new[] { "Unknown programme" }, pendingRecordCount = 0, disputedRecordCount = 0, theoryDeficitMinutes = (int?)null, practicalDeficitMinutes = (int?)null, dependencyRevision = 0, sourcesVerified = false, state = "AwaitingProgramme" })
        };
    }
    public Task<OwnerPreview> PreviewAsync(string name, JsonElement arguments, Guid school, McpActor actor, CancellationToken ct)
    {
        Reads++;
        var args = System.Text.Json.Nodes.JsonNode.Parse(arguments.GetRawText())!.AsObject(); args["schoolId"] = school;
        return Task.FromResult(new OwnerPreview("scheduling-preview/1", name.EndsWith("booking", StringComparison.Ordinal) ? "Booking" : "Rescheduling", Wire.Element(args), Wire.Element(new { revision = 1 }), Wire.Element(new { schoolId = school, notice = "Advisory; no reservation" })));
    }
}

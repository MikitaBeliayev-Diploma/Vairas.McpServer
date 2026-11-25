using System.Text.Json;
using System.Text.Json.Serialization;
namespace Vairas.McpServer.Business;

/// <summary>Authenticated delegation only; never supplied as model tool arguments.</summary>
public sealed record McpActor(Guid IdentityId, Guid SessionId, Guid UserId, string UserBearer, DateTimeOffset ValidUntil, Guid SchoolId, string[] Scopes);

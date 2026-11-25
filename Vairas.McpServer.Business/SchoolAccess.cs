using System.Text.Json;
using System.Text.Json.Serialization;
namespace Vairas.McpServer.Business;

/// <summary>Minimal current School authorization projection.</summary>
public sealed record SchoolAccess(Guid SchoolId, Guid ActorUserId, bool Allowed, bool CanAudit, string AccessStamp, DateTimeOffset ValidUntil);

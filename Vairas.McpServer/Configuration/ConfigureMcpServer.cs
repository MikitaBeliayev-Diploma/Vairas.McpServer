using System.Security.Claims;
using System.Text.Json;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;
using Vairas.Delegation;
using Vairas.McpServer.Configuration.Options;
using Vairas.McpServer.Business;
using Vairas.McpServer.Integrations;
using Vairas.McpServer.Security;

namespace Vairas.McpServer.Configuration;

/// <summary>The stateless MCP HTTP transport with its tool listing and tool-call handlers.</summary>
public static class ConfigureMcpServer
{
    public static IServiceCollection AddMcpTools(this IServiceCollection services)
    {
            services.AddMcpServer().WithHttpTransport(o => o.SessionMode = HttpServerSessionMode.Stateless)
                .WithListToolsHandler((request, _) =>
                {
                    var principal = request.Services!.GetRequiredService<IHttpContextAccessor>().HttpContext!.User;
                    var tools = Vairas.ConfirmedActions.LessonActions.IsExecution(principal) ? LessonActionCatalog.For(Vairas.ConfirmedActions.LessonActions.Tool(Vairas.ConfirmedActions.LessonActions.Parse(principal, 1).Kind)) : principal.FindFirstValue("sub") == "chat-backend" ? LessonActionCatalog.For("resolve_lesson_action") : Catalog.Tools;
                    return ValueTask.FromResult(new ListToolsResult { Tools = tools });
                })
                .WithCallToolHandler(async (request, ct) =>
                {
                    var http = request.Services!.GetRequiredService<IHttpContextAccessor>().HttpContext!;
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct, http.RequestAborted); timeout.CancelAfter(TimeSpan.FromSeconds(http.RequestServices.GetRequiredService<McpOptions>().ToolSeconds));
                    try
                    {
                        if (Vairas.ConfirmedActions.LessonActions.IsExecution(http.User) || http.User.FindFirstValue("sub") == "chat-backend")
                        {
                            var payload = Wire.Element(request.Params?.Arguments ?? new Dictionary<string, JsonElement>());
                            var action = payload.Deserialize<Vairas.ConfirmedActions.LessonAction>(Vairas.ConfirmedActions.LessonActions.Json) ?? throw McpErrors.Invalid();
                            var receipt = await request.Services!.GetRequiredService<ConfirmedLessonTransport>().CallAsync(request.Params!.Name, action, http.User, http.Request.Headers.Authorization.ToString()[7..], timeout.Token);
                            var output = Wire.Element(receipt);
                            return new CallToolResult { StructuredContent = output, Content = [new TextContentBlock { Text = output.GetRawText() }] };
                        }
                        var context = ReadPreviewDelegation.Parse(http.User, 1);
                        var actor = new McpActor(context.Identity, context.Session, Guid.Empty, http.Request.Headers.Authorization.ToString()[7..], DateTimeOffset.MinValue, context.School, context.Scopes);
                        var arguments = Wire.Element(request.Params?.Arguments ?? new Dictionary<string, JsonElement>());
                        var result = await request.Services!.GetRequiredService<ToolExecutor>().ExecuteAsync(request.Params!.Name, arguments, actor, timeout.Token);
                        return new CallToolResult { StructuredContent = result, Content = [new TextContentBlock { Text = result.GetRawText() }] };
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested || http.RequestAborted.IsCancellationRequested) { throw; }
                    catch (Exception exception)
                    {
                        var code = exception is BusinessException known ? known.Code : exception is OperationCanceledException ? "TOOL_TIMEOUT" : "TOOL_UNAVAILABLE";
                        http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Vairas.McpServer.Tools").LogWarning("Tool {Tool} failed: {Code}; correlation {CorrelationId}", ReadPreviewDelegation.Tools.Contains(request.Params?.Name, StringComparer.Ordinal) ? request.Params!.Name : "unknown", code, http.TraceIdentifier);
                        return new CallToolResult { IsError = true, Content = [new TextContentBlock { Text = JsonSerializer.Serialize(new { code, correlationId = http.TraceIdentifier }) }] };
                    }
                });
        return services;
    }
}

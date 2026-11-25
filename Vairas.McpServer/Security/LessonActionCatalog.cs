using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using Vairas.ConfirmedActions;
namespace Vairas.McpServer.Security;

public static class LessonActionCatalog
{
    public static IList<Tool> For(string name) => [new Tool
    {
        Name = name, Description = "Execute or recover one exact server-persisted human-confirmed lesson action. Never available to the language model.", InputSchema = LessonActions.InputSchema, OutputSchema = LessonActions.OutputSchema,
        Annotations = new ToolAnnotations { ReadOnlyHint = false, DestructiveHint = name != "create_lesson_booking", IdempotentHint = true, OpenWorldHint = false },
        Meta = new JsonObject { ["toolContractVersion"] = "confirmed-lesson-actions/1", ["executionProfile"] = LessonActions.Profile }
    }];
}

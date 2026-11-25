using System.Text.Json;
using Vairas.McpServer.Security;
using Xunit;
namespace Vairas.McpServer.Tests;
[Trait("Category","Unit")]
public sealed class LessonActionCatalogTests
{
    [Fact]
    public async Task Execution_and_recovery_contracts_are_closed_and_absent_from_model_catalogue()
    {
        var tools=new[]{"create_lesson_booking","reschedule_lesson_booking","resolve_lesson_action"}.SelectMany(LessonActionCatalog.For).ToArray();
        Assert.Equal(6,Business.ToolCatalog.All.Count);
        Assert.All(tools,t=>{Assert.DoesNotContain(Business.ToolCatalog.All,x=>x.Name==t.Name);Assert.NotNull(t.OutputSchema);Assert.False(t.Annotations!.ReadOnlyHint);Assert.True(t.Annotations.IdempotentHint);Assert.False(t.InputSchema.GetProperty("additionalProperties").GetBoolean());});
        if(Environment.GetEnvironmentVariable("VAIRAS_MCP_EXPORT_CONTRACTS")=="on")
        {
            var file=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../api/confirmed-lesson-tools.json"));
            await File.WriteAllTextAsync(file,JsonSerializer.Serialize(new {toolContractVersion="confirmed-lesson-actions/1",executionProfile="ConfirmedLessonActionsV1",modelVisible=false,tools},new JsonSerializerOptions{WriteIndented=true}));
        }
    }
}

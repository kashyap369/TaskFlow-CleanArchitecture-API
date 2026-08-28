using TaskFlow.Domain.Entities.Planner;
using TaskFlow.Domain.Enums.Planner;

namespace TaskFlow.Tests.Domain;

public sealed class PlannerTemplateTests
{
    [Fact]
    public void PublishedEdits_CreateImmutableVersions_AndArchivePreservesThem()
    {
        var template = Create();
        var first = template.Publish(1);
        var second = template.Update("Task card", "ListTodo", "Changed", "#ffffff", "#111111", 300, 140,
            "[\"title\"]", "{}", 2, true, 1);
        template.Archive();
        Assert.Equal(1, first.VersionNumber); Assert.Equal("Task", first.Header);
        Assert.Equal(2, second.VersionNumber); Assert.Equal("Changed", second.Header);
        Assert.Equal(PlannerTemplateStatus.Archived, template.Status); Assert.Equal(2, template.Versions.Count);
    }

    [Fact]
    public void InvalidFieldsForObjectType_AreRejected() =>
        Assert.Throws<ArgumentException>(() => new PlannerTemplate("Bad", PlannerNodeType.SubTask, "Check", "Bad",
            "#ffffff", "#000000", 200, 100, "[\"budgetAmount\"]", "{}", 0, true, 1));

    private static PlannerTemplate Create() => new("Task card", PlannerNodeType.Task, "ListTodo", "Task",
        "#f3f0ff", "#7048e8", 280, 128, "[\"title\",\"priority\"]", "{\"priority\":2}", 1, true, 1);
}

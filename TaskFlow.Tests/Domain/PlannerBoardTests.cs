using TaskFlow.Domain.Entities.Planner;
using TaskFlow.Domain.Entities.WorkManagement.Projects;
using TaskFlow.Domain.Enums.WorkManagement;
using TaskEntity = TaskFlow.Domain.Entities.WorkManagement.Tasks.Task;

namespace TaskFlow.Tests.Domain;

public sealed class PlannerBoardTests
{
    [Fact]
    public void Create_RequiresAPersonalProjectOwnedByTheBoardOwner()
    {
        var organizationProject = new Project(
            "Shared launch",
            string.Empty,
            DateTime.UtcNow,
            organizationId: 10,
            createdByUserId: 7);

        Assert.Throws<ArgumentException>(() =>
            PlannerBoard.Create(organizationProject, ownerUserId: 7));
    }

    [Fact]
    public void SaveScene_IncrementsTheConcurrencyRevisionAndCreatesAnImmutableSnapshot()
    {
        var board = PlannerBoard.Create(PersonalProject(ownerUserId: 7), ownerUserId: 7);

        var revision = board.SaveScene(
            PlannerSceneDocument.Empty,
            expectedRevision: 0,
            actorUserId: 7);

        Assert.Equal(1, board.CurrentRevision);
        Assert.Equal(1, revision.RevisionNumber);
        Assert.Equal(PlannerSceneDocument.Empty, revision.SceneJson);
        Assert.Single(board.SceneRevisions);
    }

    [Fact]
    public void SaveScene_RejectsAStaleRevision()
    {
        var board = PlannerBoard.Create(PersonalProject(ownerUserId: 7), ownerUserId: 7);
        board.SaveScene(PlannerSceneDocument.Empty, expectedRevision: 0, actorUserId: 7);

        Assert.Throws<InvalidOperationException>(() =>
            board.SaveScene(PlannerSceneDocument.Empty, expectedRevision: 0, actorUserId: 7));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"elements\":[],\"files\":{\"image\":{\"dataURL\":\"data:image/png;base64,AA\"}}}")]
    [InlineData("{\"files\":{}}")]
    public void SceneValidation_RejectsInvalidOrEmbeddedContent(string sceneJson)
    {
        Assert.Throws<ArgumentException>(() => PlannerSceneDocument.EnsureValid(sceneJson));
    }

    [Fact]
    public void SceneValidation_UsesUtf8BytesForTheFiveMegabyteLimit()
    {
        var sceneJson = $"{{\"elements\":[],\"text\":\"{new string('é', 2_500_000)}\"}}";

        Assert.True(sceneJson.Length < PlannerSceneDocument.MaximumLength);
        Assert.Throws<ArgumentException>(() => PlannerSceneDocument.EnsureValid(sceneJson));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("file:///etc/passwd")]
    public void SceneValidation_RejectsUnsafeElementLinks(string link)
    {
        var sceneJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            type = "excalidraw",
            elements = new[] { new { id = "1", type = "rectangle", link } },
            files = new { }
        });

        Assert.Throws<ArgumentException>(() => PlannerSceneDocument.EnsureValid(sceneJson));
    }

    [Fact]
    public void SceneValidation_RejectsBoardsBeyondTheMeasuredElementLimit()
    {
        var elements = string.Join(',', Enumerable.Range(0, PlannerSceneDocument.MaximumElementCount + 1)
            .Select(x => $"{{\"id\":\"{x}\",\"type\":\"rectangle\"}}"));
        var sceneJson = $"{{\"type\":\"excalidraw\",\"elements\":[{elements}],\"files\":{{}}}}";

        Assert.Throws<ArgumentException>(() => PlannerSceneDocument.EnsureValid(sceneJson));
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void SceneValidation_HandlesARealisticLargeBoardWithinTheTarget()
    {
        var elements = string.Join(',', Enumerable.Range(0, PlannerSceneDocument.MaximumElementCount)
            .Select(x => $"{{\"id\":\"{x}\",\"type\":\"rectangle\",\"x\":{x},\"y\":{x},\"text\":\"Item {x}\"}}"));
        var sceneJson = $"{{\"type\":\"excalidraw\",\"version\":2,\"elements\":[{elements}],\"files\":{{}}}}";
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        PlannerSceneDocument.EnsureValid(sceneJson);

        stopwatch.Stop();
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2),
            $"Large-board validation took {stopwatch.Elapsed.TotalMilliseconds:N0} ms.");
    }

    [Fact]
    public void LinkTask_UsesAStableElementMapping_AndRejectsDuplicateElements()
    {
        var project = PersonalProject(ownerUserId: 7);
        var board = PlannerBoard.Create(project, ownerUserId: 7);
        var task = new TaskEntity("Plan release", string.Empty, DateTime.UtcNow, TaskPriority.Medium,
            organizationId: null, createdByUserId: 7, projectId: project.Id);

        var node = board.LinkTask("task-element", task);

        Assert.Equal("task-element", node.ElementId);
        Assert.Equal(task, node.Task);
        Assert.Throws<InvalidOperationException>(() => board.LinkTask("task-element", task));
    }

    [Fact]
    public void ProjectPlanningDetails_RequireAValidBudgetCurrencyAndDuration()
    {
        Assert.Throws<ArgumentException>(() => new Project("Launch", string.Empty, DateTime.UtcNow,
            null, 7, budgetAmount: 100, budgetCurrency: null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Project("Launch", string.Empty, DateTime.UtcNow,
            null, 7, approximateDurationWeeks: 0));
    }

    private static Project PersonalProject(int ownerUserId) =>
        new(
            "Personal launch",
            string.Empty,
            DateTime.UtcNow,
            organizationId: null,
            createdByUserId: ownerUserId);
}

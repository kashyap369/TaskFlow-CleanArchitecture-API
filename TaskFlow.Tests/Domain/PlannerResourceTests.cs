using TaskFlow.Domain.Entities.Planner;
using TaskFlow.Domain.Enums.Planner;

namespace TaskFlow.Tests.Domain;

public sealed class PlannerResourceTests
{
    [Fact]
    public void Note_RequiresContent_AndLinkRequiresHttpUrl()
    {
        Assert.Throws<ArgumentException>(() => PlannerResource.CreateNote(
            Guid.NewGuid(), 1, 1, "Empty", " "));
        Assert.Throws<ArgumentException>(() => PlannerResource.CreateLink(
            Guid.NewGuid(), 1, 1, "Unsafe", "javascript:alert(1)"));
    }

    [Fact]
    public void Document_OwnsAssetMetadataWithoutBinaryContent()
    {
        var boardId = Guid.NewGuid();
        var resource = PlannerResource.CreateDocument(boardId, 7, 11, "Brief");
        var asset = new PlannerAsset(resource.Id, boardId, 7, "planner/11/7/object",
            "brief.pdf", "application/pdf", 120, new string('a', 64), 11);
        resource.AttachAsset(asset);
        asset.SetScanStatus(PlannerAssetScanStatus.Clean);
        Assert.Same(asset, resource.Asset);
        Assert.Equal(PlannerAssetScanStatus.Clean, asset.ScanStatus);
    }
}

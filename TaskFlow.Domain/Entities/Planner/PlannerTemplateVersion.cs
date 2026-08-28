using TaskFlow.Domain.Enums.Planner;

namespace TaskFlow.Domain.Entities.Planner;

public sealed class PlannerTemplateVersion
{
    public Guid Id { get; private set; }
    public Guid TemplateId { get; private set; }
    public int VersionNumber { get; private set; }
    public PlannerNodeType ObjectType { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Icon { get; private set; } = string.Empty;
    public string Header { get; private set; } = string.Empty;
    public string BackgroundColor { get; private set; } = string.Empty;
    public string StrokeColor { get; private set; } = string.Empty;
    public int DefaultWidth { get; private set; }
    public int DefaultHeight { get; private set; }
    public string VisibleFieldsJson { get; private set; } = "[]";
    public string DefaultValuesJson { get; private set; } = "{}";
    public int PublishedByUserId { get; private set; }
    public DateTime PublishedAt { get; private set; }
    public PlannerTemplate Template { get; private set; } = null!;
    private PlannerTemplateVersion() { }
    internal PlannerTemplateVersion(PlannerTemplate template, int versionNumber, int actorUserId)
    {
        Id = Guid.NewGuid(); Template = template; TemplateId = template.Id; VersionNumber = versionNumber;
        ObjectType = template.ObjectType; Name = template.Name; Icon = template.Icon; Header = template.Header;
        BackgroundColor = template.BackgroundColor; StrokeColor = template.StrokeColor;
        DefaultWidth = template.DefaultWidth; DefaultHeight = template.DefaultHeight;
        VisibleFieldsJson = template.VisibleFieldsJson; DefaultValuesJson = template.DefaultValuesJson;
        PublishedByUserId = actorUserId; PublishedAt = DateTime.UtcNow;
    }
}

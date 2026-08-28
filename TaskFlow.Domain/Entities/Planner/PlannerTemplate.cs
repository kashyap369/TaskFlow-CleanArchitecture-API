using System.Text.Json;
using System.Text.RegularExpressions;
using TaskFlow.Domain.Enums.Planner;

namespace TaskFlow.Domain.Entities.Planner;

public sealed class PlannerTemplate
{
    private readonly List<PlannerTemplateVersion> _versions = new();
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public PlannerNodeType ObjectType { get; private set; }
    public PlannerTemplateStatus Status { get; private set; }
    public bool IsActive { get; private set; }
    public int SortOrder { get; private set; }
    public string Icon { get; private set; } = string.Empty;
    public string Header { get; private set; } = string.Empty;
    public string BackgroundColor { get; private set; } = string.Empty;
    public string StrokeColor { get; private set; } = string.Empty;
    public int DefaultWidth { get; private set; }
    public int DefaultHeight { get; private set; }
    public string VisibleFieldsJson { get; private set; } = "[]";
    public string DefaultValuesJson { get; private set; } = "{}";
    public int? CurrentVersionNumber { get; private set; }
    public int CreatedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public IReadOnlyCollection<PlannerTemplateVersion> Versions => _versions.AsReadOnly();

    private PlannerTemplate() { }

    public PlannerTemplate(string name, PlannerNodeType objectType, string icon, string header,
        string backgroundColor, string strokeColor, int defaultWidth, int defaultHeight,
        string visibleFieldsJson, string defaultValuesJson, int sortOrder, bool isActive, int actorUserId)
    {
        Id = Guid.NewGuid();
        ObjectType = objectType;
        Status = PlannerTemplateStatus.Draft;
        CreatedByUserId = actorUserId;
        CreatedAt = DateTime.UtcNow;
        Apply(name, icon, header, backgroundColor, strokeColor, defaultWidth, defaultHeight,
            visibleFieldsJson, defaultValuesJson, sortOrder, isActive);
    }

    public PlannerTemplateVersion? Update(string name, string icon, string header, string backgroundColor,
        string strokeColor, int defaultWidth, int defaultHeight, string visibleFieldsJson,
        string defaultValuesJson, int sortOrder, bool isActive, int actorUserId)
    {
        if (Status == PlannerTemplateStatus.Archived)
            throw new InvalidOperationException("Archived templates cannot be edited.");
        Apply(name, icon, header, backgroundColor, strokeColor, defaultWidth, defaultHeight,
            visibleFieldsJson, defaultValuesJson, sortOrder, isActive);
        UpdatedAt = DateTime.UtcNow;
        return Status == PlannerTemplateStatus.Published ? CreateVersion(actorUserId) : null;
    }

    public PlannerTemplateVersion Publish(int actorUserId)
    {
        if (Status != PlannerTemplateStatus.Draft)
            throw new InvalidOperationException("Only a draft template can be published.");
        Status = PlannerTemplateStatus.Published;
        UpdatedAt = DateTime.UtcNow;
        return CreateVersion(actorUserId);
    }

    public void Archive()
    {
        if (Status != PlannerTemplateStatus.Published)
            throw new InvalidOperationException("Only a published template can be archived.");
        Status = PlannerTemplateStatus.Archived;
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    private PlannerTemplateVersion CreateVersion(int actorUserId)
    {
        var version = new PlannerTemplateVersion(this, (CurrentVersionNumber ?? 0) + 1, actorUserId);
        CurrentVersionNumber = version.VersionNumber;
        _versions.Add(version);
        return version;
    }

    private void Apply(string name, string icon, string header, string backgroundColor, string strokeColor,
        int width, int height, string visibleFieldsJson, string defaultsJson, int sortOrder, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100) throw new ArgumentException("Template name is required and limited to 100 characters.");
        if (string.IsNullOrWhiteSpace(icon) || icon.Trim().Length > 50) throw new ArgumentException("Template icon is required and limited to 50 characters.");
        if (string.IsNullOrWhiteSpace(header) || header.Trim().Length > 120) throw new ArgumentException("Template header is required and limited to 120 characters.");
        if (!Regex.IsMatch(backgroundColor, "^#[0-9a-fA-F]{6}$") || !Regex.IsMatch(strokeColor, "^#[0-9a-fA-F]{6}$")) throw new ArgumentException("Template colors must be six-digit hex colors.");
        if (width is < 160 or > 800 || height is < 80 or > 600) throw new ArgumentException("Template dimensions are outside the supported range.");
        if (sortOrder is < 0 or > 10000) throw new ArgumentException("Template sort order is outside the supported range.");
        ValidateSchema(ObjectType, visibleFieldsJson, defaultsJson);
        Name = name.Trim(); Icon = icon.Trim(); Header = header.Trim(); BackgroundColor = backgroundColor.ToLowerInvariant();
        StrokeColor = strokeColor.ToLowerInvariant(); DefaultWidth = width; DefaultHeight = height;
        VisibleFieldsJson = visibleFieldsJson; DefaultValuesJson = defaultsJson; SortOrder = sortOrder; IsActive = isActive;
    }

    private static void ValidateSchema(PlannerNodeType type, string fieldsJson, string defaultsJson)
    {
        var allowed = type switch
        {
            PlannerNodeType.Project => new[] { "title", "description", "problemStatement", "budgetAmount", "budgetCurrency", "approximateDurationWeeks", "progress", "dates" },
            PlannerNodeType.Task => new[] { "title", "description", "priority", "startDate", "expectedCompletionDate", "progress", "requirementState" },
            PlannerNodeType.SubTask => new[] { "title", "completionState" },
            PlannerNodeType.Note => new[] { "title", "content" },
            PlannerNodeType.Document => new[] { "title", "fileName", "contentType", "size" },
            _ => Array.Empty<string>(),
        };
        using var fields = JsonDocument.Parse(fieldsJson);
        if (fields.RootElement.ValueKind != JsonValueKind.Array || fields.RootElement.EnumerateArray().Any(x => x.ValueKind != JsonValueKind.String || !allowed.Contains(x.GetString())))
            throw new ArgumentException("Visible fields contain values unsupported by this object type.");
        using var defaults = JsonDocument.Parse(defaultsJson);
        if (defaults.RootElement.ValueKind != JsonValueKind.Object || defaults.RootElement.EnumerateObject().Any(x => !IsValidDefault(type, x)))
            throw new ArgumentException("Default values contain values unsupported by this object type.");
    }

    private static bool IsValidDefault(PlannerNodeType type, JsonProperty property)
    {
        if (property.Value.ValueKind == JsonValueKind.Null) return true;
        var stringFields = type switch
        {
            PlannerNodeType.Project => new[] { "title", "description", "problemStatement", "budgetCurrency" },
            PlannerNodeType.Task => new[] { "title", "description" },
            PlannerNodeType.SubTask => new[] { "title" },
            PlannerNodeType.Note => new[] { "title", "content" },
            PlannerNodeType.Document => new[] { "title" },
            _ => Array.Empty<string>(),
        };
        if (stringFields.Contains(property.Name)) return property.Value.ValueKind == JsonValueKind.String;
        if (type == PlannerNodeType.Project && property.Name is "budgetAmount" or "approximateDurationWeeks")
            return property.Value.ValueKind == JsonValueKind.Number;
        if (type == PlannerNodeType.Task && property.Name == "priority")
            return property.Value.TryGetInt32(out var priority) && priority is >= 1 and <= 4;
        return false;
    }
}

using TaskFlow.Domain.Enums.Planner;

namespace TaskFlow.Domain.Entities.Planner;

public sealed class PlannerResource
{
    public Guid Id { get; private set; }
    public Guid BoardId { get; private set; }
    public int ProjectId { get; private set; }
    public int OwnerUserId { get; private set; }
    public PlannerResourceKind Kind { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Content { get; private set; }
    public string? Url { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public PlannerAsset? Asset { get; private set; }

    private PlannerResource() { }

    private PlannerResource(Guid boardId, int projectId, int ownerUserId,
        PlannerResourceKind kind, string title, string? content, string? url)
    {
        if (boardId == Guid.Empty) throw new ArgumentException("Board id is required.", nameof(boardId));
        if (projectId <= 0 || ownerUserId <= 0) throw new ArgumentException("Project and owner are required.");
        Id = Guid.NewGuid();
        BoardId = boardId;
        ProjectId = projectId;
        OwnerUserId = ownerUserId;
        Kind = kind;
        SetMetadata(title, content, url);
        CreatedAt = DateTime.UtcNow;
    }

    public static PlannerResource CreateNote(Guid boardId, int projectId, int ownerUserId,
        string title, string content) => new(boardId, projectId, ownerUserId,
            PlannerResourceKind.Note, title, content, null);

    public static PlannerResource CreateLink(Guid boardId, int projectId, int ownerUserId,
        string title, string url) => new(boardId, projectId, ownerUserId,
            PlannerResourceKind.Link, title, null, url);

    public static PlannerResource CreateDocument(Guid boardId, int projectId, int ownerUserId,
        string title) => new(boardId, projectId, ownerUserId,
            PlannerResourceKind.Document, title, null, null);

    public void AttachAsset(PlannerAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        if (Kind != PlannerResourceKind.Document || asset.ResourceId != Id)
            throw new InvalidOperationException("Only document resources can own their matching asset.");
        Asset = asset;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Update(string title, string? content, string? url)
    {
        SetMetadata(title, content, url);
        UpdatedAt = DateTime.UtcNow;
    }

    private void SetMetadata(string title, string? content, string? url)
    {
        title = title?.Trim() ?? string.Empty;
        if (title.Length is < 1 or > 200) throw new ArgumentException("Resource title must be 1 to 200 characters.");
        if (content?.Length > 20000) throw new ArgumentException("Note content cannot exceed 20,000 characters.");
        if (Kind == PlannerResourceKind.Note && string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Note content is required.");
        if (Kind == PlannerResourceKind.Link &&
            (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) || parsed.Scheme is not ("http" or "https")))
            throw new ArgumentException("A valid HTTP or HTTPS link is required.");
        Title = title;
        Content = Kind == PlannerResourceKind.Note ? content!.Trim() : null;
        Url = Kind == PlannerResourceKind.Link ? url!.Trim() : null;
    }
}

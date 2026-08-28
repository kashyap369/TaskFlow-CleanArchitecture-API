using TaskFlow.Domain.Enums.Planner;

namespace TaskFlow.Domain.Entities.Planner;

public sealed class PlannerAsset
{
    public Guid Id { get; private set; }
    public Guid ResourceId { get; private set; }
    public Guid BoardId { get; private set; }
    public int ProjectId { get; private set; }
    public string StorageKey { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long Size { get; private set; }
    public string Sha256 { get; private set; } = string.Empty;
    public int UploadedByUserId { get; private set; }
    public PlannerAssetScanStatus ScanStatus { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ScannedAt { get; private set; }

    private PlannerAsset() { }

    public PlannerAsset(Guid resourceId, Guid boardId, int projectId, string storageKey,
        string fileName, string contentType, long size, string sha256, int uploadedByUserId)
    {
        if (resourceId == Guid.Empty || boardId == Guid.Empty) throw new ArgumentException("Resource and board are required.");
        if (projectId <= 0 || uploadedByUserId <= 0) throw new ArgumentException("Project and uploader are required.");
        if (size <= 0) throw new ArgumentException("File cannot be empty.");
        Id = Guid.NewGuid();
        ResourceId = resourceId;
        BoardId = boardId;
        ProjectId = projectId;
        StorageKey = storageKey;
        FileName = fileName;
        ContentType = contentType;
        Size = size;
        Sha256 = sha256;
        UploadedByUserId = uploadedByUserId;
        ScanStatus = PlannerAssetScanStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    public void SetScanStatus(PlannerAssetScanStatus status)
    {
        ScanStatus = status;
        ScannedAt = DateTime.UtcNow;
    }

    public void Rename(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 255)
            throw new ArgumentException("File name is required and cannot exceed 255 characters.");
        FileName = fileName.Trim();
    }
}

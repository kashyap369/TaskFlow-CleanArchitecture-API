using TaskFlow.Domain.Enums.Planner;

namespace TaskFlow.Application.Contracts.Storage;

public interface IPlannerAssetScanner
{
    Task<PlannerAssetScanStatus> ScanAsync(string objectKey,
        CancellationToken cancellationToken = default);
}

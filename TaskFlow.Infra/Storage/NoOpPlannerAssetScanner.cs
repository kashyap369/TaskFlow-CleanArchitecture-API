using TaskFlow.Application.Contracts.Storage;
using TaskFlow.Domain.Enums.Planner;

namespace TaskFlow.Infra.Storage;

/// <summary>Extension point for a production malware scanner. Storage remains private and every
/// download is authorized even when this development scanner marks an upload clean immediately.</summary>
public sealed class NoOpPlannerAssetScanner : IPlannerAssetScanner
{
    public Task<PlannerAssetScanStatus> ScanAsync(string objectKey,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(PlannerAssetScanStatus.Clean);
}

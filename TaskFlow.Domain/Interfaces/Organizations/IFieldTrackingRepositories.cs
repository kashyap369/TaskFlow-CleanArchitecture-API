using TaskFlow.Domain.Entities.Organization;

namespace TaskFlow.Domain.Interfaces.Organizations;

public interface IAttendanceTrackedRoleRepository
{
    Task<IReadOnlyList<AttendanceTrackedRole>> GetByOrganizationAsync(int organizationId, CancellationToken cancellationToken = default);
    Task<bool> IsTrackedAsync(int organizationId, int organizationRoleId, CancellationToken cancellationToken = default);
    Task AddAsync(AttendanceTrackedRole trackedRole, CancellationToken cancellationToken = default);
    void Update(AttendanceTrackedRole trackedRole);
}

public interface IFieldTrackingConsentRepository
{
    Task<FieldTrackingConsent?> GetAsync(int organizationId, int userId, int policyVersion, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FieldTrackingConsent>> GetActiveAsync(int organizationId, int userId, CancellationToken cancellationToken = default);
    Task AddAsync(FieldTrackingConsent consent, CancellationToken cancellationToken = default);
    void Update(FieldTrackingConsent consent);
}

public interface ILocationPointRepository
{
    /// <summary>The recorded-at times already stored for a session in a window, so a resent batch adds nothing twice.</summary>
    Task<IReadOnlySet<DateTime>> GetRecordedTimesAsync(int attendanceSessionId, DateTime fromUtc, DateTime toUtc,
        CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<LocationPoint> points, CancellationToken cancellationToken = default);
}

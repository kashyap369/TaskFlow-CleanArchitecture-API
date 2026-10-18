using Microsoft.EntityFrameworkCore;
using TaskFlow.Domain.Entities.Organization;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Infra.Persistence.Context;

namespace TaskFlow.Infra.Persistence.Repositories.Organizations;

public sealed class AttendanceTrackedRoleRepository : IAttendanceTrackedRoleRepository
{
    private readonly TaskFlowDbContext _context;
    public AttendanceTrackedRoleRepository(TaskFlowDbContext context) => _context = context;
    public async Task<IReadOnlyList<AttendanceTrackedRole>> GetByOrganizationAsync(int organizationId,
        CancellationToken cancellationToken = default) =>
        await _context.AttendanceTrackedRoles.Where(x => x.OrganizationId == organizationId).ToListAsync(cancellationToken);
    public Task<bool> IsTrackedAsync(int organizationId, int organizationRoleId, CancellationToken cancellationToken = default) =>
        _context.AttendanceTrackedRoles.AnyAsync(x => x.OrganizationId == organizationId &&
            x.OrganizationRoleId == organizationRoleId, cancellationToken);
    public Task AddAsync(AttendanceTrackedRole trackedRole, CancellationToken cancellationToken = default) =>
        _context.AttendanceTrackedRoles.AddAsync(trackedRole, cancellationToken).AsTask();
    public void Update(AttendanceTrackedRole trackedRole) => _context.AttendanceTrackedRoles.Update(trackedRole);
}

public sealed class FieldTrackingConsentRepository : IFieldTrackingConsentRepository
{
    private readonly TaskFlowDbContext _context;
    public FieldTrackingConsentRepository(TaskFlowDbContext context) => _context = context;
    public Task<FieldTrackingConsent?> GetAsync(int organizationId, int userId, int policyVersion,
        CancellationToken cancellationToken = default) =>
        _context.FieldTrackingConsents.FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.UserId == userId &&
            x.PolicyVersion == policyVersion, cancellationToken);
    public async Task<IReadOnlyList<FieldTrackingConsent>> GetActiveAsync(int organizationId, int userId,
        CancellationToken cancellationToken = default) =>
        await _context.FieldTrackingConsents.Where(x => x.OrganizationId == organizationId && x.UserId == userId)
            .ToListAsync(cancellationToken);
    public Task AddAsync(FieldTrackingConsent consent, CancellationToken cancellationToken = default) =>
        _context.FieldTrackingConsents.AddAsync(consent, cancellationToken).AsTask();
    public void Update(FieldTrackingConsent consent) => _context.FieldTrackingConsents.Update(consent);
}

public sealed class LocationPointRepository : ILocationPointRepository
{
    private readonly TaskFlowDbContext _context;
    public LocationPointRepository(TaskFlowDbContext context) => _context = context;
    public async Task<IReadOnlySet<DateTime>> GetRecordedTimesAsync(int attendanceSessionId, DateTime fromUtc, DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        var from = DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc);
        var to = DateTime.SpecifyKind(toUtc, DateTimeKind.Utc);
        var times = await _context.LocationPoints.AsNoTracking()
            .Where(x => x.AttendanceSessionId == attendanceSessionId && x.RecordedAtUtc >= from && x.RecordedAtUtc <= to)
            .Select(x => x.RecordedAtUtc)
            .ToListAsync(cancellationToken);
        return times.Select(x => DateTime.SpecifyKind(x, DateTimeKind.Utc)).ToHashSet();
    }
    public Task AddRangeAsync(IEnumerable<LocationPoint> points, CancellationToken cancellationToken = default) =>
        _context.LocationPoints.AddRangeAsync(points, cancellationToken);
}

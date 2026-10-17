using Microsoft.EntityFrameworkCore;
using TaskFlow.Domain.Entities.Organization;
using TaskFlow.Domain.Enums.Organization;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Infra.Persistence.Context;

namespace TaskFlow.Infra.Persistence.Repositories.Organizations;

public sealed class AttendanceSessionRepository : IAttendanceSessionRepository
{
    private readonly TaskFlowDbContext _context;
    public AttendanceSessionRepository(TaskFlowDbContext context) => _context = context;
    public Task<AttendanceSession?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.AttendanceSessions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<AttendanceSession?> GetOpenAsync(int organizationId, int userId, CancellationToken cancellationToken = default) =>
        _context.AttendanceSessions.FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.UserId == userId &&
            x.Status == AttendanceSessionStatus.Open, cancellationToken);
    public Task AddAsync(AttendanceSession session, CancellationToken cancellationToken = default) =>
        _context.AttendanceSessions.AddAsync(session, cancellationToken).AsTask();
    public void Update(AttendanceSession session) => _context.AttendanceSessions.Update(session);
}

public sealed class AttendanceSettingsRepository : IAttendanceSettingsRepository
{
    private readonly TaskFlowDbContext _context;
    public AttendanceSettingsRepository(TaskFlowDbContext context) => _context = context;
    public Task<AttendanceSettings?> GetByOrganizationAsync(int organizationId, CancellationToken cancellationToken = default) =>
        _context.AttendanceSettings.FirstOrDefaultAsync(x => x.OrganizationId == organizationId, cancellationToken);
    public Task AddAsync(AttendanceSettings settings, CancellationToken cancellationToken = default) =>
        _context.AttendanceSettings.AddAsync(settings, cancellationToken).AsTask();
    public void Update(AttendanceSettings settings) => _context.AttendanceSettings.Update(settings);
}

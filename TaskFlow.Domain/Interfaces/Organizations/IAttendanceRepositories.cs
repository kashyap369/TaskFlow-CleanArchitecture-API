using TaskFlow.Domain.Entities.Organization;

namespace TaskFlow.Domain.Interfaces.Organizations;

public interface IAttendanceSessionRepository
{
    Task<AttendanceSession?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<AttendanceSession?> GetOpenAsync(int organizationId, int userId, CancellationToken cancellationToken = default);
    Task AddAsync(AttendanceSession session, CancellationToken cancellationToken = default);
    void Update(AttendanceSession session);
}

public interface IAttendanceSettingsRepository
{
    Task<AttendanceSettings?> GetByOrganizationAsync(int organizationId, CancellationToken cancellationToken = default);
    Task AddAsync(AttendanceSettings settings, CancellationToken cancellationToken = default);
    void Update(AttendanceSettings settings);
}

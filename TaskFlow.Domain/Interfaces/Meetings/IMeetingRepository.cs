using TaskFlow.Domain.Entities.Meetings;

namespace TaskFlow.Domain.Interfaces.Meetings;

public interface IMeetingRepository
{
    Task<Meeting?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task AddAsync(Meeting meeting, CancellationToken cancellationToken = default);
    void Update(Meeting meeting);
}

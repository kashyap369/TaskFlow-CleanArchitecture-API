using Microsoft.EntityFrameworkCore;
using TaskFlow.Domain.Entities.Meetings;
using TaskFlow.Domain.Interfaces.Meetings;
using TaskFlow.Infra.Persistence.Context;

namespace TaskFlow.Infra.Persistence.Repositories.Meetings;

public sealed class MeetingRepository(TaskFlowDbContext context) : IMeetingRepository
{
    public Task<Meeting?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        context.Meetings.Include(x => x.Badges).Include(x => x.Participants)
            .Include(x => x.AccessLinks).Include(x => x.Attendance)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task AddAsync(Meeting meeting, CancellationToken cancellationToken = default) =>
        context.Meetings.AddAsync(meeting, cancellationToken).AsTask();
    public void Update(Meeting meeting) => context.Meetings.Update(meeting);
}

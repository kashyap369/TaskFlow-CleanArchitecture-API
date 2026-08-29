using TaskFlow.Domain.Entities.Organization;

namespace TaskFlow.Domain.Interfaces.Organizations;

public interface ICalendarEntryRepository
{
    Task<CalendarEntry?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task AddAsync(CalendarEntry entry, CancellationToken cancellationToken = default);
    void Update(CalendarEntry entry);
    void Remove(CalendarEntry entry);
}

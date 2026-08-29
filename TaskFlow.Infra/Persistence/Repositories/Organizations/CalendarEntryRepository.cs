using Microsoft.EntityFrameworkCore;
using TaskFlow.Domain.Entities.Organization;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Infra.Persistence.Context;

namespace TaskFlow.Infra.Persistence.Repositories.Organizations;

public sealed class CalendarEntryRepository : ICalendarEntryRepository
{
    private readonly TaskFlowDbContext _context;
    public CalendarEntryRepository(TaskFlowDbContext context) => _context = context;
    public Task<CalendarEntry?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.CalendarEntries.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task AddAsync(CalendarEntry entry, CancellationToken cancellationToken = default) =>
        _context.CalendarEntries.AddAsync(entry, cancellationToken).AsTask();
    public void Update(CalendarEntry entry) => _context.CalendarEntries.Update(entry);
    public void Remove(CalendarEntry entry) { entry.SoftDelete(); _context.CalendarEntries.Update(entry); }
}

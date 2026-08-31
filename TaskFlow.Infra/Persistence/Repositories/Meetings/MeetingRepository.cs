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
    public Task<Meeting?> GetByRoomNameAsync(string roomName, CancellationToken cancellationToken = default) =>
        context.Meetings.Include(x => x.Badges).Include(x => x.Participants)
            .Include(x => x.Attendance)
            .FirstOrDefaultAsync(x => x.RoomName == roomName, cancellationToken);
    public Task<bool> HasWebhookReceiptAsync(string providerEventId, CancellationToken cancellationToken = default) =>
        context.MeetingWebhookReceipts.AnyAsync(x => x.ProviderEventId == providerEventId, cancellationToken);
    public Task AddWebhookReceiptAsync(MeetingWebhookReceipt receipt, CancellationToken cancellationToken = default) =>
        context.MeetingWebhookReceipts.AddAsync(receipt, cancellationToken).AsTask();
    public Task AddAsync(Meeting meeting, CancellationToken cancellationToken = default) =>
        context.Meetings.AddAsync(meeting, cancellationToken).AsTask();
    public void Update(Meeting meeting) => context.Meetings.Update(meeting);
}

public sealed class MeetingGuestAccessRepository(TaskFlowDbContext context) : IMeetingGuestAccessRepository
{
    public Task<MeetingAccessLink?> GetLinkByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        context.MeetingAccessLinks.FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);
    public Task<MeetingGuestChallenge?> GetLatestChallengeAsync(int accessLinkId, string normalizedEmail, CancellationToken cancellationToken = default) =>
        context.MeetingGuestChallenges.Where(x => x.AccessLinkId == accessLinkId && x.NormalizedEmail == normalizedEmail)
            .OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(cancellationToken);
    public Task<MeetingGuestSession?> GetSessionByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        context.MeetingGuestSessions.FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);
    public async Task<IReadOnlyList<MeetingGuestSession>> GetActiveSessionsAsync(int participantId, CancellationToken cancellationToken = default) =>
        await context.MeetingGuestSessions.Where(x => x.ParticipantId == participantId && x.RevokedAtUtc == null && x.ExpiresAtUtc > DateTime.UtcNow).ToListAsync(cancellationToken);
    public Task AddChallengeAsync(MeetingGuestChallenge challenge, CancellationToken cancellationToken = default) =>
        context.MeetingGuestChallenges.AddAsync(challenge, cancellationToken).AsTask();
    public Task AddSessionAsync(MeetingGuestSession session, CancellationToken cancellationToken = default) =>
        context.MeetingGuestSessions.AddAsync(session, cancellationToken).AsTask();
    public Task AddDecisionAsync(MeetingGuestDecision decision, CancellationToken cancellationToken = default) =>
        context.MeetingGuestDecisions.AddAsync(decision, cancellationToken).AsTask();
    public void UpdateChallenge(MeetingGuestChallenge challenge) => context.MeetingGuestChallenges.Update(challenge);
    public void UpdateSession(MeetingGuestSession session) => context.MeetingGuestSessions.Update(session);
}

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
    public Task<int> CountLiveAsync(int organizationId, CancellationToken cancellationToken = default) =>
        context.Meetings.CountAsync(x => x.OrganizationId == organizationId &&
            x.Status == Domain.Enums.Meetings.MeetingStatus.Live, cancellationToken);
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
    public async Task<IReadOnlyList<MeetingGuestSession>> GetActiveSessionsForLinkAsync(int accessLinkId, CancellationToken cancellationToken = default) =>
        await context.MeetingGuestSessions.Where(x => x.AccessLinkId == accessLinkId && x.RevokedAtUtc == null && x.ExpiresAtUtc > DateTime.UtcNow).ToListAsync(cancellationToken);
    public Task AddChallengeAsync(MeetingGuestChallenge challenge, CancellationToken cancellationToken = default) =>
        context.MeetingGuestChallenges.AddAsync(challenge, cancellationToken).AsTask();
    public Task AddSessionAsync(MeetingGuestSession session, CancellationToken cancellationToken = default) =>
        context.MeetingGuestSessions.AddAsync(session, cancellationToken).AsTask();
    public Task AddDecisionAsync(MeetingGuestDecision decision, CancellationToken cancellationToken = default) =>
        context.MeetingGuestDecisions.AddAsync(decision, cancellationToken).AsTask();
    public void UpdateChallenge(MeetingGuestChallenge challenge) => context.MeetingGuestChallenges.Update(challenge);
    public void UpdateSession(MeetingGuestSession session) => context.MeetingGuestSessions.Update(session);
}

public sealed class MeetingCollaborationRepository(TaskFlowDbContext context) : IMeetingCollaborationRepository
{
    public Task<MeetingMessage?> GetMessageByClientIdAsync(int meetingId, int participantId, Guid clientMessageId, CancellationToken cancellationToken = default) =>
        context.MeetingMessages.FirstOrDefaultAsync(x => x.MeetingId == meetingId && x.AuthorParticipantId == participantId && x.ClientMessageId == clientMessageId, cancellationToken);
    public Task<MeetingMessage?> GetMessageAsync(int meetingId, int messageId, CancellationToken cancellationToken = default) =>
        context.MeetingMessages.FirstOrDefaultAsync(x => x.MeetingId == meetingId && x.Id == messageId, cancellationToken);
    public Task<MeetingNote?> GetNoteAsync(int meetingId, CancellationToken cancellationToken = default) =>
        context.MeetingNotes.FirstOrDefaultAsync(x => x.MeetingId == meetingId, cancellationToken);
    public Task<MeetingAsset?> GetAssetAsync(int meetingId, int assetId, CancellationToken cancellationToken = default) =>
        context.MeetingAssets.FirstOrDefaultAsync(x => x.MeetingId == meetingId && x.Id == assetId, cancellationToken);
    public async Task<long> GetAssetBytesAsync(int meetingId, CancellationToken cancellationToken = default) =>
        await context.MeetingAssets.Where(x => x.MeetingId == meetingId)
            .SumAsync(x => (long?)x.SizeBytes, cancellationToken) ?? 0;
    public Task<int> CountMessagesAsync(int meetingId, CancellationToken cancellationToken = default) =>
        context.MeetingMessages.CountAsync(x => x.MeetingId == meetingId, cancellationToken);
    public Task<int> CountAssetsAsync(int meetingId, CancellationToken cancellationToken = default) =>
        context.MeetingAssets.CountAsync(x => x.MeetingId == meetingId, cancellationToken);
    public Task AddMessageAsync(MeetingMessage message, CancellationToken cancellationToken = default) => context.MeetingMessages.AddAsync(message, cancellationToken).AsTask();
    public Task AddNoteAsync(MeetingNote note, CancellationToken cancellationToken = default) => context.MeetingNotes.AddAsync(note, cancellationToken).AsTask();
    public Task AddNoteRevisionAsync(MeetingNoteRevision revision, CancellationToken cancellationToken = default) => context.MeetingNoteRevisions.AddAsync(revision, cancellationToken).AsTask();
    public Task AddAssetAsync(MeetingAsset asset, CancellationToken cancellationToken = default) => context.MeetingAssets.AddAsync(asset, cancellationToken).AsTask();
    public void UpdateNote(MeetingNote note) => context.MeetingNotes.Update(note);
    public void UpdateAsset(MeetingAsset asset) => context.MeetingAssets.Update(asset);
}

public sealed class MeetingRecordingRepository(TaskFlowDbContext context) : IMeetingRecordingRepository
{
    public Task<MeetingRecording?> GetByIdAsync(int meetingId, int recordingId, CancellationToken cancellationToken = default) =>
        context.MeetingRecordings.Include(x => x.Consents)
            .FirstOrDefaultAsync(x => x.MeetingId == meetingId && x.Id == recordingId, cancellationToken);
    public Task<MeetingRecording?> GetActiveAsync(int meetingId, CancellationToken cancellationToken = default) =>
        context.MeetingRecordings.Include(x => x.Consents).OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(x => x.MeetingId == meetingId &&
                (x.Status == Domain.Enums.Meetings.MeetingRecordingStatus.PendingConsent ||
                 x.Status == Domain.Enums.Meetings.MeetingRecordingStatus.Starting ||
                 x.Status == Domain.Enums.Meetings.MeetingRecordingStatus.Recording ||
                 x.Status == Domain.Enums.Meetings.MeetingRecordingStatus.Processing), cancellationToken);
    public Task<MeetingRecording?> GetByProviderEgressIdAsync(string providerEgressId, CancellationToken cancellationToken = default) =>
        context.MeetingRecordings.Include(x => x.Consents)
            .FirstOrDefaultAsync(x => x.ProviderEgressId == providerEgressId, cancellationToken);
    public async Task<IReadOnlyList<MeetingRecording>> GetForMeetingAsync(int meetingId, CancellationToken cancellationToken = default) =>
        await context.MeetingRecordings.Include(x => x.Consents).Where(x => x.MeetingId == meetingId)
            .OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
    public Task<int> CountActiveAsync(CancellationToken cancellationToken = default) =>
        context.MeetingRecordings.CountAsync(x =>
            x.Status == Domain.Enums.Meetings.MeetingRecordingStatus.PendingConsent ||
            x.Status == Domain.Enums.Meetings.MeetingRecordingStatus.Starting ||
            x.Status == Domain.Enums.Meetings.MeetingRecordingStatus.Recording ||
            x.Status == Domain.Enums.Meetings.MeetingRecordingStatus.Processing, cancellationToken);
    public Task AddAsync(MeetingRecording recording, CancellationToken cancellationToken = default) =>
        context.MeetingRecordings.AddAsync(recording, cancellationToken).AsTask();
    public void Update(MeetingRecording recording) => context.MeetingRecordings.Update(recording);
}

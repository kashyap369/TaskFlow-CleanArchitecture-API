using TaskFlow.Domain.Entities.Meetings;

namespace TaskFlow.Domain.Interfaces.Meetings;

public interface IMeetingRepository
{
    Task<Meeting?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Meeting?> GetByRoomNameAsync(string roomName, CancellationToken cancellationToken = default);
    Task<bool> HasWebhookReceiptAsync(string providerEventId, CancellationToken cancellationToken = default);
    Task AddWebhookReceiptAsync(MeetingWebhookReceipt receipt, CancellationToken cancellationToken = default);
    Task AddAsync(Meeting meeting, CancellationToken cancellationToken = default);
    void Update(Meeting meeting);
}

public interface IMeetingGuestAccessRepository
{
    Task<MeetingAccessLink?> GetLinkByHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<MeetingGuestChallenge?> GetLatestChallengeAsync(int accessLinkId, string normalizedEmail, CancellationToken cancellationToken = default);
    Task<MeetingGuestSession?> GetSessionByHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MeetingGuestSession>> GetActiveSessionsAsync(int participantId, CancellationToken cancellationToken = default);
    Task AddChallengeAsync(MeetingGuestChallenge challenge, CancellationToken cancellationToken = default);
    Task AddSessionAsync(MeetingGuestSession session, CancellationToken cancellationToken = default);
    Task AddDecisionAsync(MeetingGuestDecision decision, CancellationToken cancellationToken = default);
    void UpdateChallenge(MeetingGuestChallenge challenge);
    void UpdateSession(MeetingGuestSession session);
}

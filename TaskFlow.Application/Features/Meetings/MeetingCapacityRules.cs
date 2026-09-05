using TaskFlow.Application.Contracts.Meetings;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Entities.Meetings;
using TaskFlow.Domain.Interfaces.Meetings;

namespace TaskFlow.Application.Features.Meetings;

/// <summary>
/// Phase 7 / P7.3. One place that turns the declared ceilings in <see cref="IMeetingPolicy"/> into
/// refusals, so a limit is enforced identically for members and guests and cannot drift between the
/// two paths. Each refusal carries its own code and names the number, because "you have reached the
/// limit" without the limit is not actionable for the person who hit it.
///
/// A ceiling that counts rows is checked before the write, not held under a lock: two requests
/// arriving in the same instant can both pass and leave the meeting one over. That overshoot is
/// bounded by concurrency and is deliberate — the alternative is serializing every chat message and
/// every join. Where an invariant must be exact, the database enforces it instead: the partial
/// unique index from `EnforceSingleActiveMeetingRecording` is what actually prevents two concurrent
/// recordings of one meeting. docs/MEETINGS-CAPACITY.md records this distinction.
/// </summary>
internal static class MeetingCapacityRules
{
    public static void EnsureParticipantSeat(Meeting meeting, IMeetingPolicy policy)
    {
        try { meeting.EnsureParticipantCapacity(policy.Capacity.MaxParticipantsPerMeeting); }
        catch (InvalidOperationException exception)
        { throw new BusinessException("MEETING_PARTICIPANT_LIMIT_REACHED", exception.Message); }
    }

    public static async Task EnsureLiveMeetingSlotAsync(Meeting meeting, IMeetingRepository meetings,
        IMeetingPolicy policy, CancellationToken ct)
    {
        var limit = policy.Capacity.MaxConcurrentLiveMeetingsPerOrganization;
        if (await meetings.CountLiveAsync(meeting.OrganizationId, ct) < limit) return;
        throw new BusinessException("MEETING_CONCURRENT_LIMIT_REACHED",
            $"This organization already has {limit} meetings live. End one before starting another.");
    }

    public static async Task EnsureMessageRoomAsync(int meetingId,
        IMeetingCollaborationRepository collaboration, IMeetingPolicy policy, CancellationToken ct)
    {
        var limit = policy.Capacity.MaxMessagesPerMeeting;
        if (await collaboration.CountMessagesAsync(meetingId, ct) < limit) return;
        throw new BusinessException("MEETING_MESSAGE_LIMIT_REACHED",
            $"This meeting has reached its limit of {limit} chat messages.");
    }

    public static async Task EnsureAssetRoomAsync(int meetingId, long incomingBytes,
        IMeetingCollaborationRepository collaboration, IMeetingPolicy policy, CancellationToken ct)
    {
        var capacity = policy.Capacity;
        if (await collaboration.CountAssetsAsync(meetingId, ct) >= capacity.MaxAssetsPerMeeting)
            throw new BusinessException("MEETING_FILE_COUNT_LIMIT_REACHED",
                $"This meeting has reached its limit of {capacity.MaxAssetsPerMeeting} shared files.");
        if (await collaboration.GetAssetBytesAsync(meetingId, ct) + incomingBytes > capacity.MaxStorageBytesPerMeeting)
            throw new BusinessException("MEETING_FILE_QUOTA_EXCEEDED",
                $"This meeting has reached its file storage quota of {capacity.MaxStorageBytesPerMeeting / 1048576} MB.");
    }

    /// <summary>
    /// Egress capacity is shared by the whole deployment, so this ceiling is not per meeting. A host
    /// who is refused here is refused before consent is requested from anyone: asking a room to
    /// consent and then failing to start would leave people believing they had been recorded.
    /// </summary>
    public static async Task EnsureRecordingSlotAsync(IMeetingRecordingRepository recordings,
        IMeetingPolicy policy, CancellationToken ct)
    {
        var limit = policy.Capacity.MaxConcurrentRecordings;
        if (await recordings.CountActiveAsync(ct) < limit) return;
        throw new BusinessException("MEETING_RECORDING_CAPACITY_REACHED",
            limit == 1
                ? "Another meeting is being recorded right now. Recording capacity is one meeting at a time; try again when it finishes."
                : $"Recording capacity is {limit} meetings at a time and all of it is in use. Try again shortly.");
    }
}

using Dapper;
using MediatR;
using TaskFlow.Application.Contracts.Meetings;
using TaskFlow.Application.Contracts.Persistence;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Contracts.Storage;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Entities.Meetings;
using TaskFlow.Domain.Enums.Meetings;
using TaskFlow.Domain.Interfaces.Meetings;
using TaskFlow.Domain.Interfaces.Persistence;

namespace TaskFlow.Application.Features.Meetings;

public sealed record MeetingRecordingConsentDto(int ParticipantId, string ParticipantName,
    MeetingRecordingConsentStatus Status, DateTime? DecidedAtUtc);
public sealed record MeetingRecordingDto(int Id, MeetingRecordingStatus Status, DateTime CreatedAt,
    DateTime ConsentExpiresAtUtc, DateTime? StartedAtUtc, DateTime? StoppedAtUtc, DateTime? ReadyAtUtc,
    string? FailureReason, long? SizeBytes, long? DurationMilliseconds, bool CanManage,
    MeetingRecordingConsentStatus? MyConsent, IReadOnlyList<MeetingRecordingConsentDto> Consents);
public sealed record MeetingRecordingContentDto(string FileName, string ContentType, byte[] Content);

public sealed record RequestMeetingRecordingCommand(int MeetingId, int ConsentTimeoutSeconds) : IRequest<MeetingRecordingDto>;
public sealed record ConsentMeetingRecordingCommand(int MeetingId, int RecordingId, bool Accepted) : IRequest<MeetingRecordingDto>;
public sealed record ConsentGuestMeetingRecordingCommand(string SessionToken, int RecordingId, bool Accepted) : IRequest<MeetingRecordingDto>;
public sealed record StopMeetingRecordingCommand(int MeetingId, int RecordingId) : IRequest<MeetingRecordingDto>;
public sealed record GetMeetingRecordingsQuery(int MeetingId) : IRequest<IReadOnlyList<MeetingRecordingDto>>;
public sealed record GetGuestMeetingRecordingsQuery(string SessionToken) : IRequest<IReadOnlyList<MeetingRecordingDto>>;
public sealed record DownloadMeetingRecordingQuery(int MeetingId, int RecordingId) : IRequest<MeetingRecordingContentDto>;
public sealed record DownloadGuestMeetingRecordingQuery(string SessionToken, int RecordingId) : IRequest<MeetingRecordingContentDto>;
public sealed record DeleteMeetingRecordingCommand(int MeetingId, int RecordingId) : IRequest;

internal static class MeetingRecordingRules
{
    public static void EnsureHost(MeetingCollaborationActor actor)
    { if (actor.Participant.AccessLevel != MeetingAccessLevel.Host) throw new ForbiddenException("MEETING_RECORDING_DENIED", "Only the meeting host can manage recordings."); }

    public static async Task<MeetingRecordingDto> ConsentAsync(MeetingCollaborationActor actor, int recordingId,
        bool accepted, IMeetingRecordingRepository recordings, IMeetingMediaProvider media,
        IUnitOfWork uow, CancellationToken ct)
    {
        var recording = await recordings.GetByIdAsync(actor.Meeting.Id, recordingId, ct)
            ?? throw new NotFoundException("MEETING_RECORDING_NOT_FOUND", "Meeting recording not found.");
        try { recording.RecordConsent(actor.Participant.Id, accepted, DateTime.UtcNow); }
        catch (InvalidOperationException ex) { throw new BusinessException("MEETING_RECORDING_CONSENT_CLOSED", ex.Message); }
        if (!accepted && recording.Status == MeetingRecordingStatus.PendingConsent)
            recording.Fail("A participant declined recording consent.");
        if (accepted && recording.Status == MeetingRecordingStatus.PendingConsent && recording.AllAccepted)
        {
            try
            {
                var started = await media.StartRoomRecordingAsync(actor.Meeting.RoomName, recording.StorageKey, ct);
                recording.BeginStarting(started.ProviderEgressId, DateTime.UtcNow);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            { recording.Fail("The recording service could not start."); }
        }
        recordings.Update(recording); await uow.SaveChangesAsync(ct);
        return ToDto(recording, actor);
    }

    public static MeetingRecordingDto ToDto(MeetingRecording recording, MeetingCollaborationActor actor)
    {
        var names = actor.Meeting.Participants.ToDictionary(x => x.Id,
            x => x.DisplayName ?? (x.UserId.HasValue ? $"Participant {x.UserId}" : "Guest"));
        var consents = recording.Consents.OrderBy(x => x.ParticipantId).Select(x =>
            new MeetingRecordingConsentDto(x.ParticipantId, names.GetValueOrDefault(x.ParticipantId, "Participant"), x.Status, x.DecidedAtUtc)).ToList();
        return new(recording.Id, recording.Status, recording.CreatedAt, recording.ConsentExpiresAtUtc,
            recording.StartedAtUtc, recording.StoppedAtUtc, recording.ReadyAtUtc, recording.FailureReason,
            recording.SizeBytes, recording.DurationMilliseconds, actor.Participant.AccessLevel == MeetingAccessLevel.Host,
            recording.Consents.FirstOrDefault(x => x.ParticipantId == actor.Participant.Id)?.Status, consents);
    }
}

public sealed class RequestMeetingRecordingCommandHandler(IMeetingRepository meetings,
    IMeetingGuestAccessRepository guests, IMeetingRecordingRepository recordings, ICurrentUserService user,
    IMeetingMediaProvider media, IUnitOfWork uow) : IRequestHandler<RequestMeetingRecordingCommand, MeetingRecordingDto>
{
    public async Task<MeetingRecordingDto> Handle(RequestMeetingRecordingCommand request, CancellationToken ct)
    {
        if (!media.IsEnabled) throw new BusinessException("MEETING_RECORDING_UNAVAILABLE", "Recording is not configured.");
        var actor = await new MeetingCollaborationAccess(meetings, guests).ForUserAsync(request.MeetingId, user.UserId, ct);
        MeetingCollaborationAccess.EnsureWritable(actor); MeetingRecordingRules.EnsureHost(actor);
        if (await recordings.GetActiveAsync(request.MeetingId, ct) is not null)
            throw new ConflictException("MEETING_RECORDING_ACTIVE", "A recording request or recording is already active.");
        var current = actor.Meeting.Attendance.Where(x => x.LeftAtUtc is null).Select(x => x.ParticipantId)
            .Append(actor.Participant.Id).Distinct().ToList();
        var now = DateTime.UtcNow;
        var recording = new MeetingRecording(request.MeetingId, actor.Participant.Id,
            $"meetings/{request.MeetingId}/recordings/{Guid.NewGuid():N}.mp4", current,
            now.AddSeconds(Math.Clamp(request.ConsentTimeoutSeconds, 15, 300)));
        recording.RecordConsent(actor.Participant.Id, true, now);
        await recordings.AddAsync(recording, ct); await uow.SaveChangesAsync(ct);
        if (recording.AllAccepted)
        {
            try { var result = await media.StartRoomRecordingAsync(actor.Meeting.RoomName, recording.StorageKey, ct); recording.BeginStarting(result.ProviderEgressId, now); }
            catch (Exception ex) when (ex is not OperationCanceledException) { recording.Fail("The recording service could not start."); }
            recordings.Update(recording); await uow.SaveChangesAsync(ct);
        }
        return MeetingRecordingRules.ToDto(recording, actor);
    }
}

public sealed class ConsentMeetingRecordingCommandHandler(IMeetingRepository meetings, IMeetingGuestAccessRepository guests,
    IMeetingRecordingRepository recordings, ICurrentUserService user, IMeetingMediaProvider media, IUnitOfWork uow)
    : IRequestHandler<ConsentMeetingRecordingCommand, MeetingRecordingDto>
{ public async Task<MeetingRecordingDto> Handle(ConsentMeetingRecordingCommand r, CancellationToken ct) => await MeetingRecordingRules.ConsentAsync(await new MeetingCollaborationAccess(meetings, guests).ForUserAsync(r.MeetingId, user.UserId, ct), r.RecordingId, r.Accepted, recordings, media, uow, ct); }

public sealed class ConsentGuestMeetingRecordingCommandHandler(IMeetingRepository meetings, IMeetingGuestAccessRepository guests,
    IMeetingRecordingRepository recordings, IMeetingMediaProvider media, IUnitOfWork uow)
    : IRequestHandler<ConsentGuestMeetingRecordingCommand, MeetingRecordingDto>
{ public async Task<MeetingRecordingDto> Handle(ConsentGuestMeetingRecordingCommand r, CancellationToken ct) => await MeetingRecordingRules.ConsentAsync(await new MeetingCollaborationAccess(meetings, guests).ForGuestAsync(r.SessionToken, ct), r.RecordingId, r.Accepted, recordings, media, uow, ct); }

public sealed class StopMeetingRecordingCommandHandler(IMeetingRepository meetings, IMeetingGuestAccessRepository guests,
    IMeetingRecordingRepository recordings, ICurrentUserService user, IMeetingMediaProvider media, IUnitOfWork uow)
    : IRequestHandler<StopMeetingRecordingCommand, MeetingRecordingDto>
{
    public async Task<MeetingRecordingDto> Handle(StopMeetingRecordingCommand r, CancellationToken ct)
    {
        var actor = await new MeetingCollaborationAccess(meetings, guests).ForUserAsync(r.MeetingId, user.UserId, ct); MeetingRecordingRules.EnsureHost(actor);
        var recording = await recordings.GetByIdAsync(r.MeetingId, r.RecordingId, ct) ?? throw new NotFoundException("MEETING_RECORDING_NOT_FOUND", "Meeting recording not found.");
        if (recording.Status is not (MeetingRecordingStatus.Starting or MeetingRecordingStatus.Recording) || string.IsNullOrWhiteSpace(recording.ProviderEgressId))
            throw new BusinessException("MEETING_RECORDING_NOT_ACTIVE", "This recording is not active.");
        await media.StopRoomRecordingAsync(recording.ProviderEgressId, ct); recording.MarkProcessing(DateTime.UtcNow);
        recordings.Update(recording); await uow.SaveChangesAsync(ct); return MeetingRecordingRules.ToDto(recording, actor);
    }
}

internal static class MeetingRecordingRead
{
    public static async Task<IReadOnlyList<MeetingRecordingDto>> ReadAsync(ISqlConnectionFactory sql, MeetingCollaborationActor actor, CancellationToken ct)
    {
        const string recordingSql = """
            SELECT "Id", "Status", "CreatedAt", "ConsentExpiresAtUtc", "StartedAtUtc", "StoppedAtUtc",
                   "ReadyAtUtc", "FailureReason", "SizeBytes", "DurationMilliseconds"
            FROM "MeetingRecordings" WHERE "MeetingId"=@MeetingId AND "IsDeleted"=FALSE
            ORDER BY "CreatedAt" DESC, "Id" DESC;
            """;
        const string consentSql = """
            SELECT c."MeetingRecordingId", c."ParticipantId",
                   COALESCE(p."DisplayName", u."FirstName" || ' ' || u."LastName", 'Participant') AS "ParticipantName",
                   c."Status", c."DecidedAtUtc"
            FROM "MeetingRecordingConsents" c
            JOIN "MeetingParticipants" p ON p."Id"=c."ParticipantId"
            LEFT JOIN "Users" u ON u."Id"=p."UserId"
            WHERE c."MeetingId"=@MeetingId AND c."IsDeleted"=FALSE ORDER BY c."Id";
            """;
        using var connection = sql.Create();
        var rows = (await connection.QueryAsync<RecordingRow>(new CommandDefinition(recordingSql, new { MeetingId = actor.Meeting.Id }, cancellationToken: ct))).ToList();
        var consents = (await connection.QueryAsync<ConsentRow>(new CommandDefinition(consentSql, new { MeetingId = actor.Meeting.Id }, cancellationToken: ct))).ToList();
        return rows.Select(row =>
        {
            var values = consents.Where(x => x.MeetingRecordingId == row.Id)
                .Select(x => new MeetingRecordingConsentDto(x.ParticipantId, x.ParticipantName, x.Status, x.DecidedAtUtc)).ToList();
            return new MeetingRecordingDto(row.Id, row.Status, row.CreatedAt, row.ConsentExpiresAtUtc,
                row.StartedAtUtc, row.StoppedAtUtc, row.ReadyAtUtc, row.FailureReason, row.SizeBytes,
                row.DurationMilliseconds, actor.Participant.AccessLevel == MeetingAccessLevel.Host,
                values.FirstOrDefault(x => x.ParticipantId == actor.Participant.Id)?.Status, values);
        }).ToList();
    }
    private sealed class RecordingRow { public int Id { get; init; } public MeetingRecordingStatus Status { get; init; } public DateTime CreatedAt { get; init; } public DateTime ConsentExpiresAtUtc { get; init; } public DateTime? StartedAtUtc { get; init; } public DateTime? StoppedAtUtc { get; init; } public DateTime? ReadyAtUtc { get; init; } public string? FailureReason { get; init; } public long? SizeBytes { get; init; } public long? DurationMilliseconds { get; init; } }
    private sealed class ConsentRow { public int MeetingRecordingId { get; init; } public int ParticipantId { get; init; } public string ParticipantName { get; init; } = string.Empty; public MeetingRecordingConsentStatus Status { get; init; } public DateTime? DecidedAtUtc { get; init; } }
}
public sealed class GetMeetingRecordingsQueryHandler(IMeetingRepository meetings, IMeetingGuestAccessRepository guests, ISqlConnectionFactory sql, ICurrentUserService user) : IRequestHandler<GetMeetingRecordingsQuery, IReadOnlyList<MeetingRecordingDto>>
{ public async Task<IReadOnlyList<MeetingRecordingDto>> Handle(GetMeetingRecordingsQuery r, CancellationToken ct) => await MeetingRecordingRead.ReadAsync(sql, await new MeetingCollaborationAccess(meetings, guests).ForUserAsync(r.MeetingId, user.UserId, ct), ct); }
public sealed class GetGuestMeetingRecordingsQueryHandler(IMeetingRepository meetings, IMeetingGuestAccessRepository guests, ISqlConnectionFactory sql) : IRequestHandler<GetGuestMeetingRecordingsQuery, IReadOnlyList<MeetingRecordingDto>>
{ public async Task<IReadOnlyList<MeetingRecordingDto>> Handle(GetGuestMeetingRecordingsQuery r, CancellationToken ct) => await MeetingRecordingRead.ReadAsync(sql, await new MeetingCollaborationAccess(meetings, guests).ForGuestAsync(r.SessionToken, ct), ct); }

internal static class MeetingRecordingContent
{
    public static async Task<MeetingRecordingContentDto> DownloadAsync(MeetingCollaborationActor actor, int recordingId, IMeetingRecordingRepository recordings, IObjectStorage storage, CancellationToken ct)
    { var recording = await recordings.GetByIdAsync(actor.Meeting.Id, recordingId, ct) ?? throw new NotFoundException("MEETING_RECORDING_NOT_FOUND", "Meeting recording not found."); if (recording.Status != MeetingRecordingStatus.Ready) throw new BusinessException("MEETING_RECORDING_NOT_READY", "The recording is not ready for playback."); var value = await storage.DownloadAsync(recording.StorageKey, ct); return new($"{actor.Meeting.Title}.mp4", "video/mp4", value.Content); }
}
public sealed class DownloadMeetingRecordingQueryHandler(IMeetingRepository meetings, IMeetingGuestAccessRepository guests, IMeetingRecordingRepository recordings, ICurrentUserService user, IObjectStorage storage) : IRequestHandler<DownloadMeetingRecordingQuery, MeetingRecordingContentDto>
{ public async Task<MeetingRecordingContentDto> Handle(DownloadMeetingRecordingQuery r, CancellationToken ct) => await MeetingRecordingContent.DownloadAsync(await new MeetingCollaborationAccess(meetings, guests).ForUserAsync(r.MeetingId, user.UserId, ct), r.RecordingId, recordings, storage, ct); }
public sealed class DownloadGuestMeetingRecordingQueryHandler(IMeetingRepository meetings, IMeetingGuestAccessRepository guests, IMeetingRecordingRepository recordings, IObjectStorage storage) : IRequestHandler<DownloadGuestMeetingRecordingQuery, MeetingRecordingContentDto>
{ public async Task<MeetingRecordingContentDto> Handle(DownloadGuestMeetingRecordingQuery r, CancellationToken ct) => await MeetingRecordingContent.DownloadAsync(await new MeetingCollaborationAccess(meetings, guests).ForGuestAsync(r.SessionToken, ct), r.RecordingId, recordings, storage, ct); }
public sealed class DeleteMeetingRecordingCommandHandler(IMeetingRepository meetings, IMeetingGuestAccessRepository guests, IMeetingRecordingRepository recordings, ICurrentUserService user, IObjectStorage storage, IUnitOfWork uow) : IRequestHandler<DeleteMeetingRecordingCommand>
{ public async Task Handle(DeleteMeetingRecordingCommand r, CancellationToken ct) { var actor = await new MeetingCollaborationAccess(meetings, guests).ForUserAsync(r.MeetingId, user.UserId, ct); MeetingRecordingRules.EnsureHost(actor); var recording = await recordings.GetByIdAsync(r.MeetingId, r.RecordingId, ct) ?? throw new NotFoundException("MEETING_RECORDING_NOT_FOUND", "Meeting recording not found."); if (recording.Status is MeetingRecordingStatus.Starting or MeetingRecordingStatus.Recording or MeetingRecordingStatus.Processing) throw new ConflictException("MEETING_RECORDING_ACTIVE", "Stop the recording before deleting it."); if (recording.Status == MeetingRecordingStatus.Ready) await storage.DeleteAsync(recording.StorageKey, ct); recording.SoftDelete(); recordings.Update(recording); await uow.SaveChangesAsync(ct); } }

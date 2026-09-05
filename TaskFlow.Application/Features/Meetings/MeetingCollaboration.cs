using System.Security.Cryptography;
using Dapper;
using FluentValidation;
using MediatR;
using TaskFlow.Application.Contracts.Persistence;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Contracts.Storage;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Entities.Meetings;
using TaskFlow.Domain.Enums.Meetings;
using TaskFlow.Domain.Enums.Planner;
using TaskFlow.Domain.Interfaces.Meetings;
using TaskFlow.Domain.Interfaces.Persistence;

namespace TaskFlow.Application.Features.Meetings;

public sealed record MeetingMessageDto(int Id, Guid ClientMessageId, int AuthorParticipantId,
    string AuthorName, string Body, int? ReplyToMessageId, DateTime CreatedAt);
public sealed record MeetingMessagePageDto(IReadOnlyList<MeetingMessageDto> Items, int Total, int Skip, int Take);
public sealed record MeetingNoteDto(string Content, int Version, int? LastEditedByParticipantId,
    string? LastEditedByName, DateTime? UpdatedAt, bool CanEdit);
public sealed record MeetingAssetDto(int Id, string FileName, string ContentType, long SizeBytes,
    string Sha256, MeetingAssetScanStatus ScanStatus, int UploaderParticipantId, string UploaderName,
    DateTime CreatedAt, bool CanDelete);
public sealed record MeetingAttendanceDto(int ParticipantId, string DisplayName, DateTime JoinedAtUtc,
    DateTime? LeftAtUtc, long DurationSeconds);
public sealed record MeetingArchiveDto(int MeetingId, string Title, MeetingStatus Status,
    DateTime? ActualStartUtc, DateTime? ActualEndUtc, DateTime RetainUntilUtc,
    IReadOnlyList<MeetingAttendanceDto> Attendance, MeetingMessagePageDto Messages,
    MeetingNoteDto Note, IReadOnlyList<MeetingAssetDto> Assets);
public sealed record MeetingAssetContentDto(string FileName, string ContentType, byte[] Content);

public sealed record GetMeetingMessagesQuery(int MeetingId, int Skip = 0, int Take = 100) : IRequest<MeetingMessagePageDto>;
public sealed record GetGuestMeetingMessagesQuery(string SessionToken, int Skip = 0, int Take = 100) : IRequest<MeetingMessagePageDto>;
public sealed record SendMeetingMessageCommand(int MeetingId, Guid ClientMessageId, string Body, int? ReplyToMessageId = null) : IRequest<MeetingMessageDto>;
public sealed record SendGuestMeetingMessageCommand(string SessionToken, Guid ClientMessageId, string Body, int? ReplyToMessageId = null) : IRequest<MeetingMessageDto>;
public sealed record GetMeetingNoteQuery(int MeetingId) : IRequest<MeetingNoteDto>;
public sealed record GetGuestMeetingNoteQuery(string SessionToken) : IRequest<MeetingNoteDto>;
public sealed record UpdateMeetingNoteCommand(int MeetingId, string Content, int ExpectedVersion) : IRequest<MeetingNoteDto>;
public sealed record UpdateGuestMeetingNoteCommand(string SessionToken, string Content, int ExpectedVersion) : IRequest<MeetingNoteDto>;
public sealed record GetMeetingAssetsQuery(int MeetingId) : IRequest<IReadOnlyList<MeetingAssetDto>>;
public sealed record GetGuestMeetingAssetsQuery(string SessionToken) : IRequest<IReadOnlyList<MeetingAssetDto>>;
public sealed record UploadMeetingAssetCommand(int MeetingId, string FileName, string ContentType, long Length, Stream Content, long MaxFileBytes) : IRequest<MeetingAssetDto>;
public sealed record UploadGuestMeetingAssetCommand(string SessionToken, string FileName, string ContentType, long Length, Stream Content, long MaxFileBytes) : IRequest<MeetingAssetDto>;
public sealed record DownloadMeetingAssetQuery(int MeetingId, int AssetId) : IRequest<MeetingAssetContentDto>;
public sealed record DownloadGuestMeetingAssetQuery(string SessionToken, int AssetId) : IRequest<MeetingAssetContentDto>;
public sealed record DeleteMeetingAssetCommand(int MeetingId, int AssetId) : IRequest;
public sealed record DeleteGuestMeetingAssetCommand(string SessionToken, int AssetId) : IRequest;
public sealed record GetMeetingArchiveQuery(int MeetingId) : IRequest<MeetingArchiveDto>;
public sealed record GetGuestMeetingArchiveQuery(string SessionToken) : IRequest<MeetingArchiveDto>;

public sealed class SendMeetingMessageCommandValidator : AbstractValidator<SendMeetingMessageCommand>
{ public SendMeetingMessageCommandValidator() { RuleFor(x => x.MeetingId).GreaterThan(0); RuleFor(x => x.ClientMessageId).NotEmpty(); RuleFor(x => x.Body).NotEmpty().MaximumLength(4000); } }
public sealed class UpdateMeetingNoteCommandValidator : AbstractValidator<UpdateMeetingNoteCommand>
{ public UpdateMeetingNoteCommandValidator() { RuleFor(x => x.MeetingId).GreaterThan(0); RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0); RuleFor(x => x.Content).MaximumLength(100000); } }

internal sealed record MeetingCollaborationActor(Meeting Meeting, MeetingParticipant Participant)
{
    public bool CanChat => Participant.AccessLevel != MeetingAccessLevel.Viewer || Meeting.ViewersCanChat;
    public bool CanEditNote => Participant.AccessLevel is MeetingAccessLevel.Host or MeetingAccessLevel.CoHost ||
        Participant.AccessLevel == MeetingAccessLevel.Participant && Meeting.ParticipantsCanEditNote;
    public bool CanDelete => Participant.AccessLevel == MeetingAccessLevel.Host;
}

internal sealed class MeetingCollaborationAccess(IMeetingRepository meetings,
    IMeetingGuestAccessRepository guestAccess)
{
    public async Task<MeetingCollaborationActor> ForUserAsync(int meetingId, int userId, CancellationToken ct)
    {
        var meeting = await meetings.GetByIdAsync(meetingId, ct) ?? throw new NotFoundException("MEETING_NOT_FOUND", "Meeting not found.");
        var participant = meeting.Participants.SingleOrDefault(x => x.UserId == userId && !x.IsDeleted)
            ?? throw new ForbiddenException("MEETING_COLLABORATION_DENIED", "You are not assigned to this meeting.");
        EnsureParticipant(participant); EnsureRetained(meeting); return new(meeting, participant);
    }
    public async Task<MeetingCollaborationActor> ForGuestAsync(string token, CancellationToken ct)
    {
        var session = await guestAccess.GetSessionByHashAsync(MeetingGuestAccessRules.Hash(token), ct);
        if (session is null || !session.IsActive(DateTime.UtcNow))
            throw new UnauthorizedException("MEETING_GUEST_SESSION_INVALID", "Your meeting session has expired. Verify your email again.");
        var meeting = await meetings.GetByIdAsync(session.MeetingId, ct) ?? throw new NotFoundException("MEETING_NOT_FOUND", "Meeting not found.");
        var participant = meeting.Participants.SingleOrDefault(x => x.Id == session.ParticipantId && !x.IsDeleted)
            ?? throw new UnauthorizedException("MEETING_GUEST_SESSION_INVALID", "Your meeting access is no longer available.");
        EnsureParticipant(participant); EnsureRetained(meeting); return new(meeting, participant);
    }
    public static void EnsureWritable(MeetingCollaborationActor actor)
    { if (actor.Meeting.Status != MeetingStatus.Live) throw new BusinessException("MEETING_COLLABORATION_READ_ONLY", "Collaboration is writable only while the meeting is live."); }
    public static void EnsureParticipant(MeetingParticipant participant)
    { if (participant.State != MeetingParticipantState.Admitted) throw new ForbiddenException("MEETING_COLLABORATION_DENIED", "Meeting access is not admitted."); }
    public static DateTime RetainUntil(Meeting meeting) => (meeting.ActualEndUtc ?? meeting.ScheduledEndUtc ?? meeting.CreatedAt).AddDays(meeting.RetentionDays);
    public static void EnsureRetained(Meeting meeting)
    { if (meeting.Status == MeetingStatus.Ended && DateTime.UtcNow >= RetainUntil(meeting)) throw new BusinessException("MEETING_ARCHIVE_EXPIRED", "This meeting archive has expired."); }
}

internal static class MeetingCollaborationRead
{
    public static async Task<MeetingMessagePageDto> MessagesAsync(ISqlConnectionFactory sql, int meetingId, int skip, int take, CancellationToken ct)
    {
        skip = Math.Max(0, skip); take = Math.Clamp(take, 1, 200);
        const string countSql = "SELECT COUNT(*)::int FROM \"MeetingMessages\" WHERE \"MeetingId\"=@MeetingId AND \"IsDeleted\"=FALSE;";
        const string rowsSql = """
            SELECT m."Id", m."ClientMessageId", m."AuthorParticipantId",
                   COALESCE(p."DisplayName", u."FirstName" || ' ' || u."LastName", 'Participant') AS "AuthorName",
                   m."Body", m."ReplyToMessageId", m."CreatedAt"
            FROM "MeetingMessages" m JOIN "MeetingParticipants" p ON p."Id"=m."AuthorParticipantId"
            LEFT JOIN "Users" u ON u."Id"=p."UserId"
            WHERE m."MeetingId"=@MeetingId AND m."IsDeleted"=FALSE
            ORDER BY m."CreatedAt", m."Id" OFFSET @Skip LIMIT @Take;
            """;
        using var connection = sql.Create(); var args = new { MeetingId = meetingId, Skip = skip, Take = take };
        var total = await connection.QuerySingleAsync<int>(new CommandDefinition(countSql, args, cancellationToken: ct));
        var rows = (await connection.QueryAsync<MeetingMessageDto>(new CommandDefinition(rowsSql, args, cancellationToken: ct))).ToList();
        return new(rows, total, skip, take);
    }
    public static async Task<MeetingNoteDto> NoteAsync(ISqlConnectionFactory sql, MeetingCollaborationActor actor, CancellationToken ct)
    {
        const string query = """
            SELECT n."Content", n."Version", n."LastEditedByParticipantId",
                   COALESCE(p."DisplayName", u."FirstName" || ' ' || u."LastName") AS "LastEditedByName", n."UpdatedAt"
            FROM "MeetingNotes" n LEFT JOIN "MeetingParticipants" p ON p."Id"=n."LastEditedByParticipantId"
            LEFT JOIN "Users" u ON u."Id"=p."UserId" WHERE n."MeetingId"=@MeetingId AND n."IsDeleted"=FALSE;
            """;
        using var connection = sql.Create();
        var row = await connection.QuerySingleOrDefaultAsync<NoteRow>(new CommandDefinition(query, new { MeetingId = actor.Meeting.Id }, cancellationToken: ct));
        return row is null ? new("", 0, null, null, null, actor.CanEditNote) : new(row.Content, row.Version, row.LastEditedByParticipantId, row.LastEditedByName, row.UpdatedAt, actor.CanEditNote);
    }
    public static async Task<IReadOnlyList<MeetingAssetDto>> AssetsAsync(ISqlConnectionFactory sql, MeetingCollaborationActor actor, CancellationToken ct)
    {
        const string query = """
            SELECT a."Id", a."FileName", a."ContentType", a."SizeBytes", a."Sha256", a."ScanStatus",
                   a."UploaderParticipantId", COALESCE(p."DisplayName", u."FirstName" || ' ' || u."LastName", 'Participant') AS "UploaderName",
                   a."CreatedAt", (@CanDelete OR a."UploaderParticipantId"=@ParticipantId) AS "CanDelete"
            FROM "MeetingAssets" a JOIN "MeetingParticipants" p ON p."Id"=a."UploaderParticipantId"
            LEFT JOIN "Users" u ON u."Id"=p."UserId" WHERE a."MeetingId"=@MeetingId AND a."IsDeleted"=FALSE
            ORDER BY a."CreatedAt", a."Id";
            """;
        using var connection = sql.Create();
        return (await connection.QueryAsync<MeetingAssetDto>(new CommandDefinition(query,
            new { MeetingId = actor.Meeting.Id, actor.Participant.Id, ParticipantId = actor.Participant.Id, actor.CanDelete }, cancellationToken: ct))).ToList();
    }
    private sealed record NoteRow(string Content, int Version, int? LastEditedByParticipantId, string? LastEditedByName, DateTime? UpdatedAt);
}

public sealed class GetMeetingMessagesQueryHandler(IMeetingRepository meetings, IMeetingGuestAccessRepository guests, ICurrentUserService user, ISqlConnectionFactory sql) : IRequestHandler<GetMeetingMessagesQuery, MeetingMessagePageDto>
{ public async Task<MeetingMessagePageDto> Handle(GetMeetingMessagesQuery r, CancellationToken ct) { await new MeetingCollaborationAccess(meetings, guests).ForUserAsync(r.MeetingId, user.UserId, ct); return await MeetingCollaborationRead.MessagesAsync(sql, r.MeetingId, r.Skip, r.Take, ct); } }
public sealed class GetGuestMeetingMessagesQueryHandler(IMeetingRepository meetings, IMeetingGuestAccessRepository guests, ISqlConnectionFactory sql) : IRequestHandler<GetGuestMeetingMessagesQuery, MeetingMessagePageDto>
{ public async Task<MeetingMessagePageDto> Handle(GetGuestMeetingMessagesQuery r, CancellationToken ct) { var a = await new MeetingCollaborationAccess(meetings, guests).ForGuestAsync(r.SessionToken, ct); return await MeetingCollaborationRead.MessagesAsync(sql, a.Meeting.Id, r.Skip, r.Take, ct); } }

internal static class MeetingMessageWrite
{
    public static async Task<MeetingMessageDto> SendAsync(MeetingCollaborationActor actor, Guid clientId, string body, int? reply,
        IMeetingCollaborationRepository collaboration, IUnitOfWork uow, CancellationToken ct)
    {
        MeetingCollaborationAccess.EnsureWritable(actor);
        if (!actor.CanChat) throw new ForbiddenException("MEETING_CHAT_DENIED", "Your meeting access does not allow chat.");
        var existing = await collaboration.GetMessageByClientIdAsync(actor.Meeting.Id, actor.Participant.Id, clientId, ct);
        if (existing is not null) return ToDto(existing, actor.Participant);
        // A reply id arrives from the client and was never checked against this meeting, so a
        // participant could anchor a message to a thread in a meeting they cannot see.
        if (reply.HasValue && await collaboration.GetMessageAsync(actor.Meeting.Id, reply.Value, ct) is null)
            throw new NotFoundException("MEETING_MESSAGE_NOT_FOUND", "The message being replied to is not part of this meeting.");
        var message = new MeetingMessage(actor.Meeting.Id, actor.Participant.Id, clientId, body, reply);
        await collaboration.AddMessageAsync(message, ct); await uow.SaveChangesAsync(ct); return ToDto(message, actor.Participant);
    }
    private static MeetingMessageDto ToDto(MeetingMessage m, MeetingParticipant p) => new(m.Id, m.ClientMessageId,
        m.AuthorParticipantId, p.DisplayName ?? p.NormalizedEmail?.Split('@')[0] ?? "Participant", m.Body, m.ReplyToMessageId, m.CreatedAt);
}
public sealed class SendMeetingMessageCommandHandler(IMeetingRepository meetings, IMeetingGuestAccessRepository guests, IMeetingCollaborationRepository collaboration, ICurrentUserService user, IUnitOfWork uow) : IRequestHandler<SendMeetingMessageCommand, MeetingMessageDto>
{ public async Task<MeetingMessageDto> Handle(SendMeetingMessageCommand r, CancellationToken ct) => await MeetingMessageWrite.SendAsync(await new MeetingCollaborationAccess(meetings, guests).ForUserAsync(r.MeetingId, user.UserId, ct), r.ClientMessageId, r.Body, r.ReplyToMessageId, collaboration, uow, ct); }
public sealed class SendGuestMeetingMessageCommandHandler(IMeetingRepository meetings, IMeetingGuestAccessRepository guests, IMeetingCollaborationRepository collaboration, IUnitOfWork uow) : IRequestHandler<SendGuestMeetingMessageCommand, MeetingMessageDto>
{ public async Task<MeetingMessageDto> Handle(SendGuestMeetingMessageCommand r, CancellationToken ct) => await MeetingMessageWrite.SendAsync(await new MeetingCollaborationAccess(meetings, guests).ForGuestAsync(r.SessionToken, ct), r.ClientMessageId, r.Body, r.ReplyToMessageId, collaboration, uow, ct); }

public sealed class GetMeetingNoteQueryHandler(IMeetingRepository meetings, IMeetingGuestAccessRepository guests, ICurrentUserService user, ISqlConnectionFactory sql) : IRequestHandler<GetMeetingNoteQuery, MeetingNoteDto>
{ public async Task<MeetingNoteDto> Handle(GetMeetingNoteQuery r, CancellationToken ct) => await MeetingCollaborationRead.NoteAsync(sql, await new MeetingCollaborationAccess(meetings, guests).ForUserAsync(r.MeetingId, user.UserId, ct), ct); }
public sealed class GetGuestMeetingNoteQueryHandler(IMeetingRepository meetings, IMeetingGuestAccessRepository guests, ISqlConnectionFactory sql) : IRequestHandler<GetGuestMeetingNoteQuery, MeetingNoteDto>
{ public async Task<MeetingNoteDto> Handle(GetGuestMeetingNoteQuery r, CancellationToken ct) => await MeetingCollaborationRead.NoteAsync(sql, await new MeetingCollaborationAccess(meetings, guests).ForGuestAsync(r.SessionToken, ct), ct); }

internal static class MeetingNoteWrite
{
    public static async Task<MeetingNoteDto> UpdateAsync(MeetingCollaborationActor actor, string content, int expectedVersion,
        IMeetingCollaborationRepository collaboration, IUnitOfWork uow, CancellationToken ct)
    {
        MeetingCollaborationAccess.EnsureWritable(actor);
        if (!actor.CanEditNote) throw new ForbiddenException("MEETING_NOTE_EDIT_DENIED", "Your meeting access does not allow note editing.");
        var note = await collaboration.GetNoteAsync(actor.Meeting.Id, ct);
        if (note is null) { note = new MeetingNote(actor.Meeting.Id); await collaboration.AddNoteAsync(note, ct); await uow.SaveChangesAsync(ct); }
        try { note.Update(content, expectedVersion, actor.Participant.Id); }
        catch (InvalidOperationException) { throw new ConflictException("MEETING_NOTE_CONFLICT", "The note changed in another session. Reload it before saving again."); }
        collaboration.UpdateNote(note); await collaboration.AddNoteRevisionAsync(new MeetingNoteRevision(actor.Meeting.Id,
            note.Id, note.Version, note.Content, actor.Participant.Id), ct); await uow.SaveChangesAsync(ct);
        return new(note.Content, note.Version, actor.Participant.Id, actor.Participant.DisplayName, note.UpdatedAt, true);
    }
}
public sealed class UpdateMeetingNoteCommandHandler(IMeetingRepository meetings, IMeetingGuestAccessRepository guests, IMeetingCollaborationRepository collaboration, ICurrentUserService user, IUnitOfWork uow) : IRequestHandler<UpdateMeetingNoteCommand, MeetingNoteDto>
{ public async Task<MeetingNoteDto> Handle(UpdateMeetingNoteCommand r, CancellationToken ct) => await MeetingNoteWrite.UpdateAsync(await new MeetingCollaborationAccess(meetings, guests).ForUserAsync(r.MeetingId, user.UserId, ct), r.Content, r.ExpectedVersion, collaboration, uow, ct); }
public sealed class UpdateGuestMeetingNoteCommandHandler(IMeetingRepository meetings, IMeetingGuestAccessRepository guests, IMeetingCollaborationRepository collaboration, IUnitOfWork uow) : IRequestHandler<UpdateGuestMeetingNoteCommand, MeetingNoteDto>
{ public async Task<MeetingNoteDto> Handle(UpdateGuestMeetingNoteCommand r, CancellationToken ct) => await MeetingNoteWrite.UpdateAsync(await new MeetingCollaborationAccess(meetings, guests).ForGuestAsync(r.SessionToken, ct), r.Content, r.ExpectedVersion, collaboration, uow, ct); }

public sealed class GetMeetingAssetsQueryHandler(IMeetingRepository meetings, IMeetingGuestAccessRepository guests, ICurrentUserService user, ISqlConnectionFactory sql) : IRequestHandler<GetMeetingAssetsQuery, IReadOnlyList<MeetingAssetDto>>
{ public async Task<IReadOnlyList<MeetingAssetDto>> Handle(GetMeetingAssetsQuery r, CancellationToken ct) => await MeetingCollaborationRead.AssetsAsync(sql, await new MeetingCollaborationAccess(meetings, guests).ForUserAsync(r.MeetingId, user.UserId, ct), ct); }
public sealed class GetGuestMeetingAssetsQueryHandler(IMeetingRepository meetings, IMeetingGuestAccessRepository guests, ISqlConnectionFactory sql) : IRequestHandler<GetGuestMeetingAssetsQuery, IReadOnlyList<MeetingAssetDto>>
{ public async Task<IReadOnlyList<MeetingAssetDto>> Handle(GetGuestMeetingAssetsQuery r, CancellationToken ct) => await MeetingCollaborationRead.AssetsAsync(sql, await new MeetingCollaborationAccess(meetings, guests).ForGuestAsync(r.SessionToken, ct), ct); }

internal static class MeetingAssetWrite
{
    private static readonly Dictionary<string, string[]> Allowed = new(StringComparer.OrdinalIgnoreCase)
    { ["application/pdf"] = [".pdf"], ["image/png"] = [".png"], ["image/jpeg"] = [".jpg", ".jpeg"], ["text/plain"] = [".txt"], ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = [".docx"] };
    public static async Task<MeetingAssetDto> UploadAsync(MeetingCollaborationActor actor, string fileName, string contentType,
        long length, Stream input, long maxBytes, IMeetingCollaborationRepository collaboration, IObjectStorage storage,
        IPlannerAssetScanner scanner, IUnitOfWork uow, CancellationToken ct)
    {
        MeetingCollaborationAccess.EnsureWritable(actor);
        if (!actor.CanChat) throw new ForbiddenException("MEETING_FILE_UPLOAD_DENIED", "Your meeting access does not allow file sharing.");
        var safeName = Path.GetFileName(fileName).Trim(); var extension = Path.GetExtension(safeName);
        if (safeName.Length is < 1 or > 255 || !Allowed.TryGetValue(contentType, out var extensions) || !extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            throw new BusinessException("MEETING_FILE_TYPE_DENIED", "This file type is not allowed.");
        if (length <= 0 || length > maxBytes) throw new BusinessException("MEETING_FILE_TOO_LARGE", $"Files must be smaller than {maxBytes / 1048576} MB.");
        if (await collaboration.GetAssetBytesAsync(actor.Meeting.Id, ct) + length > maxBytes * 10)
            throw new BusinessException("MEETING_FILE_QUOTA_EXCEEDED", "This meeting has reached its file storage quota.");
        await using var buffer = new MemoryStream((int)Math.Min(length, maxBytes));
        var limited = new byte[81920]; long total = 0; int read;
        while ((read = await input.ReadAsync(limited, ct)) > 0) { total += read; if (total > maxBytes) throw new BusinessException("MEETING_FILE_TOO_LARGE", "The uploaded file exceeds the size limit."); await buffer.WriteAsync(limited.AsMemory(0, read), ct); }
        if (total != length) throw new BusinessException("MEETING_FILE_LENGTH_INVALID", "The uploaded file length did not match its metadata.");
        ValidateSignature(buffer.GetBuffer().AsSpan(0, (int)buffer.Length), contentType);
        var sha = Convert.ToHexString(SHA256.HashData(buffer.GetBuffer().AsSpan(0, (int)buffer.Length)));
        var key = $"meetings/{actor.Meeting.Id}/{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        buffer.Position = 0; await storage.UploadAsync(key, buffer, contentType, ct);
        var asset = new MeetingAsset(actor.Meeting.Id, actor.Participant.Id, key, safeName, contentType,
            total, sha, MeetingCollaborationAccess.RetainUntil(actor.Meeting));
        try { await collaboration.AddAssetAsync(asset, ct); await uow.SaveChangesAsync(ct); }
        catch { await storage.DeleteAsync(key, ct); throw; }
        var status = await scanner.ScanAsync(key, ct);
        asset.SetScanStatus(status == PlannerAssetScanStatus.Clean ? MeetingAssetScanStatus.Clean : status == PlannerAssetScanStatus.Rejected ? MeetingAssetScanStatus.Rejected : MeetingAssetScanStatus.Failed);
        collaboration.UpdateAsset(asset); await uow.SaveChangesAsync(ct);
        return new(asset.Id, asset.FileName, asset.ContentType, asset.SizeBytes, asset.Sha256, asset.ScanStatus,
            asset.UploaderParticipantId, actor.Participant.DisplayName ?? "Participant", asset.CreatedAt, true);
    }
    private static void ValidateSignature(ReadOnlySpan<byte> content, string type)
    {
        var valid = type switch { "application/pdf" => content.StartsWith("%PDF"u8), "image/png" => content.StartsWith(new byte[] {137,80,78,71,13,10,26,10}),
            "image/jpeg" => content.Length >= 3 && content[0] == 0xff && content[1] == 0xd8 && content[2] == 0xff,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => content.Length >= 4 && content[0] == 0x50 && content[1] == 0x4b,
            "text/plain" => !content.Contains((byte)0), _ => false };
        if (!valid) throw new BusinessException("MEETING_FILE_SIGNATURE_INVALID", "The file content does not match its declared type.");
    }
}

public sealed class UploadMeetingAssetCommandHandler(IMeetingRepository meetings, IMeetingGuestAccessRepository guests, IMeetingCollaborationRepository collaboration, ICurrentUserService user, IObjectStorage storage, IPlannerAssetScanner scanner, IUnitOfWork uow) : IRequestHandler<UploadMeetingAssetCommand, MeetingAssetDto>
{ public async Task<MeetingAssetDto> Handle(UploadMeetingAssetCommand r, CancellationToken ct) => await MeetingAssetWrite.UploadAsync(await new MeetingCollaborationAccess(meetings, guests).ForUserAsync(r.MeetingId, user.UserId, ct), r.FileName, r.ContentType, r.Length, r.Content, r.MaxFileBytes, collaboration, storage, scanner, uow, ct); }
public sealed class UploadGuestMeetingAssetCommandHandler(IMeetingRepository meetings, IMeetingGuestAccessRepository guests, IMeetingCollaborationRepository collaboration, IObjectStorage storage, IPlannerAssetScanner scanner, IUnitOfWork uow) : IRequestHandler<UploadGuestMeetingAssetCommand, MeetingAssetDto>
{ public async Task<MeetingAssetDto> Handle(UploadGuestMeetingAssetCommand r, CancellationToken ct) => await MeetingAssetWrite.UploadAsync(await new MeetingCollaborationAccess(meetings, guests).ForGuestAsync(r.SessionToken, ct), r.FileName, r.ContentType, r.Length, r.Content, r.MaxFileBytes, collaboration, storage, scanner, uow, ct); }

internal static class MeetingAssetRead
{
    public static async Task<MeetingAssetContentDto> DownloadAsync(MeetingCollaborationActor actor, int assetId, IMeetingCollaborationRepository collaboration, IObjectStorage storage, CancellationToken ct)
    {
        var asset = await collaboration.GetAssetAsync(actor.Meeting.Id, assetId, ct) ?? throw new NotFoundException("MEETING_ASSET_NOT_FOUND", "Meeting file not found.");
        if (asset.ScanStatus != MeetingAssetScanStatus.Clean) throw new BusinessException("MEETING_ASSET_UNAVAILABLE", "This file is not available for download.");
        var value = await storage.DownloadAsync(asset.StorageKey, ct); return new(asset.FileName, asset.ContentType, value.Content);
    }
}
public sealed class DownloadMeetingAssetQueryHandler(IMeetingRepository meetings, IMeetingGuestAccessRepository guests, IMeetingCollaborationRepository collaboration, ICurrentUserService user, IObjectStorage storage) : IRequestHandler<DownloadMeetingAssetQuery, MeetingAssetContentDto>
{ public async Task<MeetingAssetContentDto> Handle(DownloadMeetingAssetQuery r, CancellationToken ct) => await MeetingAssetRead.DownloadAsync(await new MeetingCollaborationAccess(meetings, guests).ForUserAsync(r.MeetingId, user.UserId, ct), r.AssetId, collaboration, storage, ct); }
public sealed class DownloadGuestMeetingAssetQueryHandler(IMeetingRepository meetings, IMeetingGuestAccessRepository guests, IMeetingCollaborationRepository collaboration, IObjectStorage storage) : IRequestHandler<DownloadGuestMeetingAssetQuery, MeetingAssetContentDto>
{ public async Task<MeetingAssetContentDto> Handle(DownloadGuestMeetingAssetQuery r, CancellationToken ct) => await MeetingAssetRead.DownloadAsync(await new MeetingCollaborationAccess(meetings, guests).ForGuestAsync(r.SessionToken, ct), r.AssetId, collaboration, storage, ct); }

public sealed class DeleteMeetingAssetCommandHandler(IMeetingRepository meetings, IMeetingGuestAccessRepository guests, IMeetingCollaborationRepository collaboration, ICurrentUserService user, IObjectStorage storage, IUnitOfWork uow) : IRequestHandler<DeleteMeetingAssetCommand>
{
    public async Task Handle(DeleteMeetingAssetCommand r, CancellationToken ct)
    {
        var actor = await new MeetingCollaborationAccess(meetings, guests).ForUserAsync(r.MeetingId, user.UserId, ct);
        await DeleteAsync(actor, r.AssetId, collaboration, storage, uow, ct);
    }
    internal static async Task DeleteAsync(MeetingCollaborationActor actor, int assetId,
        IMeetingCollaborationRepository collaboration, IObjectStorage storage, IUnitOfWork uow, CancellationToken ct)
    {
        var asset = await collaboration.GetAssetAsync(actor.Meeting.Id, assetId, ct) ?? throw new NotFoundException("MEETING_ASSET_NOT_FOUND", "Meeting file not found.");
        if (!actor.CanDelete && asset.UploaderParticipantId != actor.Participant.Id) throw new ForbiddenException("MEETING_ASSET_DELETE_DENIED", "Only the host or uploader can delete this file.");
        await storage.DeleteAsync(asset.StorageKey, ct); asset.SoftDelete(); collaboration.UpdateAsset(asset); await uow.SaveChangesAsync(ct);
    }
}

public sealed class DeleteGuestMeetingAssetCommandHandler(IMeetingRepository meetings, IMeetingGuestAccessRepository guests, IMeetingCollaborationRepository collaboration, IObjectStorage storage, IUnitOfWork uow) : IRequestHandler<DeleteGuestMeetingAssetCommand>
{ public async Task Handle(DeleteGuestMeetingAssetCommand r, CancellationToken ct) => await DeleteMeetingAssetCommandHandler.DeleteAsync(await new MeetingCollaborationAccess(meetings, guests).ForGuestAsync(r.SessionToken, ct), r.AssetId, collaboration, storage, uow, ct); }

internal static class MeetingArchiveRead
{
    public static async Task<MeetingArchiveDto> ReadAsync(ISqlConnectionFactory sql, MeetingCollaborationActor actor, CancellationToken ct)
    {
        const string attendanceSql = """
            SELECT a."ParticipantId", COALESCE(p."DisplayName", u."FirstName" || ' ' || u."LastName", 'Participant') AS "DisplayName",
                   a."JoinedAtUtc", a."LeftAtUtc", GREATEST(0, EXTRACT(EPOCH FROM (COALESCE(a."LeftAtUtc", NOW())-a."JoinedAtUtc")))::bigint AS "DurationSeconds"
            FROM "MeetingAttendance" a JOIN "MeetingParticipants" p ON p."Id"=a."ParticipantId" LEFT JOIN "Users" u ON u."Id"=p."UserId"
            WHERE a."MeetingId"=@MeetingId AND a."IsDeleted"=FALSE ORDER BY a."JoinedAtUtc", a."Id";
            """;
        using var connection = sql.Create();
        var attendance = (await connection.QueryAsync<MeetingAttendanceDto>(new CommandDefinition(attendanceSql, new { MeetingId = actor.Meeting.Id }, cancellationToken: ct))).ToList();
        var messages = await MeetingCollaborationRead.MessagesAsync(sql, actor.Meeting.Id, 0, 200, ct);
        var note = await MeetingCollaborationRead.NoteAsync(sql, actor, ct); var assets = await MeetingCollaborationRead.AssetsAsync(sql, actor, ct);
        return new(actor.Meeting.Id, actor.Meeting.Title, actor.Meeting.Status, actor.Meeting.ActualStartUtc,
            actor.Meeting.ActualEndUtc, MeetingCollaborationAccess.RetainUntil(actor.Meeting), attendance, messages, note, assets);
    }
}
public sealed class GetMeetingArchiveQueryHandler(IMeetingRepository meetings, IMeetingGuestAccessRepository guests, ICurrentUserService user, ISqlConnectionFactory sql) : IRequestHandler<GetMeetingArchiveQuery, MeetingArchiveDto>
{ public async Task<MeetingArchiveDto> Handle(GetMeetingArchiveQuery r, CancellationToken ct) => await MeetingArchiveRead.ReadAsync(sql, await new MeetingCollaborationAccess(meetings, guests).ForUserAsync(r.MeetingId, user.UserId, ct), ct); }
public sealed class GetGuestMeetingArchiveQueryHandler(IMeetingRepository meetings, IMeetingGuestAccessRepository guests, ISqlConnectionFactory sql) : IRequestHandler<GetGuestMeetingArchiveQuery, MeetingArchiveDto>
{ public async Task<MeetingArchiveDto> Handle(GetGuestMeetingArchiveQuery r, CancellationToken ct) => await MeetingArchiveRead.ReadAsync(sql, await new MeetingCollaborationAccess(meetings, guests).ForGuestAsync(r.SessionToken, ct), ct); }

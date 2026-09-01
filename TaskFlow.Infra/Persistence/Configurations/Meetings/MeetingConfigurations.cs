using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Domain.Entities.Identity;
using TaskFlow.Domain.Entities.Meetings;
using OrganizationEntity = TaskFlow.Domain.Entities.Organization.Organization;

namespace TaskFlow.Infra.Persistence.Configurations.Meetings;

public sealed class MeetingConfiguration : IEntityTypeConfiguration<Meeting>
{
    public void Configure(EntityTypeBuilder<Meeting> builder)
    {
        builder.ToTable("Meetings"); builder.HasKey(x => x.Id); builder.Ignore(x => x.DomainEvents);
        builder.Property(x => x.Title).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.TimeZone).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RoomName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.HasIndex(x => x.RoomName).IsUnique();
        builder.HasIndex(x => new { x.OrganizationId, x.Status, x.ScheduledStartUtc });
        builder.HasOne<OrganizationEntity>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Badges).WithOne().HasForeignKey(x => x.MeetingId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Participants).WithOne().HasForeignKey(x => x.MeetingId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.AccessLinks).WithOne().HasForeignKey(x => x.MeetingId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Attendance).WithOne().HasForeignKey(x => x.MeetingId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class MeetingBadgeDefinitionConfiguration : IEntityTypeConfiguration<MeetingBadgeDefinition>
{
    public void Configure(EntityTypeBuilder<MeetingBadgeDefinition> builder)
    {
        builder.ToTable("MeetingBadgeDefinitions"); builder.HasKey(x => x.Id); builder.Ignore(x => x.DomainEvents);
        builder.Property(x => x.Label).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Color).HasMaxLength(24).IsRequired(); builder.Property(x => x.Icon).HasMaxLength(40);
        builder.HasIndex(x => new { x.MeetingId, x.Label }).IsUnique().HasFilter("\"IsDeleted\" = FALSE");
    }
}

public sealed class MeetingParticipantConfiguration : IEntityTypeConfiguration<MeetingParticipant>
{
    public void Configure(EntityTypeBuilder<MeetingParticipant> builder)
    {
        builder.ToTable("MeetingParticipants"); builder.HasKey(x => x.Id); builder.Ignore(x => x.DomainEvents);
        builder.Property(x => x.NormalizedEmail).HasMaxLength(320); builder.Property(x => x.DisplayName).HasMaxLength(120);
        builder.Property(x => x.AccessLevel).HasConversion<int>(); builder.Property(x => x.State).HasConversion<int>();
        builder.HasIndex(x => new { x.MeetingId, x.UserId }).IsUnique().HasFilter("\"UserId\" IS NOT NULL AND \"IsDeleted\" = FALSE");
        builder.HasIndex(x => new { x.MeetingId, x.NormalizedEmail }).HasFilter("\"NormalizedEmail\" IS NOT NULL AND \"IsDeleted\" = FALSE");
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MeetingBadgeDefinition>().WithMany().HasForeignKey(x => x.BadgeDefinitionId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class MeetingAccessLinkConfiguration : IEntityTypeConfiguration<MeetingAccessLink>
{
    public void Configure(EntityTypeBuilder<MeetingAccessLink> builder)
    {
        builder.ToTable("MeetingAccessLinks"); builder.HasKey(x => x.Id); builder.Ignore(x => x.DomainEvents);
        builder.Property(x => x.TokenHash).HasMaxLength(64).IsRequired(); builder.Property(x => x.LockedEmail).HasMaxLength(320);
        builder.Property(x => x.Mode).HasConversion<int>(); builder.Property(x => x.DefaultAccessLevel).HasConversion<int>();
        builder.HasIndex(x => x.TokenHash).IsUnique(); builder.HasIndex(x => new { x.MeetingId, x.ExpiresAtUtc });
        builder.HasOne<MeetingBadgeDefinition>().WithMany().HasForeignKey(x => x.BadgeDefinitionId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class MeetingAttendanceConfiguration : IEntityTypeConfiguration<MeetingAttendance>
{
    public void Configure(EntityTypeBuilder<MeetingAttendance> builder)
    {
        builder.ToTable("MeetingAttendance"); builder.HasKey(x => x.Id); builder.Ignore(x => x.DomainEvents);
        builder.Property(x => x.ProviderConnectionId).HasMaxLength(120).IsRequired();
        builder.Property(x => x.ProviderParticipantSid).HasMaxLength(120);
        builder.HasIndex(x => new { x.MeetingId, x.ProviderConnectionId }).IsUnique();
        builder.HasIndex(x => new { x.MeetingId, x.LeftAtUtc });
        builder.HasOne<MeetingParticipant>().WithMany().HasForeignKey(x => x.ParticipantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MeetingWebhookReceiptConfiguration : IEntityTypeConfiguration<MeetingWebhookReceipt>
{
    public void Configure(EntityTypeBuilder<MeetingWebhookReceipt> builder)
    {
        builder.ToTable("MeetingWebhookReceipts"); builder.HasKey(x => x.Id); builder.Ignore(x => x.DomainEvents);
        builder.Property(x => x.ProviderEventId).HasMaxLength(120).IsRequired();
        builder.Property(x => x.EventType).HasMaxLength(80).IsRequired();
        builder.HasIndex(x => x.ProviderEventId).IsUnique();
        builder.HasIndex(x => new { x.MeetingId, x.OccurredAtUtc });
        builder.HasOne<Meeting>().WithMany().HasForeignKey(x => x.MeetingId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class MeetingGuestChallengeConfiguration : IEntityTypeConfiguration<MeetingGuestChallenge>
{
    public void Configure(EntityTypeBuilder<MeetingGuestChallenge> builder)
    {
        builder.ToTable("MeetingGuestChallenges"); builder.HasKey(x => x.Id); builder.Ignore(x => x.DomainEvents);
        builder.Property(x => x.NormalizedEmail).HasMaxLength(320).IsRequired(); builder.Property(x => x.CodeHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => new { x.AccessLinkId, x.NormalizedEmail, x.CreatedAt });
        builder.HasOne<Meeting>().WithMany().HasForeignKey(x => x.MeetingId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MeetingAccessLink>().WithMany().HasForeignKey(x => x.AccessLinkId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class MeetingGuestSessionConfiguration : IEntityTypeConfiguration<MeetingGuestSession>
{
    public void Configure(EntityTypeBuilder<MeetingGuestSession> builder)
    {
        builder.ToTable("MeetingGuestSessions"); builder.HasKey(x => x.Id); builder.Ignore(x => x.DomainEvents);
        builder.Property(x => x.TokenHash).HasMaxLength(64).IsRequired(); builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => new { x.MeetingId, x.ParticipantId, x.ExpiresAtUtc });
        builder.HasOne<Meeting>().WithMany().HasForeignKey(x => x.MeetingId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MeetingParticipant>().WithMany().HasForeignKey(x => x.ParticipantId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class MeetingGuestDecisionConfiguration : IEntityTypeConfiguration<MeetingGuestDecision>
{
    public void Configure(EntityTypeBuilder<MeetingGuestDecision> builder)
    {
        builder.ToTable("MeetingGuestDecisions"); builder.HasKey(x => x.Id); builder.Ignore(x => x.DomainEvents);
        builder.Property(x => x.Kind).HasConversion<int>(); builder.HasIndex(x => new { x.MeetingId, x.ParticipantId, x.CreatedAt });
        builder.HasOne<Meeting>().WithMany().HasForeignKey(x => x.MeetingId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MeetingParticipant>().WithMany().HasForeignKey(x => x.ParticipantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MeetingMessageConfiguration : IEntityTypeConfiguration<MeetingMessage>
{
    public void Configure(EntityTypeBuilder<MeetingMessage> builder)
    {
        builder.ToTable("MeetingMessages"); builder.HasKey(x => x.Id); builder.Ignore(x => x.DomainEvents);
        builder.Property(x => x.Body).HasMaxLength(4000).IsRequired();
        builder.HasIndex(x => new { x.MeetingId, x.AuthorParticipantId, x.ClientMessageId }).IsUnique();
        builder.HasIndex(x => new { x.MeetingId, x.CreatedAt, x.Id });
        builder.HasOne<Meeting>().WithMany().HasForeignKey(x => x.MeetingId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MeetingParticipant>().WithMany().HasForeignKey(x => x.AuthorParticipantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MeetingMessage>().WithMany().HasForeignKey(x => x.ReplyToMessageId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class MeetingNoteConfiguration : IEntityTypeConfiguration<MeetingNote>
{
    public void Configure(EntityTypeBuilder<MeetingNote> builder)
    {
        builder.ToTable("MeetingNotes"); builder.HasKey(x => x.Id); builder.Ignore(x => x.DomainEvents);
        builder.Property(x => x.Content).HasMaxLength(100000).IsRequired(); builder.HasIndex(x => x.MeetingId).IsUnique();
        builder.HasOne<Meeting>().WithMany().HasForeignKey(x => x.MeetingId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MeetingParticipant>().WithMany().HasForeignKey(x => x.LastEditedByParticipantId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class MeetingNoteRevisionConfiguration : IEntityTypeConfiguration<MeetingNoteRevision>
{
    public void Configure(EntityTypeBuilder<MeetingNoteRevision> builder)
    {
        builder.ToTable("MeetingNoteRevisions"); builder.HasKey(x => x.Id); builder.Ignore(x => x.DomainEvents);
        builder.Property(x => x.Content).HasMaxLength(100000).IsRequired();
        builder.HasIndex(x => new { x.NoteId, x.Version }).IsUnique();
        builder.HasOne<Meeting>().WithMany().HasForeignKey(x => x.MeetingId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MeetingNote>().WithMany().HasForeignKey(x => x.NoteId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MeetingParticipant>().WithMany().HasForeignKey(x => x.EditorParticipantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MeetingAssetConfiguration : IEntityTypeConfiguration<MeetingAsset>
{
    public void Configure(EntityTypeBuilder<MeetingAsset> builder)
    {
        builder.ToTable("MeetingAssets"); builder.HasKey(x => x.Id); builder.Ignore(x => x.DomainEvents);
        builder.Property(x => x.StorageKey).HasMaxLength(512).IsRequired(); builder.Property(x => x.FileName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(120).IsRequired(); builder.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ScanStatus).HasConversion<int>(); builder.HasIndex(x => x.StorageKey).IsUnique();
        builder.HasIndex(x => new { x.MeetingId, x.CreatedAt }); builder.HasIndex(x => new { x.RetainUntilUtc, x.IsDeleted });
        builder.HasOne<Meeting>().WithMany().HasForeignKey(x => x.MeetingId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MeetingParticipant>().WithMany().HasForeignKey(x => x.UploaderParticipantId).OnDelete(DeleteBehavior.Restrict);
    }
}

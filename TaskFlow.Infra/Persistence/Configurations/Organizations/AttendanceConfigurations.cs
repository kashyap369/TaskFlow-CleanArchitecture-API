using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Domain.Entities.Organization;

namespace TaskFlow.Infra.Persistence.Configurations.Organizations;

public sealed class AttendanceSessionConfiguration : IEntityTypeConfiguration<AttendanceSession>
{
    // Named so UnitOfWork can turn its violation into ATTENDANCE_ALREADY_PUNCHED_IN.
    public const string OneOpenSessionIndex = "IX_AttendanceSessions_OrganizationId_UserId";

    public void Configure(EntityTypeBuilder<AttendanceSession> builder)
    {
        builder.ToTable("AttendanceSessions");
        builder.HasKey(x => x.Id);
        builder.Ignore(x => x.DomainEvents);
        builder.Ignore(x => x.IsOpen);
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.PunchInNote).HasMaxLength(500);
        builder.Property(x => x.PunchOutNote).HasMaxLength(500);
        builder.Property(x => x.PunchInPhotoKey).HasMaxLength(300);
        builder.Property(x => x.PunchInPhotoContentType).HasMaxLength(100);
        builder.Property(x => x.PunchInPhotoSha256).HasMaxLength(64);
        builder.Property(x => x.PunchOutPhotoKey).HasMaxLength(300);
        builder.Property(x => x.PunchOutPhotoContentType).HasMaxLength(100);
        builder.Property(x => x.PunchOutPhotoSha256).HasMaxLength(64);
        builder.Property(x => x.AdjustmentReason).HasMaxLength(500);
        // At most one open session per member per organization — the guard against double taps.
        builder.HasIndex(x => new { x.OrganizationId, x.UserId }).IsUnique()
            .HasFilter("\"PunchOutAtUtc\" IS NULL AND \"IsDeleted\" = FALSE");
        builder.HasIndex(x => new { x.OrganizationId, x.WorkDate });
        builder.HasIndex(x => new { x.OrganizationId, x.UserId, x.WorkDate });
    }
}

public sealed class AttendanceSettingsConfiguration : IEntityTypeConfiguration<AttendanceSettings>
{
    public void Configure(EntityTypeBuilder<AttendanceSettings> builder)
    {
        builder.ToTable("AttendanceSettings");
        builder.HasKey(x => x.Id);
        builder.Ignore(x => x.DomainEvents);
        builder.Property(x => x.TimeZone).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.OrganizationId).IsUnique();
    }
}

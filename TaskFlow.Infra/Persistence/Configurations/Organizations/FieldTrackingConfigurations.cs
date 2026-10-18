using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Domain.Entities.Organization;

namespace TaskFlow.Infra.Persistence.Configurations.Organizations;

public sealed class AttendanceTrackedRoleConfiguration : IEntityTypeConfiguration<AttendanceTrackedRole>
{
    public void Configure(EntityTypeBuilder<AttendanceTrackedRole> builder)
    {
        builder.ToTable("AttendanceTrackedRoles");
        builder.HasKey(x => x.Id);
        builder.Ignore(x => x.DomainEvents);
        builder.HasIndex(x => new { x.OrganizationId, x.OrganizationRoleId }).IsUnique()
            .HasFilter("\"IsDeleted\" = FALSE");
    }
}

public sealed class FieldTrackingConsentConfiguration : IEntityTypeConfiguration<FieldTrackingConsent>
{
    public void Configure(EntityTypeBuilder<FieldTrackingConsent> builder)
    {
        builder.ToTable("FieldTrackingConsents");
        builder.HasKey(x => x.Id);
        builder.Ignore(x => x.DomainEvents);
        builder.HasIndex(x => new { x.OrganizationId, x.UserId, x.PolicyVersion }).IsUnique()
            .HasFilter("\"IsDeleted\" = FALSE");
    }
}

public sealed class LocationPointConfiguration : IEntityTypeConfiguration<LocationPoint>
{
    // Named so UnitOfWork can turn a resent batch racing the first into a retryable conflict.
    public const string OnePointPerInstantIndex = "IX_LocationPoints_AttendanceSessionId_RecordedAtUtc";

    public void Configure(EntityTypeBuilder<LocationPoint> builder)
    {
        builder.ToTable("LocationPoints");
        builder.HasKey(x => x.Id);
        builder.Ignore(x => x.DomainEvents);
        builder.HasIndex(x => new { x.AttendanceSessionId, x.RecordedAtUtc }).IsUnique();
        builder.HasIndex(x => new { x.OrganizationId, x.UserId, x.RecordedAtUtc });
        // The processing service's work queue: small, because processed points leave it.
        builder.HasIndex(x => x.AttendanceSessionId).HasFilter("\"IsProcessed\" = FALSE");
    }
}

public sealed class TrackedRouteSegmentConfiguration : IEntityTypeConfiguration<TrackedRouteSegment>
{
    public void Configure(EntityTypeBuilder<TrackedRouteSegment> builder)
    {
        builder.ToTable("TrackedRouteSegments");
        builder.HasKey(x => x.Id);
        builder.Ignore(x => x.DomainEvents);
        builder.Property(x => x.Kind).HasConversion<int>();
        builder.Property(x => x.EncodedPolyline).IsRequired();
        builder.HasIndex(x => new { x.AttendanceSessionId, x.Sequence });
        builder.HasIndex(x => new { x.OrganizationId, x.UserId, x.StartedAtUtc });
    }
}

public sealed class TrackStopConfiguration : IEntityTypeConfiguration<TrackStop>
{
    public void Configure(EntityTypeBuilder<TrackStop> builder)
    {
        builder.ToTable("TrackStops");
        builder.HasKey(x => x.Id);
        builder.Ignore(x => x.DomainEvents);
        builder.HasIndex(x => new { x.AttendanceSessionId, x.ArrivedAtUtc });
    }
}

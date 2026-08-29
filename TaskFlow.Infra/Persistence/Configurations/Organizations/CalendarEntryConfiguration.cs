using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Domain.Entities.Organization;

namespace TaskFlow.Infra.Persistence.Configurations.Organizations;

public sealed class CalendarEntryConfiguration : IEntityTypeConfiguration<CalendarEntry>
{
    public void Configure(EntityTypeBuilder<CalendarEntry> builder)
    {
        builder.ToTable("CalendarEntries");
        builder.HasKey(x => x.Id);
        builder.Ignore(x => x.DomainEvents);
        builder.Property(x => x.Title).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.TimeZone).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Kind).HasConversion<int>();
        builder.Property(x => x.RecurrenceFrequency).HasConversion<int>();
        builder.HasIndex(x => new { x.OrganizationId, x.StartsAtUtc });
        builder.HasIndex(x => new { x.OrganizationId, x.MemberUserId });
    }
}

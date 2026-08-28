using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Domain.Entities.Planner;

namespace TaskFlow.Infra.Persistence.Configurations.Planner;

public sealed class RequirementSnapshotConfiguration : IEntityTypeConfiguration<RequirementSnapshot>
{
    public void Configure(EntityTypeBuilder<RequirementSnapshot> builder)
    {
        builder.ToTable("RequirementSnapshots");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EntityType).HasConversion<int>().IsRequired();
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.FieldsJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.CapturedAt).IsRequired();
        builder.HasQueryFilter(x => !x.Baseline.Board.Project.IsDeleted);
        builder.HasIndex(x => new { x.BaselineId, x.EntityType, x.EntityId }).IsUnique();
        builder.HasOne(x => x.Baseline).WithMany(x => x.Snapshots).HasForeignKey(x => x.BaselineId).OnDelete(DeleteBehavior.Cascade);
    }
}

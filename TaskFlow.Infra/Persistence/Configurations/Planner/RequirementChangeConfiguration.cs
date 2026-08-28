using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Domain.Entities.Planner;

namespace TaskFlow.Infra.Persistence.Configurations.Planner;

public sealed class RequirementChangeConfiguration : IEntityTypeConfiguration<RequirementChange>
{
    public void Configure(EntityTypeBuilder<RequirementChange> builder)
    {
        builder.ToTable("RequirementChanges");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EntityType).HasConversion<int>().IsRequired();
        builder.Property(x => x.ChangeType).HasConversion<int>().IsRequired();
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.OldValuesJson).HasColumnType("jsonb");
        builder.Property(x => x.NewValuesJson).HasColumnType("jsonb");
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.Property(x => x.ChangedAt).IsRequired();
        builder.HasQueryFilter(x => !x.Baseline.Board.Project.IsDeleted);
        builder.HasIndex(x => new { x.BaselineId, x.ChangedAt });
        builder.HasIndex(x => new { x.BaselineId, x.ChangeType });
        builder.HasOne(x => x.Baseline).WithMany().HasForeignKey(x => x.BaselineId).OnDelete(DeleteBehavior.Cascade);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Domain.Entities.Planner;

namespace TaskFlow.Infra.Persistence.Configurations.Planner;

public sealed class RequirementBaselineConfiguration : IEntityTypeConfiguration<RequirementBaseline>
{
    public void Configure(EntityTypeBuilder<RequirementBaseline> builder)
    {
        builder.ToTable("RequirementBaselines");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.BaselineNumber).IsRequired();
        builder.Property(x => x.FinalizedByUserId).IsRequired();
        builder.Property(x => x.FinalizedAt).IsRequired();
        builder.HasIndex(x => new { x.ProjectId, x.BaselineNumber }).IsUnique();
        builder.HasIndex(x => x.BoardId);
        builder.HasQueryFilter(x => !x.Board.Project.IsDeleted);
        builder.HasOne(x => x.Board).WithMany().HasForeignKey(x => x.BoardId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Snapshots).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

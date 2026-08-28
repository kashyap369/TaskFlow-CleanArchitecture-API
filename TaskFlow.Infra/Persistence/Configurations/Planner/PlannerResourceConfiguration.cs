using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Domain.Entities.Planner;

namespace TaskFlow.Infra.Persistence.Configurations.Planner;

public sealed class PlannerResourceConfiguration : IEntityTypeConfiguration<PlannerResource>
{
    public void Configure(EntityTypeBuilder<PlannerResource> builder)
    {
        builder.ToTable("PlannerResources", table =>
        {
            table.HasCheckConstraint("CK_PlannerResources_Content",
                "(\"Kind\" = 1 AND \"Content\" IS NOT NULL AND \"Url\" IS NULL) OR " +
                "(\"Kind\" = 2 AND \"Content\" IS NULL AND \"Url\" IS NOT NULL) OR " +
                "(\"Kind\" = 3 AND \"Content\" IS NULL AND \"Url\" IS NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasConversion<int>().IsRequired();
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Content).HasMaxLength(20000);
        builder.Property(x => x.Url).HasMaxLength(2048);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.HasIndex(x => new { x.BoardId, x.CreatedAt });
        builder.HasIndex(x => new { x.ProjectId, x.OwnerUserId });
        builder.HasOne<PlannerBoard>().WithMany().HasForeignKey(x => x.BoardId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Asset).WithOne().HasForeignKey<PlannerAsset>(x => x.ResourceId).OnDelete(DeleteBehavior.Cascade);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Domain.Entities.Planner;

namespace TaskFlow.Infra.Persistence.Configurations.Planner;

public sealed class PlannerSceneRevisionConfiguration
    : IEntityTypeConfiguration<PlannerSceneRevision>
{
    public void Configure(EntityTypeBuilder<PlannerSceneRevision> builder)
    {
        builder.ToTable("PlannerSceneRevisions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SceneJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.CreatedByUserId).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.HasIndex(x => new { x.BoardId, x.RevisionNumber }).IsUnique();
        builder.HasIndex(x => new { x.BoardId, x.CreatedAt });

        builder.HasOne<PlannerBoard>()
            .WithMany(x => x.SceneRevisions)
            .HasForeignKey(x => x.BoardId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

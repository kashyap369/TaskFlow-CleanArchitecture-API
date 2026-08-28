using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Domain.Entities.Planner;

namespace TaskFlow.Infra.Persistence.Configurations.Planner;

public sealed class PlannerBoardConfiguration : IEntityTypeConfiguration<PlannerBoard>
{
    public void Configure(EntityTypeBuilder<PlannerBoard> builder)
    {
        builder.ToTable("PlannerBoards");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SceneJson)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(x => x.CurrentRevision)
            .IsConcurrencyToken()
            .IsRequired();

        builder.Property(x => x.OwnerUserId).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();
        builder.Property(x => x.LastOpenedAt);

        builder.HasIndex(x => x.ProjectId).IsUnique();
        builder.HasIndex(x => x.OwnerUserId);

        // Project uses soft deletion. Apply the same visibility boundary to its required board so
        // a deleted personal project can never leave an independently queryable Planner record.
        builder.HasQueryFilter(x => !x.Project.IsDeleted);

        builder.HasOne(x => x.Project)
            .WithOne()
            .HasForeignKey<PlannerBoard>(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Nodes)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(x => x.SceneRevisions)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

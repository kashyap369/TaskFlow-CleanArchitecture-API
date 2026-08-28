using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Domain.Entities.Planner;

namespace TaskFlow.Infra.Persistence.Configurations.Planner;

public sealed class PlannerNodeConfiguration : IEntityTypeConfiguration<PlannerNode>
{
    public void Configure(EntityTypeBuilder<PlannerNode> builder)
    {
        builder.ToTable("PlannerNodes", table => table.HasCheckConstraint(
            "CK_PlannerNodes_ExactlyOneTarget",
            "(\"NodeType\" = 1 AND \"ProjectId\" IS NOT NULL AND \"TaskId\" IS NULL AND \"SubTaskId\" IS NULL AND \"ResourceId\" IS NULL) OR " +
            "(\"NodeType\" = 2 AND \"ProjectId\" IS NULL AND \"TaskId\" IS NOT NULL AND \"SubTaskId\" IS NULL AND \"ResourceId\" IS NULL) OR " +
            "(\"NodeType\" = 3 AND \"ProjectId\" IS NULL AND \"TaskId\" IS NULL AND \"SubTaskId\" IS NOT NULL AND \"ResourceId\" IS NULL) OR " +
            "(\"NodeType\" IN (4, 5) AND \"ProjectId\" IS NULL AND \"TaskId\" IS NULL AND \"SubTaskId\" IS NULL AND \"ResourceId\" IS NOT NULL)"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ElementId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.NodeType).HasConversion<int>().IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);
        builder.HasIndex(x => new { x.BoardId, x.ElementId }).IsUnique();
        builder.HasIndex(x => new { x.BoardId, x.ProjectId }).IsUnique().HasFilter("\"ProjectId\" IS NOT NULL");
        builder.HasIndex(x => new { x.BoardId, x.TaskId }).IsUnique().HasFilter("\"TaskId\" IS NOT NULL");
        builder.HasIndex(x => new { x.BoardId, x.SubTaskId }).IsUnique().HasFilter("\"SubTaskId\" IS NOT NULL");
        builder.HasIndex(x => new { x.BoardId, x.ResourceId }).IsUnique().HasFilter("\"ResourceId\" IS NOT NULL");

        builder.HasOne<PlannerBoard>()
            .WithMany(x => x.Nodes)
            .HasForeignKey(x => x.BoardId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Task).WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.SubTask).WithMany().HasForeignKey(x => x.SubTaskId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Resource).WithMany().HasForeignKey(x => x.ResourceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.TemplateVersion).WithMany().HasForeignKey(x => x.TemplateVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}

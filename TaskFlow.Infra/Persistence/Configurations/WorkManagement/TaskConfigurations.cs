using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Domain.Entities.WorkManagement.Tasks;
using Task = TaskFlow.Domain.Entities.WorkManagement.Tasks.Task;
namespace TaskFlow.Infra.Persistence.Configurations.WorkManagement
{
    public sealed class TaskConfigurations
        : IEntityTypeConfiguration<Task>
    {
        public void Configure(
            EntityTypeBuilder<Task> builder)
        {
            builder.ToTable("Tasks");

            builder.HasKey(x => x.Id);

            builder.Ignore(x => x.DomainEvents);

            builder.Property(x => x.Title)
                .HasMaxLength(200)
                .IsRequired();

            builder.Property(x => x.Description)
                .HasMaxLength(2000);

            builder.Property(x => x.Priority)
                .HasConversion<int>()
                .IsRequired();

            builder.Property(x => x.Status)
                .HasConversion<int>()
                .IsRequired();

            // Nullable: personal tasks have no organization.
            builder.Property(x => x.OrganizationId);

            builder.Property(x => x.CreatedByUserId)
                .IsRequired();

            builder.Property(x => x.AssignedToUserId);

            builder.Property(x => x.StartDate)
                .IsRequired();

            builder.Property(x => x.ExpectedCompletionDate);

            builder.Property(x => x.ActualCompletionDate);

            builder.Property(x => x.ProjectId);

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.Property(x => x.IsDeleted)
                .IsRequired();

            builder.HasIndex(x => x.OrganizationId);

            builder.HasIndex(x => x.ProjectId);

            builder.HasIndex(x => x.CreatedByUserId);

            builder.HasIndex(x => x.AssignedToUserId);

            builder.HasIndex(x => x.Priority);

            builder.HasIndex(x => x.Status);

            builder.HasIndex(x => new
            {
                x.OrganizationId,
                x.Title
            });

            builder.HasIndex(x => new
            {
                x.ProjectId,
                x.Status
            });

            builder.Metadata
                .FindNavigation(nameof(Task.SubTasks))
                ?.SetPropertyAccessMode(
                    PropertyAccessMode.Field);

            builder.HasQueryFilter(
                x => !x.IsDeleted);
        }
    }
}
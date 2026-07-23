using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Domain.Entities.WorkManagement.WorkLogs;

namespace TaskFlow.Infra.Persistence.Configurations.WorkManagement
{
    public sealed class TaskWorkLogConfigurations
        : IEntityTypeConfiguration<TaskWorkLog>
    {
        public void Configure(
            EntityTypeBuilder<TaskWorkLog> builder)
        {
            builder.ToTable("TaskWorkLogs");

            builder.HasKey(x => x.Id);

            builder.Ignore(x => x.DomainEvents);

            builder.Ignore(x => x.Duration);

            builder.Property(x => x.TaskId)
                .IsRequired();

            builder.Property(x => x.UserId)
                .IsRequired();

            builder.Property(x => x.StartedAt)
                .IsRequired();

            builder.Property(x => x.EndedAt);

            builder.Property(x => x.Notes)
                .HasMaxLength(1000);

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.Property(x => x.IsDeleted)
                .IsRequired();

            builder.HasIndex(x => x.TaskId);

            builder.HasIndex(x => x.UserId);

            builder.HasIndex(x => new
            {
                x.UserId,
                x.StartedAt
            });

            // Fast lookup of a user's running timer
            // (EndedAt is null while running).
            builder.HasIndex(x => new
            {
                x.UserId,
                x.EndedAt
            });

            builder.HasQueryFilter(
                x => !x.IsDeleted);
        }
    }
}

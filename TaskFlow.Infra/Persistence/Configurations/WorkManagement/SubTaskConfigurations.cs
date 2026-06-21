using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Domain.Entities.WorkManagement.SubTasks;

namespace TaskFlow.Infra.Persistence.Configurations.WorkManagement
{
    public sealed class SubTaskConfigurations
        : IEntityTypeConfiguration<SubTask>
    {
        public void Configure(
            EntityTypeBuilder<SubTask> builder)
        {
            builder.ToTable("SubTasks");

            builder.HasKey(x => x.Id);

            builder.Ignore(x => x.DomainEvents);

            builder.Property(x => x.Title)
                .HasMaxLength(200)
                .IsRequired();

            builder.Property(x => x.Status)
                .HasConversion<int>()
                .IsRequired();

            builder.Property(x => x.TaskId)
                .IsRequired();

            builder.Property(x => x.CreatedDate)
                .IsRequired();

            builder.Property(x => x.CompletedDate);

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.Property(x => x.IsDeleted)
                .IsRequired();

            builder.HasIndex(x => x.TaskId);

            builder.HasIndex(x => x.Status);

            builder.HasIndex(x => new
            {
                x.TaskId,
                x.Title
            });

            builder.HasIndex(x => new
            {
                x.TaskId,
                x.Status
            });

            builder.HasQueryFilter(
                x => !x.IsDeleted);
        }
    }
}
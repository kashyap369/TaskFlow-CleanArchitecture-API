using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Domain.Entities.WorkManagement.Projects;

namespace TaskFlow.Infra.Persistence.Configurations.WorkManagement
{
    public sealed class ProjectConfigurations
        : IEntityTypeConfiguration<Project>
    {
        public void Configure(
            EntityTypeBuilder<Project> builder)
        {
            builder.ToTable("Projects");

            builder.HasKey(x => x.Id);

            builder.Ignore(x => x.DomainEvents);

            builder.Property(x => x.Title)
                .HasMaxLength(200)
                .IsRequired();

            builder.Property(x => x.Description)
                .HasMaxLength(2000);

            builder.Property(x => x.Status)
                .HasConversion<int>()
                .IsRequired();

            builder.Property(x => x.OrganizationId)
                .IsRequired();

            builder.Property(x => x.CreatedByUserId)
                .IsRequired();

            builder.Property(x => x.StartDate)
                .IsRequired();

            builder.Property(x => x.ExpectedCompletionDate);

            builder.Property(x => x.ActualCompletionDate);

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.Property(x => x.IsDeleted)
                .IsRequired();

            builder.HasIndex(x => x.OrganizationId);

            builder.HasIndex(x => x.CreatedByUserId);

            builder.HasIndex(x => x.Status);

            builder.HasIndex(x => x.StartDate);

            builder.HasIndex(x => new
            {
                x.OrganizationId,
                x.Title
            })
            .IsUnique();

            builder.HasIndex(x => new
            {
                x.OrganizationId,
                x.Status
            });

            builder.Metadata
                .FindNavigation(nameof(Project.Tasks))
                ?.SetPropertyAccessMode(
                    PropertyAccessMode.Field);

            builder.HasQueryFilter(
                x => !x.IsDeleted);
        }
    }
}
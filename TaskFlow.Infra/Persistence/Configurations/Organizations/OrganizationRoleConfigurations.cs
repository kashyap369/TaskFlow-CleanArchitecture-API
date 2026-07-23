using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Domain.Entities.Organization;

namespace TaskFlow.Infra.Persistence.Configurations.Organizations
{
    public sealed class OrganizationRoleConfigurations
        : IEntityTypeConfiguration<OrganizationRole>
    {
        public void Configure(
            EntityTypeBuilder<OrganizationRole> builder)
        {
            builder.ToTable("OrganizationRoles");

            builder.HasKey(x => x.Id);

            builder.Ignore(x => x.DomainEvents);

            builder.Property(x => x.OrganizationId)
                .IsRequired();

            builder.Property(x => x.Name)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(x => x.Description)
                .HasMaxLength(500);

            builder.Metadata
                .FindNavigation(nameof(OrganizationRole.Permissions))
                ?.SetPropertyAccessMode(
                    PropertyAccessMode.Field);

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.Property(x => x.IsDeleted)
                .IsRequired();

            builder.Property(x => x.UpdatedAt);

            builder.Property(x => x.DeletedAt);

            builder.HasIndex(x => x.OrganizationId);

            builder.HasIndex(x => x.Name);

            builder.HasIndex(x => new
            {
                x.OrganizationId,
                x.Name
            })
            .IsUnique();

            builder.HasIndex(x => new
            {
                x.OrganizationId,
                x.IsDeleted
            });

            builder.HasQueryFilter(
                x => !x.IsDeleted);
        }
    }
}
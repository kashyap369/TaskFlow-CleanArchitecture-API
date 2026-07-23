using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Domain.Entities.Organization;

namespace TaskFlow.Infra.Persistence.Configurations.Organizations
{
    public sealed class OrganizationPermissionConfigurations
        : IEntityTypeConfiguration<OrganizationPermission>
    {
        public void Configure(
            EntityTypeBuilder<OrganizationPermission> builder)
        {
            builder.ToTable("OrganizationPermissions");

            builder.HasKey(x => x.Id);

            builder.Ignore(x => x.DomainEvents);

            builder.Property(x => x.Name)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(x => x.Description)
                .HasMaxLength(500);

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.Property(x => x.IsDeleted)
                .IsRequired();

            builder.HasIndex(x => x.Name)
                .IsUnique();

            builder.HasQueryFilter(
                x => !x.IsDeleted);
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Domain.Entities.Organization;

namespace TaskFlow.Infra.Persistence.Configurations.Organizations
{
    public sealed class OrganizationRolePermissionConfigurations
        : IEntityTypeConfiguration<OrganizationRolePermission>
    {
        public void Configure(
            EntityTypeBuilder<OrganizationRolePermission> builder)
        {
            builder.ToTable("OrganizationRolePermissions");

            builder.HasKey(x => x.Id);

            builder.Ignore(x => x.DomainEvents);

            builder.Property(x => x.OrganizationRoleId)
                .IsRequired();

            builder.Property(x => x.OrganizationPermissionId)
                .IsRequired();

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.Property(x => x.IsDeleted)
                .IsRequired();

            builder.HasIndex(x => x.OrganizationRoleId);

            builder.HasIndex(x => new
            {
                x.OrganizationRoleId,
                x.OrganizationPermissionId
            })
            .IsUnique();

            builder.HasOne<OrganizationRole>()
                .WithMany(x => x.Permissions)
                .HasForeignKey(x => x.OrganizationRoleId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne<OrganizationPermission>()
                .WithMany()
                .HasForeignKey(x => x.OrganizationPermissionId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasQueryFilter(
                x => !x.IsDeleted);
        }
    }
}

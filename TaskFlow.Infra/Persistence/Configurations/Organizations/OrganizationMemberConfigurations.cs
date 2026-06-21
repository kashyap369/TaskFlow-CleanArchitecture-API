using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Domain.Entities.Organization;

namespace TaskFlow.Infra.Persistence.Configurations.Organization
{
    public sealed class OrganizationMemberConfigurations
        : IEntityTypeConfiguration<OrganizationMember>
    {
        public void Configure(
            EntityTypeBuilder<OrganizationMember> builder)
        {
            builder.ToTable("OrganizationMembers");

            builder.HasKey(x => x.Id);

            builder.Ignore(x => x.DomainEvents);

            builder.Property(x => x.OrganizationId)
                .IsRequired();

            builder.Property(x => x.UserId)
                .IsRequired();

            builder.Property(x => x.OrganizationRoleId)
                .IsRequired();

            builder.Property(x => x.JoinedAt)
                .IsRequired();

            builder.Property(x => x.IsActive)
                .IsRequired();

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.Property(x => x.IsDeleted)
                .IsRequired();

            builder.HasIndex(x => x.OrganizationId);

            builder.HasIndex(x => x.UserId);

            builder.HasIndex(x => x.OrganizationRoleId);

            builder.HasIndex(x => x.IsActive);

            builder.HasIndex(x => new
            {
                x.OrganizationId,
                x.UserId
            })
            .IsUnique();

            builder.HasIndex(x => new
            {
                x.OrganizationId,
                x.IsActive
            });

            builder.HasQueryFilter(
                x => !x.IsDeleted);
        }
    }
}
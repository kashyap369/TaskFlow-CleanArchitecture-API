using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Domain.Entities.Organization;

namespace TaskFlow.Infra.Persistence.Configurations.Organization
{
    public sealed class OrganizationInvitationConfigurations
        : IEntityTypeConfiguration<OrganizationInvitation>
    {
        public void Configure(
            EntityTypeBuilder<OrganizationInvitation> builder)
        {
            builder.ToTable("OrganizationInvitations");

            builder.HasKey(x => x.Id);

            builder.Ignore(x => x.DomainEvents);

            builder.Property(x => x.Email)
                .HasMaxLength(256)
                .IsRequired();

            builder.Property(x => x.OrganizationId)
                .IsRequired();

            builder.Property(x => x.OrganizationRoleId)
                .IsRequired();

            builder.Property(x => x.InvitedByUserId)
                .IsRequired();

            builder.Property(x => x.ExpiryDate)
                .IsRequired();

            builder.Property(x => x.Status)
                .HasConversion<int>()
                .IsRequired();

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.Property(x => x.IsDeleted)
                .IsRequired();

            builder.HasIndex(x => x.OrganizationId);

            builder.HasIndex(x => x.Email);

            builder.HasIndex(x => x.Status);

            builder.HasIndex(x => x.ExpiryDate);

            builder.HasIndex(x => new
            {
                x.OrganizationId,
                x.Email
            });

            builder.HasIndex(x => new
            {
                x.OrganizationId,
                x.Status
            });

            builder.HasQueryFilter(
                x => !x.IsDeleted);
        }
    }
}
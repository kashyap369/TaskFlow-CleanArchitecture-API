using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Domain.Entities.Organization;

namespace TaskFlow.Infra.Persistence.Configurations.Organizations
{
    public sealed class TeamMemberConfigurations
        : IEntityTypeConfiguration<TeamMember>
    {
        public void Configure(
            EntityTypeBuilder<TeamMember> builder)
        {
            builder.ToTable("TeamMembers");

            builder.HasKey(x => x.Id);

            builder.Ignore(x => x.DomainEvents);

            builder.Property(x => x.TeamId)
                .IsRequired();

            builder.Property(x => x.UserId)
                .IsRequired();

            builder.Property(x => x.JoinedAt)
                .IsRequired();

            builder.Property(x => x.IsActive)
                .IsRequired();

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.Property(x => x.IsDeleted)
                .IsRequired();

            builder.HasIndex(x => x.TeamId);

            builder.HasIndex(x => x.UserId);

            builder.HasIndex(x => new
            {
                x.TeamId,
                x.UserId
            })
            .IsUnique();

            builder.HasQueryFilter(
                x => !x.IsDeleted);
        }
    }
}

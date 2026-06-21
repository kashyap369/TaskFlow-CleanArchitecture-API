using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Domain.Entities.Identity;

namespace TaskFlow.Infra.Persistence.Configurations.Identity
{
    public sealed class RefreshTokenConfigurations
        : IEntityTypeConfiguration<RefreshToken>
    {
        public void Configure(
            EntityTypeBuilder<RefreshToken> builder)
        {
            builder.ToTable("RefreshTokens");

            builder.HasKey(x => x.Id);

            builder.Ignore(x => x.DomainEvents);

            builder.Property(x => x.UserId)
                .IsRequired();

            builder.Property(x => x.Token)
                .HasMaxLength(500)
                .IsRequired();

            builder.Property(x => x.CreatedByIp)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(x => x.RevokedByIp)
                .HasMaxLength(100);

            builder.Property(x => x.ReplacedByToken)
                .HasMaxLength(500);

            builder.Property(x => x.ExpiresAt)
                .IsRequired();

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.Property(x => x.IsDeleted)
                .IsRequired();

            builder.HasIndex(x => x.UserId);

            builder.HasIndex(x => x.Token)
                .IsUnique();

            builder.HasIndex(x => x.ExpiresAt);

            builder.HasIndex(x => new
            {
                x.UserId,
                x.IsDeleted
            });

            builder.HasQueryFilter(
                x => !x.IsDeleted);
        }
    }
}
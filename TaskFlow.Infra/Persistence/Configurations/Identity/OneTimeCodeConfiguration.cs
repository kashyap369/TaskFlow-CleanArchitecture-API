using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Domain.Entities.Identity;

namespace TaskFlow.Infra.Persistence.Configurations.Identity
{
    public sealed class OneTimeCodeConfiguration
        : IEntityTypeConfiguration<OneTimeCode>
    {
        public void Configure(EntityTypeBuilder<OneTimeCode> builder)
        {
            builder.ToTable("OneTimeCodes");
            builder.HasKey(x => x.Id);
            builder.Ignore(x => x.DomainEvents);

            builder.Property(x => x.UserId).IsRequired();
            builder.Property(x => x.Purpose).HasConversion<int>().IsRequired();
            builder.Property(x => x.CodeHash).HasMaxLength(100).IsRequired();
            builder.Property(x => x.ExpiresAt).IsRequired();
            builder.Property(x => x.FailedAttempts).IsRequired();
            builder.Property(x => x.MaxAttempts).IsRequired();
            builder.Property(x => x.CreatedAt).IsRequired();
            builder.Property(x => x.IsDeleted).IsRequired();

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(x => new { x.UserId, x.Purpose, x.CreatedAt });
            builder.HasIndex(x => x.ExpiresAt);
            builder.HasQueryFilter(x => !x.IsDeleted);
        }
    }
}

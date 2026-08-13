using TaskFlow.Domain.Common;
using TaskFlow.Domain.Enums.Identity;

namespace TaskFlow.Domain.Entities.Identity
{
    public sealed class OneTimeCode : AuditableEntity
    {
        public int UserId { get; private set; }
        public OneTimeCodePurpose Purpose { get; private set; }
        public string CodeHash { get; private set; }
        public DateTime ExpiresAt { get; private set; }
        public int FailedAttempts { get; private set; }
        public int MaxAttempts { get; private set; }
        public DateTime? ConsumedAt { get; private set; }

        public bool IsConsumed => ConsumedAt.HasValue;

        private OneTimeCode()
        {
            CodeHash = string.Empty;
        }

        public OneTimeCode(
            int userId,
            OneTimeCodePurpose purpose,
            string codeHash,
            DateTime expiresAt,
            int maxAttempts)
        {
            if (userId <= 0)
                throw new ArgumentOutOfRangeException(nameof(userId));
            if (string.IsNullOrWhiteSpace(codeHash))
                throw new ArgumentException("Code hash is required.", nameof(codeHash));
            if (expiresAt <= DateTime.UtcNow)
                throw new ArgumentException("Expiry must be in the future.", nameof(expiresAt));
            if (maxAttempts <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxAttempts));

            UserId = userId;
            Purpose = purpose;
            CodeHash = codeHash;
            ExpiresAt = expiresAt;
            MaxAttempts = maxAttempts;
        }

        public bool CanAttempt(DateTime now) =>
            !IsConsumed && now < ExpiresAt && FailedAttempts < MaxAttempts;

        public void RegisterFailedAttempt(DateTime now)
        {
            if (!CanAttempt(now))
                return;

            FailedAttempts++;
            if (FailedAttempts >= MaxAttempts)
                ConsumedAt = now;

            MarkAsUpdated();
        }

        public void Consume(DateTime now)
        {
            if (IsConsumed)
                return;

            ConsumedAt = now;
            MarkAsUpdated();
        }
    }
}

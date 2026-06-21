using System;
using TaskFlow.Domain.Common;

namespace TaskFlow.Domain.Entities.Identity
{
    public class RefreshToken : AuditableEntity
    {
        public int UserId { get; private set; }

        public string Token { get; private set; }

        public DateTime ExpiresAt { get; private set; }

        public DateTime? RevokedAt { get; private set; }

        public string CreatedByIp { get; private set; }

        public string RevokedByIp { get; private set; }

        public string ReplacedByToken { get; private set; }

        public bool IsExpired =>
            DateTime.UtcNow >= ExpiresAt;

        public bool IsRevoked =>
            RevokedAt.HasValue;

        public bool IsActive =>
            !IsExpired && !IsRevoked;

        protected RefreshToken()
        {
        }

        public RefreshToken(
            int userId,
            string token,
            DateTime expiresAt,
            string createdByIp)
        {
            UserId = userId;
            Token = token;
            ExpiresAt = expiresAt;
            CreatedByIp = createdByIp;
        }

        public void Revoke(
            string revokedByIp,
            string replacedByToken = null)
        {
            if (IsRevoked)
                return;

            RevokedAt = DateTime.UtcNow;
            RevokedByIp = revokedByIp;
            ReplacedByToken = replacedByToken;

            MarkAsUpdated();
        }
    }
}
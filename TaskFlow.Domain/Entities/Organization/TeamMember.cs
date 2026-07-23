using System;
using TaskFlow.Domain.Common;

namespace TaskFlow.Domain.Entities.Organization
{
    /// <summary>
    /// Membership of one user in one team. Owned by Team —
    /// always modify it through Team.AddMember / RemoveMember.
    /// Deactivated (not deleted) on removal so historical
    /// reports keep working.
    /// </summary>
    public class TeamMember : AuditableEntity
    {
        public int TeamId { get; private set; }

        public int UserId { get; private set; }

        public DateTime JoinedAt { get; private set; }

        public bool IsActive { get; private set; }

        protected TeamMember()
        {
        }

        public TeamMember(
            int teamId,
            int userId)
        {
            if (userId <= 0)
                throw new ArgumentException(
                    "UserId is required.",
                    nameof(userId));

            TeamId = teamId;
            UserId = userId;

            JoinedAt = DateTime.UtcNow;
            IsActive = true;
        }

        public void Activate()
        {
            if (IsActive)
                return;

            IsActive = true;
            JoinedAt = DateTime.UtcNow;

            MarkAsUpdated();
        }

        public void Deactivate()
        {
            if (!IsActive)
                return;

            IsActive = false;

            MarkAsUpdated();
        }
    }
}

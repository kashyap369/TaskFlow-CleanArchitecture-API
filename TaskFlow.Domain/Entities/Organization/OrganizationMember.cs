using System;
using TaskFlow.Domain.Common;

namespace TaskFlow.Domain.Entities.Organization
{
    public class OrganizationMember : AuditableEntity
    {
        public int OrganizationId { get; private set; }

        public int UserId { get; private set; }

        public int OrganizationRoleId { get; private set; }

        public DateTime JoinedAt { get; private set; }

        public bool IsActive { get; private set; }

        /// <summary>
        /// Normal working capacity for one Monday-Sunday week. Null means the
        /// organization has not recorded a defensible capacity for this member.
        /// </summary>
        public int? WeeklyCapacityMinutes { get; private set; }

        protected OrganizationMember()
        {
        }

        public OrganizationMember(
            int organizationId,
            int userId,
            int organizationRoleId)
        {
            OrganizationId = organizationId;
            UserId = userId;
            OrganizationRoleId = organizationRoleId;

            JoinedAt = DateTime.UtcNow;
            IsActive = true;
        }

        public void ChangeRole(int roleId)
        {
            OrganizationRoleId = roleId;

            MarkAsUpdated();
        }

        public void Deactivate()
        {
            IsActive = false;

            MarkAsUpdated();
        }

        public void Activate()
        {
            IsActive = true;

            MarkAsUpdated();
        }

        public void SetWeeklyCapacity(int? weeklyCapacityMinutes)
        {
            if (weeklyCapacityMinutes < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(weeklyCapacityMinutes),
                    "Weekly capacity cannot be negative.");

            WeeklyCapacityMinutes = weeklyCapacityMinutes;

            MarkAsUpdated();
        }
    }
}

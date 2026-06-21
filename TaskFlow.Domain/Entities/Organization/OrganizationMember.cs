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
    }
}
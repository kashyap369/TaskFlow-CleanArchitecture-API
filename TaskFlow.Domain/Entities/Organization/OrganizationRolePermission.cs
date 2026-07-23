using System;
using TaskFlow.Domain.Common;

namespace TaskFlow.Domain.Entities.Organization
{
    /// <summary>
    /// Grants one permission (from the OrganizationPermission
    /// catalog) to one organization role. Owned by
    /// OrganizationRole — always modify it through
    /// OrganizationRole.GrantPermission / RevokePermission.
    /// </summary>
    public class OrganizationRolePermission : AuditableEntity
    {
        public int OrganizationRoleId { get; private set; }

        public int OrganizationPermissionId { get; private set; }

        protected OrganizationRolePermission()
        {
        }

        public OrganizationRolePermission(
            int organizationRoleId,
            int organizationPermissionId)
        {
            if (organizationRoleId <= 0)
                throw new ArgumentException(
                    "OrganizationRoleId is required.",
                    nameof(organizationRoleId));

            if (organizationPermissionId <= 0)
                throw new ArgumentException(
                    "OrganizationPermissionId is required.",
                    nameof(organizationPermissionId));

            OrganizationRoleId = organizationRoleId;
            OrganizationPermissionId = organizationPermissionId;
        }
    }
}

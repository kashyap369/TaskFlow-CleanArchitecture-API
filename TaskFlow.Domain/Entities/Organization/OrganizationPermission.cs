using System;
using TaskFlow.Domain.Common;

namespace TaskFlow.Domain.Entities.Organization
{
    /// <summary>
    /// A global catalog entry (one row per permission name from
    /// OrganizationPermissionNames). Organization roles are granted
    /// permissions via OrganizationRolePermission.
    /// </summary>
    public class OrganizationPermission : AuditableEntity, IAggregateRoot
    {
        public string Name { get; private set; }

        public string Description { get; private set; }

        protected OrganizationPermission()
        {
        }

        public OrganizationPermission(
            string name,
            string description)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException(
                    "Permission name is required.");

            Name = name.Trim();
            Description = description?.Trim();
        }

        public void UpdateDescription(
            string description)
        {
            Description = description?.Trim();

            MarkAsUpdated();
        }
    }
}

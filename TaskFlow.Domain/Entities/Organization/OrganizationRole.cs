using System;
using System.Collections.Generic;
using System.Linq;
using TaskFlow.Domain.Common;

namespace TaskFlow.Domain.Entities.Organization
{
    public class OrganizationRole : AuditableEntity
    {
        private readonly List<OrganizationRolePermission> _permissions = new();

        public int OrganizationId { get; private set; }

        public string Name { get; private set; }

        public string Description { get; private set; }

        public IReadOnlyCollection<OrganizationRolePermission> Permissions =>
            _permissions.AsReadOnly();

        protected OrganizationRole()
        {
        }

        public OrganizationRole(
            int organizationId,
            string name,
            string description)
        {
            if (organizationId <= 0)
                throw new ArgumentException(nameof(organizationId));

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Role name is required.");

            OrganizationId = organizationId;
            Name = name.Trim();
            Description = description?.Trim();
        }

        public void Update(
            string name,
            string description)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Role name is required.");

            Name = name.Trim();
            Description = description?.Trim();

            MarkAsUpdated();
        }

        public bool HasPermission(int organizationPermissionId)
        {
            return _permissions.Any(
                x => x.OrganizationPermissionId
                    == organizationPermissionId);
        }

        public void GrantPermission(int organizationPermissionId)
        {
            if (organizationPermissionId <= 0)
                throw new ArgumentException(
                    "OrganizationPermissionId is required.",
                    nameof(organizationPermissionId));

            if (HasPermission(organizationPermissionId))
                return;

            _permissions.Add(
                new OrganizationRolePermission(
                    Id,
                    organizationPermissionId));

            MarkAsUpdated();
        }

        public void RevokePermission(int organizationPermissionId)
        {
            var permission =
                _permissions.FirstOrDefault(
                    x => x.OrganizationPermissionId
                        == organizationPermissionId);

            if (permission is null)
                return;

            _permissions.Remove(permission);

            MarkAsUpdated();
        }
    }
}

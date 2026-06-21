using System;
using TaskFlow.Domain.Common;

namespace TaskFlow.Domain.Entities.Organization
{
    public class OrganizationRole : AuditableEntity
    {
        public int OrganizationId { get; private set; }

        public string Name { get; private set; }

        public string Description { get; private set; }

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
    }
}
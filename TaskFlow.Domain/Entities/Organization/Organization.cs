using System;
using System.Collections.Generic;
using TaskFlow.Domain.Common;
using TaskFlow.Domain.Enums.Organizations;

namespace TaskFlow.Domain.Entities.Organization
{
    public class Organization : AuditableEntity, IAggregateRoot
    {
        private readonly List<OrganizationMember> _members = new();
        private readonly List<OrganizationRole> _roles = new();

        public string Name { get; private set; }

        public string Description { get; private set; }

        public int OwnerUserId { get; private set; }

        public OrganizationStatus Status { get; private set; }

        public IReadOnlyCollection<OrganizationMember> Members =>
            _members.AsReadOnly();

        public IReadOnlyCollection<OrganizationRole> Roles =>
            _roles.AsReadOnly();

        protected Organization()
        {
        }

        public Organization(
            string name,
            string description,
            int ownerUserId)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Organization name is required.");

            if (ownerUserId <= 0)
                throw new ArgumentException(nameof(ownerUserId));

            Name = name.Trim();
            Description = description?.Trim();
            OwnerUserId = ownerUserId;

            Status = OrganizationStatus.Active;
        }

        public void UpdateDetails(
            string name,
            string description)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Organization name is required.");

            Name = name.Trim();
            Description = description?.Trim();

            MarkAsUpdated();
        }

        public void Suspend()
        {
            Status = OrganizationStatus.Suspended;

            MarkAsUpdated();
        }

        public void Activate()
        {
            Status = OrganizationStatus.Active;

            MarkAsUpdated();
        }

        public void AddRole(OrganizationRole role)
        {
            ArgumentNullException.ThrowIfNull(role);

            _roles.Add(role);

            MarkAsUpdated();
        }

        public void AddMember(OrganizationMember member)
        {
            ArgumentNullException.ThrowIfNull(member);

            _members.Add(member);

            MarkAsUpdated();
        }
    }
}
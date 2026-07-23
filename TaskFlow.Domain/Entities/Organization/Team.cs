using System;
using System.Collections.Generic;
using System.Linq;
using TaskFlow.Domain.Common;

namespace TaskFlow.Domain.Entities.Organization
{
    /// <summary>
    /// A group of organization members (e.g. "Developer Team",
    /// "Designer Team"). Used for team-wise task views and the
    /// team performance reports.
    /// </summary>
    public class Team : AuditableEntity, IAggregateRoot
    {
        private readonly List<TeamMember> _members = new();

        public int OrganizationId { get; private set; }

        public string Name { get; private set; }

        public string Description { get; private set; }

        public int CreatedByUserId { get; private set; }

        public IReadOnlyCollection<TeamMember> Members =>
            _members.AsReadOnly();

        protected Team()
        {
        }

        public Team(
            int organizationId,
            string name,
            string description,
            int createdByUserId)
        {
            if (organizationId <= 0)
                throw new ArgumentException(
                    "OrganizationId is required.",
                    nameof(organizationId));

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException(
                    "Team name is required.");

            if (createdByUserId <= 0)
                throw new ArgumentException(
                    "CreatedByUserId is required.",
                    nameof(createdByUserId));

            OrganizationId = organizationId;
            Name = name.Trim();
            Description = description?.Trim();
            CreatedByUserId = createdByUserId;
        }

        public void UpdateDetails(
            string name,
            string description)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException(
                    "Team name is required.");

            Name = name.Trim();
            Description = description?.Trim();

            MarkAsUpdated();
        }

        /// <summary>
        /// Adds a user to the team. Re-activates the membership
        /// if the user was previously removed; does nothing if
        /// the user is already an active member.
        /// </summary>
        public void AddMember(int userId)
        {
            if (userId <= 0)
                throw new ArgumentException(
                    "UserId is required.",
                    nameof(userId));

            var existing =
                _members.FirstOrDefault(x => x.UserId == userId);

            if (existing is not null)
            {
                existing.Activate();

                MarkAsUpdated();

                return;
            }

            _members.Add(
                new TeamMember(
                    Id,
                    userId));

            MarkAsUpdated();
        }

        /// <summary>
        /// Removes a user from the team (deactivates the
        /// membership so history stays intact for reports).
        /// </summary>
        public void RemoveMember(int userId)
        {
            var member =
                _members.FirstOrDefault(
                    x => x.UserId == userId && x.IsActive);

            if (member is null)
                return;

            member.Deactivate();

            MarkAsUpdated();
        }

        public bool HasActiveMember(int userId)
        {
            return _members.Any(
                x => x.UserId == userId && x.IsActive);
        }
    }
}

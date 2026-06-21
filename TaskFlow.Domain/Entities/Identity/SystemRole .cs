using System;
using TaskFlow.Domain.Common;

namespace TaskFlow.Domain.Entities.Identity
{
    public class SystemRole : AuditableEntity, IAggregateRoot
    {
        public string Name { get; private set; }

        public string Description { get; private set; }

        protected SystemRole()
        {
        }

        public SystemRole(
            string name,
            string description)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException(
                    "Role name is required.");

            Name = name.Trim();
            Description = description?.Trim();
        }

        public void Update(
            string name,
            string description)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException(
                    "Role name is required.");

            Name = name.Trim();
            Description = description?.Trim();

            MarkAsUpdated();
        }
    }
}
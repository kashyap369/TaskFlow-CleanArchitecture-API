using System;

namespace TaskFlow.Domain.Common
{
    public abstract class AuditableEntity : BaseEntity
    {
        public DateTime CreatedAt { get; protected set; }

        public DateTime? UpdatedAt { get; protected set; }

        public bool IsDeleted { get; protected set; }

        public DateTime? DeletedAt { get; protected set; }

        protected AuditableEntity()
        {
            CreatedAt = DateTime.UtcNow;
        }

        public virtual void MarkAsUpdated()
        {
            UpdatedAt = DateTime.UtcNow;
        }

        public virtual void SoftDelete()
        {
            if (IsDeleted)
                return;

            IsDeleted = true;
            DeletedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public virtual void Restore()
        {
            if (!IsDeleted)
                return;

            IsDeleted = false;
            DeletedAt = null;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
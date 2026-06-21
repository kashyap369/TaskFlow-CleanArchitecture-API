using TaskFlow.Domain.Common;

namespace TaskFlow.Domain.Entities.Identity
{
    public class UserRole : AuditableEntity
    {
        public int UserId { get; private set; }

        public int SystemRoleId { get; private set; }

        protected UserRole()
        {
        }

        public UserRole(
            int userId,
            int systemRoleId)
        {
            UserId = userId;
            SystemRoleId = systemRoleId;
        }
    }
}
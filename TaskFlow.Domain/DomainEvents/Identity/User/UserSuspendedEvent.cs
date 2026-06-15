using TaskFlow.Domain.DomainEvents;

namespace TaskFlow.Domain.DomainEvents.Identity.User
{
    public sealed class UserSuspendedEvent : DomainEvent
    {
        public string Email { get; }

        public UserSuspendedEvent(string email)
        {
            Email = email;
        }
    }
}
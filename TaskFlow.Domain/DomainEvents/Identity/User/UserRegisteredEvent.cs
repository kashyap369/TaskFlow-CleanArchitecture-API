using TaskFlow.Domain.DomainEvents;

namespace TaskFlow.Domain.DomainEvents.Identity.User
{
    public sealed class UserRegisteredEvent : DomainEvent
    {
        public string FullName { get; }

        public string Email { get; }

        public UserRegisteredEvent(
            string fullName,
            string email)
        {
            FullName = fullName;
            Email = email;
        }
    }
}
using TaskFlow.Domain.DomainEvents;

namespace TaskFlow.Domain.DomainEvents.Identity.User
{
    public sealed class UserEmailVerifiedEvent : DomainEvent
    {
        public string Email { get; }

        public UserEmailVerifiedEvent(string email)
        {
            Email = email;
        }
    }
}
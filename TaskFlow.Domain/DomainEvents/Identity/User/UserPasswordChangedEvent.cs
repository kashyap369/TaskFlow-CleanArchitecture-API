using TaskFlow.Domain.DomainEvents;

namespace TaskFlow.Domain.DomainEvents.Identity.User
{
    public sealed class UserPasswordChangedEvent : DomainEvent
    {
        public string Email { get; }

        public UserPasswordChangedEvent(string email)
        {
            Email = email;
        }
    }
}
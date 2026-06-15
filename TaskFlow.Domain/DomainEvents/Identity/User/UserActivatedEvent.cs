using TaskFlow.Domain.DomainEvents;

namespace TaskFlow.Domain.DomainEvents.Identity.User
{
    public sealed class UserActivatedEvent : DomainEvent
    {
        public string Email { get; }

        public UserActivatedEvent(string email)
        {
            Email = email;
        }
    }
}
using TaskFlow.Domain.DomainEvents;

namespace TaskFlow.Domain.DomainEvents.Identity.User
{
    public sealed class UserDeactivatedEvent : DomainEvent
    {
        public string Email { get; }

        public UserDeactivatedEvent(string email)
        {
            Email = email;
        }
    }
}
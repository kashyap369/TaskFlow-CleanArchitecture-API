using TaskFlow.Domain.DomainEvents;

namespace TaskFlow.Domain.DomainEvents.Identity.User
{
    public sealed class UserPhoneNumberChangedEvent : DomainEvent
    {
        public string Email { get; }

        public string PhoneNumber { get; }

        public UserPhoneNumberChangedEvent(
            string email,
            string phoneNumber)
        {
            Email = email;
            PhoneNumber = phoneNumber;
        }
    }
}
namespace TaskFlow.Domain.DomainEvents.Organizations
{
    /// <summary>
    /// Raised when an organization invitation is created.
    /// The Application layer handles it to send the
    /// invitation email.
    /// </summary>
    public sealed class OrganizationMemberInvitedEvent : DomainEvent
    {
        public int OrganizationId { get; }

        public string Email { get; }

        public int OrganizationRoleId { get; }

        public int InvitedByUserId { get; }

        public OrganizationMemberInvitedEvent(
            int organizationId,
            string email,
            int organizationRoleId,
            int invitedByUserId)
        {
            OrganizationId = organizationId;
            Email = email;
            OrganizationRoleId = organizationRoleId;
            InvitedByUserId = invitedByUserId;
        }
    }
}

using System;
using TaskFlow.Domain.Common;
using TaskFlow.Domain.Enums.Organizations;

namespace TaskFlow.Domain.Entities.Organization
{
    public class OrganizationInvitation : AuditableEntity
    {
        public int OrganizationId { get; private set; }

        public string Email { get; private set; }

        public int OrganizationRoleId { get; private set; }

        public int InvitedByUserId { get; private set; }

        public DateTime ExpiryDate { get; private set; }

        public InvitationStatus Status { get; private set; }

        protected OrganizationInvitation()
        {
        }

        public OrganizationInvitation(
            int organizationId,
            string email,
            int organizationRoleId,
            int invitedByUserId,
            DateTime expiryDate)
        {
            OrganizationId = organizationId;
            Email = email?.Trim().ToLowerInvariant();
            OrganizationRoleId = organizationRoleId;
            InvitedByUserId = invitedByUserId;
            ExpiryDate = expiryDate;

            Status = InvitationStatus.Pending;
        }

        public void Accept()
        {
            if (Status != InvitationStatus.Pending)
                return;

            Status = InvitationStatus.Accepted;

            MarkAsUpdated();
        }

        public void Reject()
        {
            if (Status != InvitationStatus.Pending)
                return;

            Status = InvitationStatus.Rejected;

            MarkAsUpdated();
        }

        public void Cancel()
        {
            if (Status != InvitationStatus.Pending)
                return;

            Status = InvitationStatus.Cancelled;

            MarkAsUpdated();
        }

        public void MarkExpired()
        {
            if (Status != InvitationStatus.Pending)
                return;

            Status = InvitationStatus.Expired;

            MarkAsUpdated();
        }
    }
}
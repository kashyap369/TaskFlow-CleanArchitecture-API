using TaskFlow.Application.Contracts.Email;
using TaskFlow.Domain.DomainEvents.Organizations;
using TaskFlow.Domain.Interfaces.Organizations;

namespace TaskFlow.Application.DomainEvents.Organizations
{
    /// <summary>
    /// Sends the invitation email when an organization member is
    /// invited. Raised by
    /// <see cref="Domain.Entities.Organization.OrganizationInvitation"/>
    /// on creation and dispatched after the invitation is saved.
    /// </summary>
    public sealed class OrganizationMemberInvitedEventHandler
        : IDomainEventHandler<OrganizationMemberInvitedEvent>
    {
        private readonly IEmailService _emailService;
        private readonly IOrganizationRepository _organizationRepository;

        public OrganizationMemberInvitedEventHandler(
            IEmailService emailService,
            IOrganizationRepository organizationRepository)
        {
            _emailService = emailService;
            _organizationRepository = organizationRepository;
        }

        public async Task HandleAsync(
            OrganizationMemberInvitedEvent domainEvent,
            CancellationToken cancellationToken)
        {
            var organization =
                await _organizationRepository.GetByIdAsync(
                    domainEvent.OrganizationId,
                    cancellationToken);

            var organizationName =
                organization?.Name ?? "an organization";

            var templatePath = Path.Combine(
                AppContext.BaseDirectory,
                "Email",
                "Templates",
                "Invitation.html");

            var template =
                await File.ReadAllTextAsync(
                    templatePath,
                    cancellationToken);

            template = template
                .Replace(
                    "{{OrganizationName}}",
                    organizationName)
                .Replace(
                    "{{Email}}",
                    domainEvent.Email)
                .Replace(
                    "{{AcceptUrl}}",
                    "https://localhost:4200/invitations")
                .Replace(
                    "{{CurrentYear}}",
                    DateTime.UtcNow.Year.ToString());

            await _emailService.SendAsync(
                domainEvent.Email,
                $"You've been invited to join {organizationName} on TaskFlow",
                template,
                cancellationToken);
        }
    }
}

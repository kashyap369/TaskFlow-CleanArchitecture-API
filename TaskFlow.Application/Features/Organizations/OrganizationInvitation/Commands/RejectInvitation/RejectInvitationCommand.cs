using MediatR;

namespace TaskFlow.Application.Features.Organizations.OrganizationInvitation.Commands.RejectInvitation
{
    public sealed record RejectInvitationCommand(
        int InvitationId
    ) : IRequest;
}
using MediatR;

namespace TaskFlow.Application.Features.Organizations.OrganizationInvitation.Commands.CancelInvitation
{
    public sealed record CancelInvitationCommand(
        int InvitationId
    ) : IRequest;
}
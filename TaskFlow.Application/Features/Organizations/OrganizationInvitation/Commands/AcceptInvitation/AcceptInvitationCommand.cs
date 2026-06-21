using MediatR;

namespace TaskFlow.Application.Features.Organizations.OrganizationInvitation.Commands.AcceptInvitation
{
    public sealed record AcceptInvitationCommand(
        int InvitationId
    ) : IRequest;
}
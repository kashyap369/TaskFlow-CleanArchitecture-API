using MediatR;

namespace TaskFlow.Application.Features.Organizations.OrganizationInvitation.Commands.InviteUser
{
    public sealed record InviteUserCommand(
        int OrganizationId,
        string Email,
        int OrganizationRoleId
    ) : IRequest<int>;
}
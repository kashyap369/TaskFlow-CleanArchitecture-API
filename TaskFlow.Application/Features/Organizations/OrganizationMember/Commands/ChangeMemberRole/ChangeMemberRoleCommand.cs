using MediatR;

namespace TaskFlow.Application.Features.Organizations.OrganizationMember.Commands.ChangeMemberRole
{
    public sealed record ChangeMemberRoleCommand(
        int OrganizationId,
        int UserId,
        int OrganizationRoleId
    ) : IRequest;
}
using MediatR;

namespace TaskFlow.Application.Features.Organizations.OrganizationRole.Commands.DeleteRole
{
    public sealed record DeleteRoleCommand(
        int RoleId
    ) : IRequest;
}
using MediatR;

namespace TaskFlow.Application.Features.Organizations.OrganizationRole.Commands.UpdateRole
{
    public sealed record UpdateRoleCommand(
        int RoleId,
        string Name,
        string Description
    ) : IRequest;
}
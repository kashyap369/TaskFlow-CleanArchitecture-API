using MediatR;

namespace TaskFlow.Application.Features.Organizations.OrganizationRole.Commands.GrantPermission
{
    public sealed record GrantPermissionCommand(
        int OrganizationRoleId,
        string PermissionName
    ) : IRequest;
}

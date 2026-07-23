using MediatR;

namespace TaskFlow.Application.Features.Organizations.OrganizationRole.Commands.RevokePermission
{
    public sealed record RevokePermissionCommand(
        int OrganizationRoleId,
        string PermissionName
    ) : IRequest;
}

using MediatR;

namespace TaskFlow.Application.Features.Organizations.OrganizationRole.Commands.CreateRole
{
    public sealed record CreateRoleCommand(
        int OrganizationId,
        string Name,
        string Description
    ) : IRequest<int>;
}
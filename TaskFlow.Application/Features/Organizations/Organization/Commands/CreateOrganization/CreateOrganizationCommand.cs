using MediatR;

namespace TaskFlow.Application.Features.Organizations.Organization.Commands.CreateOrganization
{
    public sealed record CreateOrganizationCommand(
        string Name,
        string Description
    ) : IRequest<int>;
}
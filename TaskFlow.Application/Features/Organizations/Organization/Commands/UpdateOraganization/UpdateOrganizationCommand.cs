using MediatR;

namespace TaskFlow.Application.Features.Organizations.Organization.Commands.UpdateOraganization
{
    public sealed record UpdateOrganizationCommand(
        int OrganizationId,
        string Name,
        string Description
    ) : IRequest;
}
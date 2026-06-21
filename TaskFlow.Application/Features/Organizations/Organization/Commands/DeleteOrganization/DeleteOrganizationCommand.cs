using MediatR;

namespace TaskFlow.Application.Features.Organizations.Organization.Commands.DeleteOrganization
{
    public sealed record DeleteOrganizationCommand(
        int OrganizationId
    ) : IRequest;
}
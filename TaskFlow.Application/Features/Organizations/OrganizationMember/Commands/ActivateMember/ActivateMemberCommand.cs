using MediatR;

namespace TaskFlow.Application.Features.Organizations.OrganizationMember.Commands.ActivateMember
{
    public sealed record ActivateMemberCommand(
        int OrganizationId,
        int UserId
    ) : IRequest;
}
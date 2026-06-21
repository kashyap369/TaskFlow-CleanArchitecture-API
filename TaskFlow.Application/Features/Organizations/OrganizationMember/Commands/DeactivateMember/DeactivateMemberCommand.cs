using MediatR;

namespace TaskFlow.Application.Features.Organizations.OrganizationMember.Commands.DeactivateMember
{
    public sealed record DeactivateMemberCommand(
        int OrganizationId,
        int UserId
    ) : IRequest;
}
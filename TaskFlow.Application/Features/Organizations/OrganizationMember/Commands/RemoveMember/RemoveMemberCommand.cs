using MediatR;

namespace TaskFlow.Application.Features.Organizations.OrganizationMember.Commands.RemoveMember
{
    public sealed record RemoveMemberCommand(
        int OrganizationId,
        int UserId
    ) : IRequest;
}   
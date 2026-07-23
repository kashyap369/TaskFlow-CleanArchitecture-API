using MediatR;

namespace TaskFlow.Application.Features.Organizations.Team.Commands.RemoveTeamMember
{
    public sealed record RemoveTeamMemberCommand(
        int TeamId,
        int UserId
    ) : IRequest;
}

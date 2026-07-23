using MediatR;

namespace TaskFlow.Application.Features.Organizations.Team.Commands.AddTeamMember
{
    public sealed record AddTeamMemberCommand(
        int TeamId,
        int UserId
    ) : IRequest;
}

using MediatR;

namespace TaskFlow.Application.Features.Organizations.Team.Commands.DeleteTeam
{
    public sealed record DeleteTeamCommand(
        int TeamId
    ) : IRequest;
}

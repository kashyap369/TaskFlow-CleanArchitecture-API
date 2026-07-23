using MediatR;

namespace TaskFlow.Application.Features.Organizations.Team.Commands.UpdateTeam
{
    public sealed record UpdateTeamCommand(
        int TeamId,
        string Name,
        string Description
    ) : IRequest;
}

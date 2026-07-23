using MediatR;

namespace TaskFlow.Application.Features.Organizations.Team.Commands.CreateTeam
{
    public sealed record CreateTeamCommand(
        int OrganizationId,
        string Name,
        string Description
    ) : IRequest<int>;
}

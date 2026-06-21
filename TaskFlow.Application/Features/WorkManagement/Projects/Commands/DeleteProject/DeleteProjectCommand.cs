using MediatR;

namespace TaskFlow.Application.Features.WorkManagement.Projects.Commands.DeleteProject
{
    public sealed record DeleteProjectCommand(
        int ProjectId
    ) : IRequest;
}
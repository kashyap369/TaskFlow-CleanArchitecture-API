using MediatR;

namespace TaskFlow.Application.Features.WorkManagement.Projects.Commands.UpdateProject
{
    public sealed record UpdateProjectCommand(
        int ProjectId,
        string Title,
        string Description,
        DateTime? ExpectedCompletionDate
    ) : IRequest;
}
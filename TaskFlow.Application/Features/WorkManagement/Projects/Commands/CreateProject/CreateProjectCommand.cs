using MediatR;

namespace TaskFlow.Application.Features.WorkManagement.Projects.Commands.CreateProject
{
    public sealed record CreateProjectCommand(
        string Title,
        string Description,
        DateTime StartDate,
        DateTime? ExpectedCompletionDate,
        int OrganizationId,
        int CreatedByUserId
    ) : IRequest<int>;
}
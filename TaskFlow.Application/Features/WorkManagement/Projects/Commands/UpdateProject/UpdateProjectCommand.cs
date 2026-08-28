using MediatR;

namespace TaskFlow.Application.Features.WorkManagement.Projects.Commands.UpdateProject
{
    public sealed record UpdateProjectCommand(
        int ProjectId,
        string Title,
        string Description,
        DateTime? ExpectedCompletionDate,
        string? ProblemStatement = null,
        decimal? BudgetAmount = null,
        string? BudgetCurrency = null,
        int? ApproximateDurationWeeks = null
    ) : IRequest;
}

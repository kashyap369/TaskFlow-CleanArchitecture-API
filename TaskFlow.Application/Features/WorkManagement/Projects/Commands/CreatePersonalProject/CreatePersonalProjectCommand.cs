using MediatR;

namespace TaskFlow.Application.Features.WorkManagement.Projects.Commands.CreatePersonalProject;

/// <summary>
/// Creates a project owned exclusively by the signed-in user. The command has no
/// organization or creator field by design; ownership always comes from the JWT.
/// </summary>
public sealed record CreatePersonalProjectCommand(
    string Title,
    string Description,
    DateTime StartDate,
    DateTime? ExpectedCompletionDate,
    string? ProblemStatement = null,
    decimal? BudgetAmount = null,
    string? BudgetCurrency = null,
    int? ApproximateDurationWeeks = null
) : IRequest<int>;

using FluentValidation;
using MediatR;
using TaskFlow.Domain.Entities.Planner;

namespace TaskFlow.Application.Features.Planner.Commands.SavePlannerScene;

public sealed record SavePlannerSceneCommand(
    int ProjectId,
    int ExpectedRevision,
    string SceneJson) : IRequest<SavePlannerSceneResult>;

public sealed record SavePlannerSceneResult(
    Guid BoardId,
    int Revision,
    DateTime SavedAt);

public sealed class SavePlannerSceneCommandValidator
    : AbstractValidator<SavePlannerSceneCommand>
{
    public SavePlannerSceneCommandValidator()
    {
        RuleFor(x => x.ProjectId).GreaterThan(0);
        RuleFor(x => x.ExpectedRevision).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SceneJson)
            .NotEmpty()
            .MaximumLength(PlannerSceneDocument.MaximumLength);
    }
}

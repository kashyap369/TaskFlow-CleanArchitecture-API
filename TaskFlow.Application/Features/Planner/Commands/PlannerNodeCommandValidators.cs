using FluentValidation;
using TaskFlow.Application.Features.Planner.Commands.CreatePlannerSubTaskNode;
using TaskFlow.Application.Features.Planner.Commands.CreatePlannerTaskNode;
using TaskFlow.Application.Features.Planner.Commands.LinkPlannerProject;
using TaskFlow.Application.Features.Planner.Commands.RemovePlannerNode;
using TaskFlow.Application.Features.Planner.Commands.UpdatePlannerNode;
using TaskFlow.Application.Features.Planner.Commands.FinalizePrimaryRequirements;

namespace TaskFlow.Application.Features.Planner.Commands;

public sealed class FinalizePrimaryRequirementsCommandValidator : AbstractValidator<FinalizePrimaryRequirementsCommand>
{
    public FinalizePrimaryRequirementsCommandValidator() => RuleFor(x => x.ProjectId).GreaterThan(0);
}

public sealed class LinkPlannerProjectCommandValidator : AbstractValidator<LinkPlannerProjectCommand>
{
    public LinkPlannerProjectCommandValidator() { RuleFor(x => x.ProjectId).GreaterThan(0); RuleFor(x => x.ElementId).NotEmpty().MaximumLength(128); }
}

public sealed class CreatePlannerTaskNodeCommandValidator : AbstractValidator<CreatePlannerTaskNodeCommand>
{
    public CreatePlannerTaskNodeCommandValidator()
    {
        RuleFor(x => x.ProjectId).GreaterThan(0); RuleFor(x => x.ElementId).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200); RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.StartDate).NotEmpty();
        RuleFor(x => x.ExpectedCompletionDate).GreaterThan(x => x.StartDate).When(x => x.ExpectedCompletionDate.HasValue);
        RuleFor(x => x.ChangeReason).MaximumLength(500);
    }
}

public sealed class CreatePlannerSubTaskNodeCommandValidator : AbstractValidator<CreatePlannerSubTaskNodeCommand>
{
    public CreatePlannerSubTaskNodeCommandValidator()
    { RuleFor(x => x.ProjectId).GreaterThan(0); RuleFor(x => x.ElementId).NotEmpty().MaximumLength(128); RuleFor(x => x.TaskId).GreaterThan(0); RuleFor(x => x.Title).NotEmpty().MaximumLength(200); RuleFor(x => x.ChangeReason).MaximumLength(500); }
}

public sealed class UpdatePlannerNodeCommandValidator : AbstractValidator<UpdatePlannerNodeCommand>
{
    public UpdatePlannerNodeCommandValidator()
    {
        RuleFor(x => x.ProjectId).GreaterThan(0); RuleFor(x => x.NodeId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200); RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.ProblemStatement).MaximumLength(4000);
        RuleFor(x => x.BudgetAmount).GreaterThanOrEqualTo(0).When(x => x.BudgetAmount.HasValue);
        RuleFor(x => x.BudgetCurrency).NotEmpty().Length(3).Matches("^[A-Za-z]{3}$").When(x => x.BudgetAmount.HasValue);
        RuleFor(x => x.ApproximateDurationWeeks).InclusiveBetween(1, 520).When(x => x.ApproximateDurationWeeks.HasValue);
        RuleFor(x => x.ChangeReason).MaximumLength(500);
    }
}

public sealed class RemovePlannerNodeCommandValidator : AbstractValidator<RemovePlannerNodeCommand>
{
    public RemovePlannerNodeCommandValidator() { RuleFor(x => x.ProjectId).GreaterThan(0); RuleFor(x => x.NodeId).NotEmpty(); RuleFor(x => x.ChangeReason).MaximumLength(500); }
}

using FluentValidation;

namespace TaskFlow.Application.Features.WorkManagement.Projects.Commands.ImportProjectPlan;

public sealed class ImportProjectPlanCommandValidator
    : AbstractValidator<ImportProjectPlanCommand>
{
    public ImportProjectPlanCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.StartDate).NotEmpty();
        RuleFor(x => x.ExpectedCompletionDate)
            .GreaterThan(x => x.StartDate)
            .When(x => x.ExpectedCompletionDate.HasValue);
        RuleFor(x => x.OrganizationId).GreaterThan(0).When(x => x.OrganizationId.HasValue);
        RuleFor(x => x.Tasks).NotNull().NotEmpty().Must(x => x.Count <= 500)
            .WithMessage("A project plan can contain at most 500 tasks.");
        RuleFor(x => x.Tasks.Sum(task => task.SubTasks.Count)).LessThanOrEqualTo(5000)
            .WithMessage("A project plan can contain at most 5,000 subtasks.");
        RuleForEach(x => x.Tasks).SetValidator(new ImportProjectPlanTaskValidator());
    }
}

public sealed class ImportProjectPlanTaskValidator
    : AbstractValidator<ImportProjectPlanTask>
{
    public ImportProjectPlanTaskValidator()
    {
        RuleFor(x => x.Key).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.StartDate).NotEmpty();
        RuleFor(x => x.ExpectedCompletionDate)
            .GreaterThan(x => x.StartDate)
            .When(x => x.ExpectedCompletionDate.HasValue);
        RuleFor(x => x.Priority).IsInEnum();
        RuleFor(x => x.EstimateMinutes).GreaterThanOrEqualTo(0).When(x => x.EstimateMinutes.HasValue);
        RuleFor(x => x.TeamName).MaximumLength(200);
        RuleFor(x => x.AssigneeEmail).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.AssigneeEmail));
        RuleForEach(x => x.SubTasks).NotEmpty().MaximumLength(200);
    }
}

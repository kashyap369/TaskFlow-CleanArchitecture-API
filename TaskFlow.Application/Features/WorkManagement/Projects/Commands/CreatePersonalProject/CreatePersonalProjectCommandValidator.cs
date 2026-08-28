using FluentValidation;

namespace TaskFlow.Application.Features.WorkManagement.Projects.Commands.CreatePersonalProject;

public sealed class CreatePersonalProjectCommandValidator
    : AbstractValidator<CreatePersonalProjectCommand>
{
    public CreatePersonalProjectCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .MaximumLength(2000);

        RuleFor(x => x.StartDate)
            .NotEmpty();

        RuleFor(x => x.ExpectedCompletionDate)
            .GreaterThan(x => x.StartDate)
            .When(x => x.ExpectedCompletionDate.HasValue);

        RuleFor(x => x.ProblemStatement).MaximumLength(4000);
        RuleFor(x => x.BudgetAmount).GreaterThanOrEqualTo(0).When(x => x.BudgetAmount.HasValue);
        RuleFor(x => x.BudgetCurrency)
            .NotEmpty().Length(3).Matches("^[A-Za-z]{3}$")
            .When(x => x.BudgetAmount.HasValue);
        RuleFor(x => x.ApproximateDurationWeeks)
            .InclusiveBetween(1, 520)
            .When(x => x.ApproximateDurationWeeks.HasValue);
    }
}

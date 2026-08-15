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
    }
}

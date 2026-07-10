using FluentValidation;

namespace TaskFlow.Application.Features.WorkManagement.Projects.Commands.CreateProject
{
    public sealed class CreateProjectCommandValidator
        : AbstractValidator<CreateProjectCommand>
    {
        public CreateProjectCommandValidator()
        {
            RuleFor(x => x.Title)
                .NotEmpty()
                .MaximumLength(200);

            RuleFor(x => x.Description)
                .MaximumLength(2000);

            RuleFor(x => x.OrganizationId)
                .GreaterThan(0);

            RuleFor(x => x.StartDate)
                .NotEmpty();

            RuleFor(x => x.ExpectedCompletionDate)
                .GreaterThan(x => x.StartDate)
                .When(x => x.ExpectedCompletionDate.HasValue);
        }
    }
}
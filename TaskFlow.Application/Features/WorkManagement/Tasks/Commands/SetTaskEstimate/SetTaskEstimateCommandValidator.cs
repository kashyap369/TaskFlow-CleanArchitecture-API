using FluentValidation;

namespace TaskFlow.Application.Features.WorkManagement.Tasks.Commands.SetTaskEstimate;

public sealed class SetTaskEstimateCommandValidator : AbstractValidator<SetTaskEstimateCommand>
{
    public SetTaskEstimateCommandValidator()
    {
        RuleFor(x => x.TaskId).GreaterThan(0);
        RuleFor(x => x.EstimateMinutes)
            .InclusiveBetween(0, 525_600)
            .When(x => x.EstimateMinutes.HasValue)
            .WithMessage("Estimate must be between 0 and 525600 minutes.");
    }
}

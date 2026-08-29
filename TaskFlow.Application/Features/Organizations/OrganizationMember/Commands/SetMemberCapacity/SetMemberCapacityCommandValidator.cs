using FluentValidation;

namespace TaskFlow.Application.Features.Organizations.OrganizationMember.Commands.SetMemberCapacity;

public sealed class SetMemberCapacityCommandValidator : AbstractValidator<SetMemberCapacityCommand>
{
    public SetMemberCapacityCommandValidator()
    {
        RuleFor(x => x.OrganizationId).GreaterThan(0);
        RuleFor(x => x.UserId).GreaterThan(0);
        RuleFor(x => x.WeeklyCapacityMinutes)
            .InclusiveBetween(0, 10_080)
            .When(x => x.WeeklyCapacityMinutes.HasValue)
            .WithMessage("Weekly capacity must be between 0 and 10080 minutes.");
    }
}

using FluentValidation;

namespace TaskFlow.Application.Features.Identity.User.Commands.LogoutUser
{
    public sealed class LogoutUserCommandValidator
        : AbstractValidator<LogoutUserCommand>
    {
        public LogoutUserCommandValidator()
        {
            RuleFor(x => x.RefreshToken)
                .NotEmpty();
        }
    }
}

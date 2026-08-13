using FluentValidation;
using TaskFlow.Application.Features.Identity.User.Commands.LoginWithCode;

namespace TaskFlow.Application.Features.Identity.User.Validators.Commands.LoginWithCode
{
    public sealed class LoginWithCodeCommandValidator
        : AbstractValidator<LoginWithCodeCommand>
    {
        public LoginWithCodeCommandValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty()
                .EmailAddress()
                .MaximumLength(256);
            RuleFor(x => x.Code)
                .NotEmpty()
                .Matches("^[0-9]{6}$")
                .WithMessage("Code must contain exactly 6 digits.");
        }
    }
}

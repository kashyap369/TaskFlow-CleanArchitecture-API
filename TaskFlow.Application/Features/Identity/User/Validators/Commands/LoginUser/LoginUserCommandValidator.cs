using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;
using TaskFlow.Application.Features.Identity.User.Commands.LoginUser;

namespace TaskFlow.Application.Features.Identity.User.Validators.Commands.LoginUser
{
    public sealed class LoginUserCommandValidator
        : AbstractValidator<LoginUserCommand>
    {
        public LoginUserCommandValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty()
                .EmailAddress();

            RuleFor(x => x.Password)
                .NotEmpty();
        }
    }
}

using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;
using TaskFlow.Application.Features.Identity.User.Commands.RegisterUser;

namespace TaskFlow.Application.Features.Identity.User.Validators.Commands.RegisterUser
{
    public sealed class RegisterUserCommandValidator
       : AbstractValidator<RegisterUserCommand>
    {
        public RegisterUserCommandValidator()
        {
            RuleFor(x => x.FirstName)
                .NotEmpty()
                .MaximumLength(100);

            RuleFor(x => x.LastName)
                .NotEmpty()
                .MaximumLength(100);

            RuleFor(x => x.Email)
                .NotEmpty()
                .EmailAddress()
                .MaximumLength(256);

            RuleFor(x => x.PhoneNumber)
                .NotEmpty()
                .MinimumLength(10)
                .MaximumLength(20);

            RuleFor(x => x.Password)
                .NotEmpty()
                .MinimumLength(8)
                .Matches("[A-Z]")
                .WithMessage("Password must contain an uppercase letter.")
                .Matches("[a-z]")
                .WithMessage("Password must contain a lowercase letter.")
                .Matches("[0-9]")
                .WithMessage("Password must contain a number.");

            RuleFor(x => x.AccountType)
                .IsInEnum();
        }
    }
}

using MediatR;
using TaskFlow.Domain.Enums.Identity;

namespace TaskFlow.Application.Features.Identity.User.Commands.RegisterUser
{
    public sealed record RegisterUserCommand(
        string FirstName,
        string LastName,
        string Email,
        string PhoneNumber,
        string Password,
        AccountType AccountType = AccountType.Individual
    ) : IRequest<int>;
}
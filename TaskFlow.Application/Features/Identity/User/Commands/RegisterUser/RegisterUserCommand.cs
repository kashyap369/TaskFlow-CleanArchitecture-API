using MediatR;

namespace TaskFlow.Application.Features.Identity.User.Commands.RegisterUser
{
    public sealed record RegisterUserCommand(
        string FirstName,
        string LastName,
        string Email,
        string PhoneNumber,
        string Password
    ) : IRequest<int>;
}
using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Interfaces.Identity.Users;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.ValueObjects;

namespace TaskFlow.Application.Features.Identity.User.Commands.RegisterUser
{
    public sealed class RegisterUserCommandHandler
        : IRequestHandler<RegisterUserCommand, int>
    {
        private readonly IUserRepository _userRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPasswordHasher _passwordHasher;

        public RegisterUserCommandHandler(
            IUserRepository userRepository,
            IUnitOfWork unitOfWork,
            IPasswordHasher passwordHasher)
        {
            _userRepository = userRepository;
            _unitOfWork = unitOfWork;
            _passwordHasher = passwordHasher;
        }

        public async Task<int> Handle(
            RegisterUserCommand request,
            CancellationToken cancellationToken)
        {
            var email = new Email(
                request.Email);

            var phoneNumber = new PhoneNumber(
                request.PhoneNumber);

            var emailExists =
                await _userRepository.ExistsByEmailAsync(
                    email,
                    cancellationToken);

            if (emailExists)
            {
                throw new ConflictException(
                    "EMAIL_ALREADY_EXISTS",
                    "Email already exists.");
            }

            var phoneExists =
                await _userRepository.ExistsByPhoneNumberAsync(
                    phoneNumber,
                    cancellationToken);

            if (phoneExists)
            {
                throw new ConflictException(
                    "PHONE_NUMBER_ALREADY_EXISTS",
                    "Phone number already exists.");
            }

            var fullName = new FullName(
                request.FirstName,
                request.LastName);

            var passwordHash =
                _passwordHasher.Hash(
                    request.Password);

            var user =
                Domain.Entities.Identity.User.Register(
                    fullName,
                    email,
                    phoneNumber,
                    passwordHash);

            await _userRepository.AddAsync(
                user,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);

            return user.Id;
        }
    }
}
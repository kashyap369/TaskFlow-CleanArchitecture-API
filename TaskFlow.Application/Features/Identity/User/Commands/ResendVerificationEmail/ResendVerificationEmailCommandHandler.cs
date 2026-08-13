using MediatR;
using TaskFlow.Application.Contracts.Configuration;
using TaskFlow.Application.Contracts.Email;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Domain.Interfaces.Identity.Users;
using TaskFlow.Domain.ValueObjects;

namespace TaskFlow.Application.Features.Identity.User.Commands.ResendVerificationEmail
{
    public sealed class ResendVerificationEmailCommandHandler
        : IRequestHandler<ResendVerificationEmailCommand>
    {
        private readonly IUserRepository _userRepository;
        private readonly IEmailVerificationTokenService _tokenService;
        private readonly IEmailService _emailService;
        private readonly IClientUrlProvider _clientUrlProvider;

        public ResendVerificationEmailCommandHandler(
            IUserRepository userRepository,
            IEmailVerificationTokenService tokenService,
            IEmailService emailService,
            IClientUrlProvider clientUrlProvider)
        {
            _userRepository = userRepository;
            _tokenService = tokenService;
            _emailService = emailService;
            _clientUrlProvider = clientUrlProvider;
        }

        public async Task Handle(
            ResendVerificationEmailCommand request,
            CancellationToken cancellationToken)
        {
            // Silent no-op for unknown or already-verified addresses. Telling
            // the caller which is which would let anyone enumerate accounts.
            var user =
                await _userRepository.GetByEmailAsync(
                    new Email(request.Email),
                    cancellationToken);

            if (user is null || user.IsEmailVerified)
            {
                return;
            }

            var verifyUrl =
                $"{_clientUrlProvider.BaseUrl}/auth/verify-email?token="
                + Uri.EscapeDataString(
                    _tokenService.Generate(user.Id));

            var templatePath = Path.Combine(
                AppContext.BaseDirectory,
                "Email",
                "Templates",
                "Welcome.html");

            var template =
                await File.ReadAllTextAsync(
                    templatePath,
                    cancellationToken);

            template = template
                .Replace("{{UserName}}", user.FullName.ToString())
                .Replace("{{Email}}", user.Email.Value)
                .Replace("{{VerifyUrl}}", verifyUrl)
                .Replace("{{LoginUrl}}", verifyUrl)
                .Replace("{{CurrentYear}}", DateTime.UtcNow.Year.ToString());

            await _emailService.SendAsync(
                user.Email.Value,
                "Verify your TaskFlow account",
                template,
                cancellationToken);
        }
    }
}

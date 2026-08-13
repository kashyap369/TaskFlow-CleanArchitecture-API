using MediatR;
using Microsoft.Extensions.Logging;
using TaskFlow.Application.Features.Identity.User.Services;
using TaskFlow.Domain.Enums.Identity;
using TaskFlow.Domain.Interfaces.Identity.Users;
using TaskFlow.Domain.ValueObjects;

namespace TaskFlow.Application.Features.Identity.User.Commands.RequestPasswordReset
{
    public sealed class RequestPasswordResetCommandHandler
        : IRequestHandler<RequestPasswordResetCommand>
    {
        private readonly IUserRepository _userRepository;
        private readonly OneTimeCodeRequestService _codeService;
        private readonly ILogger<RequestPasswordResetCommandHandler> _logger;

        public RequestPasswordResetCommandHandler(
            IUserRepository userRepository,
            OneTimeCodeRequestService codeService,
            ILogger<RequestPasswordResetCommandHandler> logger)
        {
            _userRepository = userRepository;
            _codeService = codeService;
            _logger = logger;
        }

        public async Task Handle(
            RequestPasswordResetCommand request,
            CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByEmailAsync(
                new Email(request.Email),
                cancellationToken);

            if (user is null || !user.IsEmailVerified)
                return;

            try
            {
                await _codeService.IssueAsync(
                    user,
                    OneTimeCodePurpose.PasswordReset,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Could not deliver a password recovery code for {UserId}.",
                    user.Id);
            }
        }
    }
}

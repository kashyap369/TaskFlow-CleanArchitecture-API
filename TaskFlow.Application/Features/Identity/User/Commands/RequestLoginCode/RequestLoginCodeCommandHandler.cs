using MediatR;
using Microsoft.Extensions.Logging;
using TaskFlow.Application.Features.Identity.User.Services;
using TaskFlow.Domain.Enums.Identity;
using TaskFlow.Domain.Interfaces.Identity.Users;
using TaskFlow.Domain.ValueObjects;

namespace TaskFlow.Application.Features.Identity.User.Commands.RequestLoginCode
{
    public sealed class RequestLoginCodeCommandHandler
        : IRequestHandler<RequestLoginCodeCommand>
    {
        private readonly IUserRepository _userRepository;
        private readonly OneTimeCodeRequestService _codeService;
        private readonly ILogger<RequestLoginCodeCommandHandler> _logger;

        public RequestLoginCodeCommandHandler(
            IUserRepository userRepository,
            OneTimeCodeRequestService codeService,
            ILogger<RequestLoginCodeCommandHandler> logger)
        {
            _userRepository = userRepository;
            _codeService = codeService;
            _logger = logger;
        }

        public async Task Handle(
            RequestLoginCodeCommand request,
            CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByEmailAsync(
                new Email(request.Email),
                cancellationToken);

            if (user is null ||
                !user.IsEmailVerified ||
                user.Status != UserStatus.Active)
            {
                return;
            }

            try
            {
                await _codeService.IssueAsync(
                    user,
                    OneTimeCodePurpose.PasswordlessLogin,
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
                    "Could not deliver a passwordless login code for {UserId}.",
                    user.Id);
            }
        }
    }
}

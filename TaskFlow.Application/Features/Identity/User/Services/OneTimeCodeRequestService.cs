using TaskFlow.Application.Contracts.Email;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Domain.Entities.Identity;
using TaskFlow.Domain.Enums.Identity;
using TaskFlow.Domain.Interfaces.Identity.Users;
using TaskFlow.Domain.Interfaces.Persistence;

namespace TaskFlow.Application.Features.Identity.User.Services
{
    public sealed class OneTimeCodeRequestService
    {
        public const int ExpiryMinutes = 10;
        public const int ResendCooldownSeconds = 60;
        public const int MaxAttempts = 5;

        private readonly IOneTimeCodeRepository _repository;
        private readonly IOneTimeCodeProtector _protector;
        private readonly IEmailService _emailService;
        private readonly IUnitOfWork _unitOfWork;

        public OneTimeCodeRequestService(
            IOneTimeCodeRepository repository,
            IOneTimeCodeProtector protector,
            IEmailService emailService,
            IUnitOfWork unitOfWork)
        {
            _repository = repository;
            _protector = protector;
            _emailService = emailService;
            _unitOfWork = unitOfWork;
        }

        public async Task IssueAsync(
            Domain.Entities.Identity.User user,
            OneTimeCodePurpose purpose,
            CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var latest = await _repository.GetLatestAsync(
                user.Id,
                purpose,
                cancellationToken);

            if (latest is not null &&
                !latest.IsConsumed &&
                latest.CreatedAt.AddSeconds(ResendCooldownSeconds) > now)
            {
                return;
            }

            var existing = await _repository.GetUnconsumedAsync(
                user.Id,
                purpose,
                cancellationToken);

            foreach (var item in existing)
            {
                item.Consume(now);
                _repository.Update(item);
            }

            var code = _protector.GenerateCode();
            var oneTimeCode = new OneTimeCode(
                user.Id,
                purpose,
                _protector.Protect(user.Id, purpose, code),
                now.AddMinutes(ExpiryMinutes),
                MaxAttempts);

            await _repository.AddAsync(oneTimeCode, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var templatePath = Path.Combine(
                AppContext.BaseDirectory,
                "Email",
                "Templates",
                "OneTimeCode.html");
            var template = await File.ReadAllTextAsync(
                templatePath,
                cancellationToken);

            var isReset = purpose == OneTimeCodePurpose.PasswordReset;
            template = template
                .Replace("{{UserName}}", user.FullName.DisplayName)
                .Replace("{{Code}}", code)
                .Replace("{{Purpose}}", isReset ? "reset your password" : "sign in")
                .Replace("{{ExpiryMinutes}}", ExpiryMinutes.ToString())
                .Replace("{{CurrentYear}}", DateTime.UtcNow.Year.ToString());

            try
            {
                await _emailService.SendAsync(
                    user.Email.Value,
                    isReset ? "Reset your TaskFlow password" : "Your TaskFlow sign-in code",
                    template,
                    cancellationToken);
            }
            catch
            {
                // Let a subsequent request retry immediately if delivery failed.
                oneTimeCode.Consume(DateTime.UtcNow);
                _repository.Update(oneTimeCode);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                throw;
            }
        }
    }
}

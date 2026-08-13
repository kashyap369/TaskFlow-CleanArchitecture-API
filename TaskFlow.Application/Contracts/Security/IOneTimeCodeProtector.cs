using TaskFlow.Domain.Enums.Identity;

namespace TaskFlow.Application.Contracts.Security
{
    public interface IOneTimeCodeProtector
    {
        string GenerateCode();

        string Protect(
            int userId,
            OneTimeCodePurpose purpose,
            string code);

        bool Verify(
            int userId,
            OneTimeCodePurpose purpose,
            string code,
            string protectedCode);
    }
}

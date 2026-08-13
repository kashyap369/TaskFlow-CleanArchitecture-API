using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Domain.Enums.Identity;

namespace TaskFlow.Infra.Security
{
    public sealed class OneTimeCodeProtector : IOneTimeCodeProtector
    {
        private readonly byte[] _key;

        public OneTimeCodeProtector(IOptions<OneTimeCodeSettings> options)
        {
            _key = Encoding.UTF8.GetBytes(options.Value.SecretKey);
        }

        public string GenerateCode() =>
            RandomNumberGenerator
                .GetInt32(0, 1_000_000)
                .ToString("D6", CultureInfo.InvariantCulture);

        public string Protect(
            int userId,
            OneTimeCodePurpose purpose,
            string code)
        {
            var payload = Encoding.UTF8.GetBytes(
                $"{userId}:{(int)purpose}:{code}");

            return Convert.ToBase64String(
                HMACSHA256.HashData(_key, payload));
        }

        public bool Verify(
            int userId,
            OneTimeCodePurpose purpose,
            string code,
            string protectedCode)
        {
            byte[] expected;
            try
            {
                expected = Convert.FromBase64String(protectedCode);
            }
            catch (FormatException)
            {
                return false;
            }

            var actual = Convert.FromBase64String(
                Protect(userId, purpose, code));

            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
    }
}

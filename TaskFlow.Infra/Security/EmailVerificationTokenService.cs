using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Contracts.Security;

namespace TaskFlow.Infra.Security
{
    /// <summary>
    /// Stateless email-verification tokens: <c>userId.expiresUnix.signature</c>,
    /// base64url-encoded. The signature is an HMAC-SHA256 over the user id and
    /// the expiry, keyed with the JWT secret — so a token is bound to one user,
    /// expires on its own, and cannot be forged or edited without the secret.
    /// </summary>
    public sealed class EmailVerificationTokenService
        : IEmailVerificationTokenService
    {
        private const int ValidForHours = 48;

        private readonly JwtSettings _jwtSettings;

        public EmailVerificationTokenService(
            IOptions<JwtSettings> jwtSettings)
        {
            _jwtSettings = jwtSettings.Value;
        }

        public string Generate(int userId)
        {
            var expiresUnix =
                DateTimeOffset.UtcNow
                    .AddHours(ValidForHours)
                    .ToUnixTimeSeconds();

            return Base64UrlEncode(
                $"{userId}.{expiresUnix}.{Sign(userId, expiresUnix)}");
        }

        public bool TryValidate(
            string token,
            out int userId)
        {
            userId = 0;

            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            string decoded;

            try
            {
                decoded = Base64UrlDecode(token);
            }
            catch
            {
                // Malformed base64 is just an invalid token, not a fault.
                return false;
            }

            var parts = decoded.Split('.');

            if (parts.Length != 3
                || !int.TryParse(parts[0], out var parsedUserId)
                || !long.TryParse(parts[1], out var expiresUnix))
            {
                return false;
            }

            var expected = Sign(parsedUserId, expiresUnix);

            var signatureMatches =
                CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(expected),
                    Encoding.UTF8.GetBytes(parts[2]));

            if (!signatureMatches)
            {
                return false;
            }

            if (DateTimeOffset.FromUnixTimeSeconds(expiresUnix)
                < DateTimeOffset.UtcNow)
            {
                return false;
            }

            userId = parsedUserId;

            return true;
        }

        private string Sign(
            int userId,
            long expiresUnix)
        {
            using var hmac = new HMACSHA256(
                Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));

            var hash =
                hmac.ComputeHash(
                    Encoding.UTF8.GetBytes($"{userId}|{expiresUnix}"));

            return Convert.ToHexString(hash);
        }

        private static string Base64UrlEncode(string value) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');

        private static string Base64UrlDecode(string value)
        {
            var padded =
                value.Replace('-', '+').Replace('_', '/');

            padded = (padded.Length % 4) switch
            {
                2 => padded + "==",
                3 => padded + "=",
                _ => padded
            };

            return Encoding.UTF8.GetString(
                Convert.FromBase64String(padded));
        }
    }
}

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Contracts.Security;

namespace TaskFlow.Infra.Security;

public sealed class MeetingGuestCodeProtector(IOptions<OneTimeCodeSettings> options) : IMeetingGuestCodeProtector
{
    private readonly byte[] key = Encoding.UTF8.GetBytes(options.Value.SecretKey);
    public string GenerateCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    public string Protect(int accessLinkId, string normalizedEmail, string code) =>
        Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes($"meeting:{accessLinkId}:{normalizedEmail}:{code}")));
    public bool Verify(int accessLinkId, string normalizedEmail, string code, string protectedCode)
    {
        byte[] expected; try { expected = Convert.FromHexString(protectedCode); } catch (FormatException) { return false; }
        return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(Protect(accessLinkId, normalizedEmail, code)), expected);
    }
}

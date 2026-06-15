using System;
using System.Collections.Generic;
using System.Text;

namespace TaskFlow.Infra.Security
{
    public sealed class JwtSettings
    {
        public string Issuer { get; set; } = string.Empty;

        public string Audience { get; set; } = string.Empty;

        public string SecretKey { get; set; } = string.Empty;

        public int ExpiryMinutes { get; set; }

        public int RefreshTokenExpiryDays { get; set; }
    }
}

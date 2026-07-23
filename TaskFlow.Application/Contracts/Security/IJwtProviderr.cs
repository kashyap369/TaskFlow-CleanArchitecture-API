using System;
using System.Collections.Generic;
using System.Text;

namespace TaskFlow.Application.Contracts.Security
{
    public interface IJwtProvider
    {
        string GenerateToken(
            int userId,
            string email,
            IReadOnlyList<string> roles);

        string GenerateRefreshToken();

        DateTime GetRefreshTokenExpiryDate();
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace TaskFlow.Application.Contracts.Security
{
    public interface IPasswordHasher
    {
        string Hash(string password);

        bool Verify(string password,string passwordHash);
    }
}

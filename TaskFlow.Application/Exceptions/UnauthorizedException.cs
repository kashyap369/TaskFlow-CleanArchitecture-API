using System;
using System.Collections.Generic;
using System.Text;

namespace TaskFlow.Application.Exceptions
{
    public sealed class UnauthorizedException
        : BusinessException
    {
        public UnauthorizedException(
            string code,
            string message)
            : base(code, message)
        {
        }
    }
}

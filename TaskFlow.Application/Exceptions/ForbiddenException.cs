using System;
using System.Collections.Generic;
using System.Text;

namespace TaskFlow.Application.Exceptions
{
    public sealed class ForbiddenException
        : BusinessException
    {
        public ForbiddenException(
            string code,
            string message)
            : base(code, message)
        {
        }
    }
}

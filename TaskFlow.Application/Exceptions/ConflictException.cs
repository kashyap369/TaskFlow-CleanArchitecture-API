using System;
using System.Collections.Generic;
using System.Text;

namespace TaskFlow.Application.Exceptions
{
    public sealed class ConflictException
        : BusinessException
    {
        public ConflictException(
            string code,
            string message)
            : base(code, message)
        {
        }
    }
}

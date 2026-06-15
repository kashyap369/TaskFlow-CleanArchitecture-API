using System;
using System.Collections.Generic;
using System.Text;

namespace TaskFlow.Application.Exceptions
{
    public sealed class NotFoundException
        : BusinessException
    {
        public NotFoundException(
            string code,
            string message)
            : base(code, message)
        {
        }
    }
}

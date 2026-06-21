using System;
using System.Collections.Generic;
using System.Text;

namespace TaskFlow.Domain.Exceptions
{
    public sealed class BadRequestException : Exception
    {
        public string Code { get; }

        public BadRequestException(
            string code,
            string message)
            : base(message)
        {
            Code = code;
        }
    }
}

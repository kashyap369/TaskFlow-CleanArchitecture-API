using System;
using System.Collections.Generic;
using System.Text;

namespace TaskFlow.Application.Exceptions
{
    public class BusinessException : Exception
    {
        public string Code { get; }

        public BusinessException(
            string code,
            string message)
            : base(message)
        {
            Code = code;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace TaskFlow.Domain.Enums
{
    public enum UserStatus
    {
        Active = 1,
        Inactive = 2,
        Suspended = 3,
        PendingVerification = 4
    }
}

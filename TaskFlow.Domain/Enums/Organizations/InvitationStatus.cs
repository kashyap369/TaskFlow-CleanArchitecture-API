using System;
using System.Collections.Generic;
using System.Text;

namespace TaskFlow.Domain.Enums.Organizations
{
    public enum InvitationStatus
    {
        Pending = 1,
        Accepted = 2,
        Rejected = 3,
        Expired = 4,
        Cancelled = 5
    }
}

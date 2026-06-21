using System;
using System.Collections.Generic;
using System.Text;

namespace TaskFlow.Domain.Enums.WorkManagement
{
    public enum ProjectStatus
    {
        Draft = 1,
        Active = 2,
        OnHold = 3,
        Completed = 4,
        Archived = 5,
        Cancelled = 6
    }
}

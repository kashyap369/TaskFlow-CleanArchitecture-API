using System;
using System.Collections.Generic;
using System.Text;

namespace TaskFlow.Domain.Enums.WorkManagement
{
    public enum TaskStatus
    {
        Todo = 1,
        InProgress = 2,
        Completed = 3,
        Blocked = 4,
        Cancelled = 5
    }
}

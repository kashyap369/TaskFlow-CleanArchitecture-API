using System;
using System.Collections.Generic;
using System.Text;

namespace TaskFlow.Domain.DomainEvents
{
    public interface IDomainEvent
    {
        DateTime OccurredOn { get; }
    }
}

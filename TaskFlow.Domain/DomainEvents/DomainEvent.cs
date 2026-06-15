using System;
using System.Collections.Generic;
using System.Text;

namespace TaskFlow.Domain.DomainEvents
{
    public abstract class DomainEvent : IDomainEvent
    {
        public DateTime OccurredOn { get; }

        protected DomainEvent()
        {
            OccurredOn = DateTime.UtcNow;
        }
    }
}

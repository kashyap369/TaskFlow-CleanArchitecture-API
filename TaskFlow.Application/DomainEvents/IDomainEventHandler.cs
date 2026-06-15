using System;
using System.Collections.Generic;
using System.Text;
using TaskFlow.Domain.DomainEvents;

namespace TaskFlow.Application.DomainEvents
{
    public interface IDomainEventHandler<in TDomainEvent> where TDomainEvent : IDomainEvent
    {
        Task HandleAsync(TDomainEvent domainEvent,CancellationToken cancellationToken);
    }
}

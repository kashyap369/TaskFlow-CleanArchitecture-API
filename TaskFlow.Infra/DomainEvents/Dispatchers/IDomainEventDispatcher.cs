using System;
using System.Collections.Generic;
using System.Text;
using TaskFlow.Domain.DomainEvents;

namespace TaskFlow.Infra.DomainEvents.Dispatchers
{
    public interface IDomainEventDispatcher
    {
        Task DispatchAsync(
            IEnumerable<IDomainEvent> domainEvents,
            CancellationToken cancellationToken = default);
    }
}

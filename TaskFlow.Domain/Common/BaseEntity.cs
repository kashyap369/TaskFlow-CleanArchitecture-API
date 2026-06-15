using System;
using System.Collections.Generic;
using TaskFlow.Domain.DomainEvents;


namespace TaskFlow.Domain.Common
{
    public abstract class BaseEntity
    {
        private readonly List<IDomainEvent> _domainEvents = new();

        public int Id { get; protected set; }

        public IReadOnlyCollection<IDomainEvent> DomainEvents
            => _domainEvents.AsReadOnly();

        protected void AddDomainEvent(IDomainEvent domainEvent)
        {
            _domainEvents.Add(domainEvent);
        }

        public void RemoveDomainEvent(IDomainEvent domainEvent)
        {
            _domainEvents.Remove(domainEvent);
        }

        public void ClearDomainEvents()
        {
            _domainEvents.Clear();
        }
    }
}
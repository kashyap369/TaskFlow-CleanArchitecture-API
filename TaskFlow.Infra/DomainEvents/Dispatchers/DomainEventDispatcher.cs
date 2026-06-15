using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Application.DomainEvents;
using TaskFlow.Domain.DomainEvents;

namespace TaskFlow.Infra.DomainEvents.Dispatchers
{
    public sealed class DomainEventDispatcher
        : IDomainEventDispatcher
    {
        private readonly IServiceProvider _serviceProvider;

        public DomainEventDispatcher(
            IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task DispatchAsync(
            IEnumerable<IDomainEvent> domainEvents,
            CancellationToken cancellationToken = default)
        {
            foreach (var domainEvent in domainEvents)
            {
                var handlerType =
                    typeof(IDomainEventHandler<>)
                        .MakeGenericType(domainEvent.GetType());

                var handlers =
                    _serviceProvider.GetServices(handlerType);

                foreach (var handler in handlers)
                {
                    var method =
                        handlerType.GetMethod("HandleAsync");

                    if (method is not null)
                    {
                        await (Task)method.Invoke(
                            handler,
                            new object[]
                            {
                                domainEvent,
                                cancellationToken
                            });
                    }
                }
            }
        }
    }
}
using System;
using Microsoft.Extensions.DependencyInjection;

namespace Proto.DependencyInjection
{
    public class DependencyResolver : IDependencyResolver
    {
        private readonly IServiceProvider _services;

        public DependencyResolver(IServiceProvider services)
        {
            _services = services;
        }

        public Props PropsFor<TActor>() where TActor : IActor => Props.FromProducer(() => _services.GetService<TActor>()!);

        public Props PropsFor(Type actorType) => Props.FromProducer(() => (IActor)_services.GetService(actorType)!);
    }
}
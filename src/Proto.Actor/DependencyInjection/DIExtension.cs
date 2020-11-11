using System;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Proto.Extensions;

namespace Proto.DependencyInjection
{
    [PublicAPI]
    public class DIExtension : IActorSystemExtension<DIExtension>
    {
        public DIExtension(IDependencyResolver resolver)
        {
            Resolver = resolver;
        }
        public IDependencyResolver Resolver { get; }
    }
    
    public static class Extensions 
    {
        public static IDependencyResolver DI(this ActorSystem system) => system.Extensions.Get<DIExtension>().Resolver;
    }

    public class MsExtDependencyResolver : IDependencyResolver
    {
        private readonly ServiceProvider _services;

        public MsExtDependencyResolver(ServiceProvider services)
        {
            _services = services;
        }
        
        // public Type GetType(string actorName)
        // {
        //     throw new NotImplementedException();
        // }
        //
        // public Func<IActor> CreateActorFactory(Type actorType)
        // {
        //     throw new NotImplementedException();
        // }

        public Props Create<TActor>() where TActor : IActor => Props.FromProducer(() => _services.GetService<TActor>()!);

        public Props Create(Type actorType) => Props.FromProducer(() => (IActor)_services.GetService(actorType)!);

        public void Release(IActor actor)
        {
        }
    }
}
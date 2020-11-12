using JetBrains.Annotations;
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
}
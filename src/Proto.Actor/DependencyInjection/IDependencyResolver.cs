using System;

namespace Proto.DependencyInjection
{
    public interface IDependencyResolver
    {
        Props PropsFor<TActor>() where TActor : IActor;
        Props PropsFor(Type actorType);
    }
}
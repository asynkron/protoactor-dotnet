using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Proto
{
    public static class ServiceCollectionExtensions
    {
        public static void AddProtoActor(this IServiceCollection services, Action<ActorPropsRegistry> registerAction = null)
        {
            services.AddSingleton<IActorFactory, ActorFactory>();
            services.TryAdd(ServiceDescriptor.Singleton(typeof(EventStream<>), typeof(EventStream<>)));

            var registry = new ActorPropsRegistry();
            registerAction?.Invoke(registry);
            services.AddSingleton(registry);
        }
    }
}
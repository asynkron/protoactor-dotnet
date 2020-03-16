using System;
using Microsoft.Extensions.DependencyInjection;

namespace Proto
{
    public static class ServiceCollectionExtensions
    {
        public static void AddProtoActor(this IServiceCollection services, Action<ActorPropsRegistry> registerAction = null)
        {
            services.AddSingleton<IActorFactory, ActorFactory>();

            var registry = new ActorPropsRegistry();
            registerAction?.Invoke(registry);
            services.AddSingleton(registry);
            services.AddSingleton<ActorSystem>();
        }
    }
}
// -----------------------------------------------------------------------
// <copyright file="DIExtension.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Proto.Extensions;

namespace Proto.DependencyInjection
{
    public class DI : IActorSystemOption
    {
        private readonly IServiceProvider _provider;

        public static DI WithServiceProvider(IServiceProvider provider)
        {
            var di = new DI(provider);
            return di;
        }

        private DI(IServiceProvider provider) => _provider = provider;

        Task IActorSystemOption.Apply(ActorSystem system)
        {
            system.WithServiceProvider(_provider);
            return Task.CompletedTask;
        }
    }
    [PublicAPI]
    public class DIExtension : IActorSystemExtension<DIExtension>
    {
        public DIExtension(IDependencyResolver resolver) => Resolver = resolver;

        public IDependencyResolver Resolver { get; }
    }

    public static class Extensions
    {
        public static ActorSystem WithServiceProvider(this ActorSystem actorSystem, IServiceProvider serviceProvider)
        {
            var dependencyResolver = new DependencyResolver(serviceProvider);
            var diExtension = new DIExtension(dependencyResolver);
            actorSystem.Extensions.Register(diExtension);
            return actorSystem;
        }

        // ReSharper disable once InconsistentNaming
        public static IDependencyResolver DI(this ActorSystem system) => system.Extensions.Get<DIExtension>()!.Resolver;
    }
}
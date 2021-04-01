// -----------------------------------------------------------------------
// <copyright file="DIExtension.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using JetBrains.Annotations;
using Proto.Extensions;

namespace Proto.DependencyInjection
{
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
            DependencyResolver? dependencyResolver = new(serviceProvider);
            DIExtension? diExtension = new(dependencyResolver);
            actorSystem.Extensions.Register(diExtension);
            return actorSystem;
        }

        // ReSharper disable once InconsistentNaming
        public static IDependencyResolver DI(this ActorSystem system) => system.Extensions.Get<DIExtension>()!.Resolver;
    }
}

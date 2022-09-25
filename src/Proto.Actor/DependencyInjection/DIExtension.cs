// -----------------------------------------------------------------------
// <copyright file="DIExtension.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using JetBrains.Annotations;
using Proto.Extensions;

namespace Proto.DependencyInjection;

[PublicAPI]
public class DIExtension : IActorSystemExtension<DIExtension>
{
    public DIExtension(IDependencyResolver resolver)
    {
        Resolver = resolver;
    }

    public IDependencyResolver Resolver { get; }
}

[PublicAPI]
public static class Extensions
{
    /// <summary>
    ///     Adds the DI extension to the actor system, that helps to create Props based on the DI container.
    /// </summary>
    /// <param name="actorSystem"></param>
    /// <param name="serviceProvider">Service provider to use to resolve actors</param>
    /// <returns></returns>
    public static ActorSystem WithServiceProvider(this ActorSystem actorSystem, IServiceProvider serviceProvider)
    {
        var dependencyResolver = new DependencyResolver(serviceProvider);
        var diExtension = new DIExtension(dependencyResolver);
        actorSystem.Extensions.Register(diExtension);

        return actorSystem;
    }

    /// <summary>
    ///     Access the <see cref="IDependencyResolver" /> from the DI extension. Requires that the actor system was configured
    ///     with <see cref="WithServiceProvider" />.
    /// </summary>
    /// <param name="system"></param>
    /// <returns></returns>
    // ReSharper disable once InconsistentNaming
    public static IDependencyResolver DI(this ActorSystem system) =>
        system.Extensions.GetRequired<DIExtension>().Resolver;
}
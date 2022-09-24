// -----------------------------------------------------------------------
// <copyright file="DependencyResolver.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Proto.DependencyInjection;

public class DependencyResolver : IDependencyResolver
{
    private static readonly ILogger Logger = Log.CreateLogger<DependencyResolver>();
    private readonly IServiceProvider _services;

    public DependencyResolver(IServiceProvider services)
    {
        _services = services;
    }

    public Props PropsFor<TActor>(params object[] args) where TActor : IActor
    {
        return Props.FromProducer(() =>
            {
                var actorType = typeof(TActor);

                try
                {
                    return (IActor)ActivatorUtilities.CreateInstance(_services, actorType, args);
                }
                catch (Exception x)
                {
                    Logger.LogError(x, "DependencyResolved Failed resolving Props for actor type {ActorType}",
                        actorType.Name);

                    throw;
                }
            }
        );
    }

    public Props PropsFor<TActor>() where TActor : IActor
    {
        return PropsFor(typeof(TActor));
    }

    public Props PropsFor(Type actorType)
    {
        return Props.FromProducer(() =>
            {
                try
                {
                    return (IActor)_services.GetRequiredService(actorType);
                }
                catch (Exception x)
                {
                    Logger.LogError(x, "DependencyResolved Failed resolving Props for actor type {ActorType}",
                        actorType.Name);

                    throw;
                }
            }
        );
    }
}
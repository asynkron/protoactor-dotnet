// -----------------------------------------------------------------------
// <copyright file="IDependencyResolver.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;

namespace Proto.DependencyInjection
{
    public interface IDependencyResolver
    {
        Props PropsFor<TActor>() where TActor : IActor;

        Props PropsFor(Type actorType);
    }
}
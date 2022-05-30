// -----------------------------------------------------------------------
// <copyright file="IDependencyResolver.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;

namespace Proto.DependencyInjection;

public interface IDependencyResolver
{
    Props PropsForArgs<TActor>(params object[] args) where TActor : IActor;
    
    Props PropsFor<TActor>() where TActor : IActor;

    Props PropsFor(Type actorType);
}
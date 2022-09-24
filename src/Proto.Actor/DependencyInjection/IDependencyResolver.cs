// -----------------------------------------------------------------------
// <copyright file="IDependencyResolver.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;

namespace Proto.DependencyInjection;

/// <summary>
///     Utility to create Props based on <see cref="IServiceProvider" />
/// </summary>
public interface IDependencyResolver
{
    /// <summary>
    ///     Creates Props that resolve the actor by type, with possibility to supply additional constructor arguments
    /// </summary>
    /// <param name="args">Additional constructor arguments</param>
    /// <returns></returns>
    Props PropsFor<TActor>(params object[] args) where TActor : IActor;

    /// <summary>
    ///     Creates Props that resolve the actor by type
    /// </summary>
    /// <returns></returns>
    Props PropsFor<TActor>() where TActor : IActor;

    /// <summary>
    ///     Creates Props that resolve the actor by type
    /// </summary>
    /// <param name="actorType"></param>
    /// <returns></returns>
    Props PropsFor(Type actorType);
}
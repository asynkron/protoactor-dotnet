// -----------------------------------------------------------------------
// <copyright file="IActorSystemExtension.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Threading;

namespace Proto.Extensions;

/// <summary>
///     Marks a class as an actor system extension
/// </summary>
public interface IActorSystemExtension
{
    private static int _nextId;

    internal static int GetNextId() => Interlocked.Increment(ref _nextId);
}

/// <summary>
///     Marks a class as an actor system extension
/// </summary>
public interface IActorSystemExtension<T> : IActorSystemExtension where T : IActorSystemExtension
{
    public static int Id = GetNextId();
}
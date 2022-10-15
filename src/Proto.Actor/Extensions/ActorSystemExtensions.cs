// -----------------------------------------------------------------------
// <copyright file="ActorSystemExtensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace Proto.Extensions;

/// <summary>
///     Contains extensions for the actor system. Examples: Cluster, PubSub, etc.
/// </summary>
public class ActorSystemExtensions
{
    private readonly ActorSystem _actorSystem;
    private readonly object _lockObject = new();
    private IActorSystemExtension[] _extensions = new IActorSystemExtension[10];

    public ActorSystemExtensions(ActorSystem actorSystem)
    {
        _actorSystem = actorSystem;
    }

    /// <summary>
    ///     Gets the extension by the given type.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public T? Get<T>() where T : IActorSystemExtension
    {
        var id = IActorSystemExtension<T>.Id;

        return (T)_extensions[id];
    }

    /// <summary>
    ///     Gets the extension by the given type or throws if not found.
    /// </summary>
    /// <param name="notFoundMessage">Message to put on the exception</param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    public T GetRequired<T>(string? notFoundMessage = null) where T : IActorSystemExtension
    {
        var id = IActorSystemExtension<T>.Id;
        var res = (T)_extensions[id];

        if (res is null)
        {
            notFoundMessage ??= $"Key not found {typeof(T).Name}";

            throw new NotSupportedException(notFoundMessage);
        }

        return res;
    }

    /// <summary>
    ///     Registers a new extension by its type.
    /// </summary>
    /// <param name="extension">Extension to register</param>
    /// <typeparam name="T"></typeparam>
    public void Register<T>(IActorSystemExtension<T> extension) where T : IActorSystemExtension
    {
        lock (_lockObject)
        {
            var id = IActorSystemExtension<T>.Id;

            if (id >= _extensions.Length)
            {
                var newSize = id * 2; //double size when growing
                Array.Resize(ref _extensions, newSize);
            }

            _extensions[id] = extension;
            _actorSystem.Diagnostics.RegisterEvent("ActorSystem", $"Extension enabled {typeof(T).Name}");
        }
    }

    public IEnumerable<IActorSystemExtension> GetAll()
    {
        foreach (var e in _extensions)
        {
            if (e == null)
                continue;

            yield return e;
        }
    }
}
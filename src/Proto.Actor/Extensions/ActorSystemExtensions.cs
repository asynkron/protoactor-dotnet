// -----------------------------------------------------------------------
// <copyright file="ActorSystemExtensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace Proto.Extensions
{
    public class ActorSystemExtensions
    {
        private readonly ActorSystem _actorSystem;
        private readonly object _lockObject = new();
        private IActorSystemExtension[] _extensions = new IActorSystemExtension[10];

        public ActorSystemExtensions(ActorSystem actorSystem) => _actorSystem = actorSystem;

        public T? Get<T>() where T : IActorSystemExtension
        {
            var id = IActorSystemExtension<T>.Id;
            return (T) _extensions[id];
        }
        
        public T GetRequired<T>(string? notFoundMessage=null) where T : IActorSystemExtension
        {
            var id = IActorSystemExtension<T>.Id;
            var res =  (T) _extensions[id];

            if (res is null)
            {
                notFoundMessage ??= $"Key not found {typeof(T).Name}";
                throw new NotSupportedException(notFoundMessage);
            }
            return res;
        }

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
            }
        }
    }
}
// -----------------------------------------------------------------------
// <copyright file="ActorSystemExtensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;

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
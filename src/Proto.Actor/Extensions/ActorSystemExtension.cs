// -----------------------------------------------------------------------
// <copyright file="IActorSystemExtension.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Proto.Extensions
{
    public interface IActorSystemExtension
    {
        private static int nextId;

        internal static int GetNextId() => Interlocked.Increment(ref nextId);
    }

    // ReSharper disable once UnusedTypeParameter
    public abstract class ActorSystemExtension<T> : IActorSystemExtension where T : IActorSystemExtension
    {
        public static int Id = IActorSystemExtension.GetNextId();
        public ActorSystem System { get; }

        protected ActorSystemExtension(ActorSystem system)
        {
            System = system;
        }
        
        public virtual Task Started => Task.CompletedTask;
    }

    public abstract class StartableActorSystemExtension<T> : ActorSystemExtension<T> where T : IActorSystemExtension
    {
        private TaskCompletionSource<object> Source { get; } = new();

        public virtual Task DependenciesStarted( )
        {
            return Task.CompletedTask;
        }
        
        protected StartableActorSystemExtension([NotNull] ActorSystem system) : base(system)
        {
        }

        public override Task Started => Source.Task;
        
        public void Start()
        {
            Source.SetResult(new object());
        }
    }
}
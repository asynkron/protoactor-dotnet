// -----------------------------------------------------------------------
// <copyright file="IActorSystemExtension.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
    }

    public abstract class StartableActorSystemExtension<T> : ActorSystemExtension<T> where T : IActorSystemExtension
    {
        private IList<Task> _dependencies = new List<Task>();
        protected void AddDependency<TDep>() where TDep : StartableActorSystemExtension<TDep>
        {
            var task = System.Extensions.Get<TDep>()!.Started;
            _dependencies.Add(task);
        }
        private TaskCompletionSource<object> Source { get; } = new();

        public Task DependenciesStarted( ) => Task.WhenAll(_dependencies);

        protected StartableActorSystemExtension([NotNull] ActorSystem system) : base(system)
        {
        }

        public Task Started => Source.Task;
        
        public void Start()
        {
            Source.SetResult(new object());
        }
    }
}
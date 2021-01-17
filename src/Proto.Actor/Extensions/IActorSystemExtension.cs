// -----------------------------------------------------------------------
// <copyright file="IActorSystemExtension.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Proto.Extensions
{
    public interface IActorSystemExtension
    {
        private static int nextId;

        internal static int GetNextId() => Interlocked.Increment(ref nextId);

        Type[] GetDependencies() => Array.Empty<Type>();
    }

    // ReSharper disable once UnusedTypeParameter
    public interface IActorSystemExtension<T> : IActorSystemExtension where T : IActorSystemExtension
    {
        // ReSharper disable once StaticMemberInGenericType
        public static readonly int Id = GetNextId();
    }

    public abstract class ActorSystemExtension<T> : IActorSystemExtension<T> where T : IActorSystemExtension
    {
        private List<Type> _dependencies = new();

        public void AddDependency<TDep>() => _dependencies.Add(typeof(TDep));

        public Type[] GetDependencies() => _dependencies.ToArray();
    }
}
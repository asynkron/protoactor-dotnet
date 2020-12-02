// -----------------------------------------------------------------------
// <copyright file="IActorSystemExtension.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading;

namespace Proto.Extensions
{
    public interface IActorSystemExtension
    {
        private static int _nextId;

        internal static int GetNextId() => Interlocked.Increment(ref _nextId);
    }

    public interface IActorSystemExtension<T> : IActorSystemExtension where T : IActorSystemExtension
    {
        public static int Id = GetNextId();
    }
}
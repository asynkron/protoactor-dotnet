// -----------------------------------------------------------------------
// <copyright file="ISystemContext.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
namespace Proto
{
    public interface ISystemContext
    {
        /// <summary>
        ///     Gets the actor system this actor was spawned in.
        /// </summary>
        public ActorSystem System { get; }
    }
}
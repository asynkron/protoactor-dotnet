// -----------------------------------------------------------------------
// <copyright file="Extensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
namespace Proto.Remote
{
    public static class ActorSystemExtensions
    {
        public static Serialization Serialization(this ActorSystem system) => system.Extensions.Get<Serialization>();
    }
}
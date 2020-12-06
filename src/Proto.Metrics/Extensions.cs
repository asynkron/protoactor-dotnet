// -----------------------------------------------------------------------
// <copyright file="Extensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using JetBrains.Annotations;

namespace Proto.Metrics
{
    [PublicAPI]
    public static class Extensions
    {
        public static ActorSystem WithMetrics(this ActorSystem system)
        {
            return system; 
        }

        public static Metrics Metrics(this ActorSystem system)
        {
            return system.Extensions.Get<Metrics>();
        }
    }
}
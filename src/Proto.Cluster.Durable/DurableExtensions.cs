// -----------------------------------------------------------------------
// <copyright file="DurableExtensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using Proto.Cluster.Durable.FileSystem;

namespace Proto.Cluster.Durable
{
    public static class DurableExtensions
    {
        public static DurablePlugin DurableFunctions(this IContext self) => self.System.Extensions.Get<DurablePlugin>();

        public static DurablePlugin DurableFunctions(this Cluster self) => self.System.Extensions.Get<DurablePlugin>();

        public static ActorSystem WithDurableFunctions(this ActorSystem system, IDurablePersistence durablePersistence)
        {
            var p = new DurablePlugin(system.Cluster(), durablePersistence);
            system.Extensions.Register(p);
            return system;
        }
    }
}
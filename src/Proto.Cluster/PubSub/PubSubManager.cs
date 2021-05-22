// -----------------------------------------------------------------------
// <copyright file="PubSubManager.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading.Tasks;

namespace Proto.Cluster.PubSub
{
    public class PubSubManager
    {
        public const string PubSubDeliveryName = "pubsub-delivery";
        private readonly Cluster _cluster;
        private PID? _pid;

        public PubSubManager(Cluster cluster) => _cluster = cluster;

        public Task StartAsync()
        {
            var props = Props.FromProducer(() => new PubSubMemberDeliveryActor());
            _pid = _cluster.System.Root.SpawnNamed(props, PubSubDeliveryName);

            return Task.CompletedTask;
        }
    }
}
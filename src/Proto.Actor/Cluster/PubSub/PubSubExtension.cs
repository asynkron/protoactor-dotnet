// -----------------------------------------------------------------------
// <copyright file="PubSubManager.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Proto.Extensions;

namespace Proto.Cluster.PubSub;

public class PubSubExtension : IActorSystemExtension<PubSubExtension>
{
    public const string PubSubDeliveryName = "$pubsub-delivery";
    private readonly Cluster _cluster;

    public PubSubExtension(Cluster cluster)
    {
        _cluster = cluster;
        _cluster.System.Extensions.Register(this);
    }

    public Task StartAsync()
    {
        var props = Props.FromProducer(() =>
            new PubSubMemberDeliveryActor(_cluster.Config.PubSubConfig.SubscriberTimeout));

        _cluster.System.Root.SpawnNamed(props, PubSubDeliveryName);

        return Task.CompletedTask;
    }
}
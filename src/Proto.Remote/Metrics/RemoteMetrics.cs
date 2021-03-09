// -----------------------------------------------------------------------
// <copyright file="RemoteMetrics.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using Proto.Metrics;

namespace Proto.Remote.Metrics
{
    public class RemoteMetrics
    {
        public RemoteMetrics(Proto.Metrics.Metrics metrics)
        {
            const string prefix = "proto_remote_";
            RemoteSerializedMessageCount = metrics.CreateCount(prefix + nameof(RemoteSerializedMessageCount), new string[] { });
            RemoteDeserializedMessageCount = metrics.CreateCount(prefix + nameof(RemoteDeserializedMessageCount), new string[] { });
            RemoteKindCount = metrics.CreateCount(prefix + nameof(RemoteKindCount), new string[] { });
            RemoteActorSpawnCount = metrics.CreateCount(prefix + nameof(RemoteActorSpawnCount), new string[] { });
            RemoteEndpointConnectedCount = metrics.CreateCount(prefix + nameof(RemoteEndpointConnectedCount), new string[] { });
            RemoteEndpointDisconnectedCount = metrics.CreateCount(prefix + nameof(RemoteEndpointDisconnectedCount), new string[] { });
        }

        public readonly ICountMetric RemoteSerializedMessageCount;
        public readonly ICountMetric RemoteDeserializedMessageCount;
        
        public readonly ICountMetric RemoteKindCount;
        public readonly ICountMetric RemoteActorSpawnCount;
        
        public readonly ICountMetric RemoteEndpointConnectedCount;
        public readonly ICountMetric RemoteEndpointDisconnectedCount;
    }
}
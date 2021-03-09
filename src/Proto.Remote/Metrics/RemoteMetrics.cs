// -----------------------------------------------------------------------
// <copyright file="RemoteMetrics.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using Proto.Metrics;
using ICountMetric = Ubiquitous.Metrics.ICountMetric;
using Ubiquitous.Metrics;
namespace Proto.Remote.Metrics
{
    public class RemoteMetrics
    {
        public RemoteMetrics(ProtoMetrics metrics)
        {
            const string prefix = "proto_remote_";
            RemoteSerializedMessageCount = metrics.CreateCount(prefix + nameof(RemoteSerializedMessageCount), "");
            RemoteDeserializedMessageCount = metrics.CreateCount(prefix + nameof(RemoteDeserializedMessageCount), "");
            RemoteKindCount = metrics.CreateCount(prefix + nameof(RemoteKindCount), "");
            RemoteActorSpawnCount = metrics.CreateCount(prefix + nameof(RemoteActorSpawnCount), "");
            RemoteEndpointConnectedCount = metrics.CreateCount(prefix + nameof(RemoteEndpointConnectedCount), "");
            RemoteEndpointDisconnectedCount = metrics.CreateCount(prefix + nameof(RemoteEndpointDisconnectedCount), "");
        }

        public readonly ICountMetric RemoteSerializedMessageCount;   //done
        public readonly ICountMetric RemoteDeserializedMessageCount; //done

        public readonly ICountMetric RemoteKindCount;
        public readonly ICountMetric RemoteActorSpawnCount; //done

        public readonly ICountMetric RemoteEndpointConnectedCount;    //done
        public readonly ICountMetric RemoteEndpointDisconnectedCount; //done
    }
}
// -----------------------------------------------------------------------
// <copyright file="RemoteMetrics.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using Proto.Metrics;
using ICountMetric = Ubiquitous.Metrics.ICountMetric;
using Ubiquitous.Metrics;
using Ubiquitous.Metrics.Labels;

namespace Proto.Remote.Metrics
{
    public class RemoteMetrics
    {
        public RemoteMetrics(ProtoMetrics metrics)
        {
            const string prefix = "proto_remote_";
            RemoteSerializedMessageCount = metrics.CreateCount(prefix + nameof(RemoteSerializedMessageCount), "", "messagetype");
            RemoteDeserializedMessageCount = metrics.CreateCount(prefix + nameof(RemoteDeserializedMessageCount), "", "messagetype");
            RemoteKindCount = metrics.CreateCount(prefix + nameof(RemoteKindCount), "");
            RemoteActorSpawnCount = metrics.CreateCount(prefix + nameof(RemoteActorSpawnCount), "");
            RemoteEndpointConnectedCount = metrics.CreateCount(prefix + nameof(RemoteEndpointConnectedCount), "", "address");
            RemoteEndpointDisconnectedCount = metrics.CreateCount(prefix + nameof(RemoteEndpointDisconnectedCount), "", "address");
        }

        public readonly ICountMetric RemoteSerializedMessageCount;   //done
        public readonly ICountMetric RemoteDeserializedMessageCount; //done

        public readonly ICountMetric RemoteKindCount;
        public readonly ICountMetric RemoteActorSpawnCount; //done

        public readonly ICountMetric RemoteEndpointConnectedCount;    //done
        public readonly ICountMetric RemoteEndpointDisconnectedCount; //done
    }
}
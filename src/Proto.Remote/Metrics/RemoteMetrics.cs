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
            RemoteSerializedMessageCount = metrics.CreateCount("protoremote_message_serialize_count", "","messagetype");
            RemoteDeserializedMessageCount = metrics.CreateCount("protoremote_message_deserialize_count", "", "messagetype");
            RemoteKindCount = metrics.CreateCount("protoremote_kind_count", "");
            RemoteActorSpawnCount = metrics.CreateCount("protoremote_spawn_count", "");
            RemoteEndpointConnectedCount = metrics.CreateCount("protoremote_endpoint_connected_count", "", "address");
            RemoteEndpointDisconnectedCount = metrics.CreateCount("protoremote_endpoint_disconnected_count", "", "address");
        }

        public readonly ICountMetric RemoteSerializedMessageCount;   //done
        public readonly ICountMetric RemoteDeserializedMessageCount; //done

        public readonly ICountMetric RemoteKindCount;
        public readonly ICountMetric RemoteActorSpawnCount; //done

        public readonly ICountMetric RemoteEndpointConnectedCount;    //done
        public readonly ICountMetric RemoteEndpointDisconnectedCount; //done
    }
}
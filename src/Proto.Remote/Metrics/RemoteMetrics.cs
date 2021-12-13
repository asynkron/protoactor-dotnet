// -----------------------------------------------------------------------
// <copyright file="RemoteMetrics.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Diagnostics.Metrics;
using Proto.Metrics;

namespace Proto.Remote.Metrics
{
    public class RemoteMetrics
    {
        public readonly Counter<long> RemoteActorSpawnCount;
        public readonly Counter<long> RemoteDeserializedMessageCount;

        public readonly Counter<long> RemoteEndpointConnectedCount;
        public readonly Counter<long> RemoteEndpointDisconnectedCount;

        public readonly Counter<long> RemoteSerializedMessageCount;

        public RemoteMetrics(ProtoMetrics metrics)
        {
            RemoteSerializedMessageCount =
                metrics.CreateCounter<long>("protoremote_message_serialize_count", description: "Number of serialized messages");
            RemoteDeserializedMessageCount =
                metrics.CreateCounter<long>("protoremote_message_deserialize_count", description: "Number of deserialized messages");
            RemoteActorSpawnCount = metrics.CreateCounter<long>("protoremote_spawn_count", description: "Number of actors spawned over remote");
            RemoteEndpointConnectedCount =
                metrics.CreateCounter<long>("protoremote_endpoint_connected_count", description: "Number of endpoint connects");
            RemoteEndpointDisconnectedCount =
                metrics.CreateCounter<long>("protoremote_endpoint_disconnected_count", description: "Number of endpoint disconnects");
        }
    }
}
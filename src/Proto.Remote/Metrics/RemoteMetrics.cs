// -----------------------------------------------------------------------
// <copyright file="RemoteMetrics.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Diagnostics.Metrics;
using Proto.Metrics;

namespace Proto.Remote.Metrics
{
    public static class RemoteMetrics
    {
        public static readonly Counter<long> RemoteActorSpawnCount =
            ProtoMetrics.Meter.CreateCounter<long>("protoremote_spawn_count", description: "Number of actors spawned over remote");

        public static readonly Counter<long> RemoteSerializedMessageCount =
            ProtoMetrics.Meter.CreateCounter<long>("protoremote_message_serialize_count", description: "Number of serialized messages");

        public static readonly Counter<long> RemoteDeserializedMessageCount =
            ProtoMetrics.Meter.CreateCounter<long>("protoremote_message_deserialize_count", description: "Number of deserialized messages");

        public static readonly Counter<long> RemoteEndpointConnectedCount =
            ProtoMetrics.Meter.CreateCounter<long>("protoremote_endpoint_connected_count", description: "Number of endpoint connects");

        public static readonly Counter<long> RemoteEndpointDisconnectedCount =
            ProtoMetrics.Meter.CreateCounter<long>("protoremote_endpoint_disconnected_count", description: "Number of endpoint disconnects");
    }
}
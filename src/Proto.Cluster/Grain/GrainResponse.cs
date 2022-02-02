// -----------------------------------------------------------------------
// <copyright file="GrainResponse.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using Google.Protobuf;
using Proto.Remote;

namespace Proto.Cluster
{
    public partial class GrainResponse : IRootSerialized
    {
        public IRootSerializable Deserialize(ActorSystem system)
        {
            if (MessageData.IsEmpty) return new GrainResponseMessage(null);

            var ser = system.Serialization();
            var message = ser.Deserialize(MessageTypeName, MessageData, Serialization.SERIALIZER_ID_PROTOBUF);
            return new GrainResponseMessage((IMessage) message);
        }
    }
}
// -----------------------------------------------------------------------
// <copyright file="GrainRequest.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using Google.Protobuf;
using Proto.Remote;

namespace Proto.Cluster
{
    public partial class GrainRequest : IRootSerialized
    {
        //deserialize into the in-process message type that the grain actors understands
        public IRootSerializable Deserialize(ActorSystem system)
        {
            var ser = system.Serialization();
            var message = ser.Deserialize(MessageTypeName, MessageData, Serialization.SERIALIZER_ID_PROTOBUF);
            return new GrainRequestMessage(MethodIndex, (IMessage)message);
        }
    }
}
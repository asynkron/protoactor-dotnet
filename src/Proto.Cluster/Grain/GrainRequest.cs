// -----------------------------------------------------------------------
// <copyright file="GrainRequest.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using Google.Protobuf;
using Proto.Remote;

namespace Proto.Cluster;

public partial class GrainRequest : IRootSerialized
{
    //deserialize into the in-process message type that the grain actors understands
    public IRootSerializable Deserialize(ActorSystem system)
    {
        //special case for null messages
        if (MessageData.IsEmpty && string.IsNullOrEmpty(MessageTypeName))
        {
            return new GrainRequestMessage(MethodIndex, null);
        }

        var ser = system.Serialization();
        var message = ser.Deserialize(MessageTypeName, MessageData.Span, Serialization.SERIALIZER_ID_PROTOBUF);
            
        return new GrainRequestMessage(MethodIndex, (IMessage) message);
    }
}
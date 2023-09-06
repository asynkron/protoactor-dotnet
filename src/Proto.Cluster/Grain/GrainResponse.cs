// -----------------------------------------------------------------------
// <copyright file="GrainResponse.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using Google.Protobuf;
using Proto.Remote;

namespace Proto.Cluster;

/// <summary>
///     Transport-level representation of <see cref="GrainResponseMessage" />
/// </summary>
public partial class GrainResponse : IRootSerialized
{
    //deserialize into the in-process message type that the grain actors understands
    public IRootSerializable Deserialize(ActorSystem system)
    {
        //special case for null messages
        if (MessageData.IsEmpty && string.IsNullOrEmpty(MessageTypeName))
        {
            return new GrainResponseMessage(null);
        }

        var ser = system.Serialization();
        var message = ser.Deserialize(MessageTypeName, MessageData, Serialization.SERIALIZER_ID_PROTOBUF);

        return new GrainResponseMessage((IMessage)message);
    }
}
// -----------------------------------------------------------------------
// <copyright file="GrainResponseMessage.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using Google.Protobuf;
using Proto.Remote;

namespace Proto.Cluster;

public record GrainResponseMessage(IMessage? ResponseMessage) : IRootSerializable
{
    public IRootSerialized Serialize(ActorSystem system)
    {
        if (ResponseMessage is null) return new GrainResponse();

        var ser = system.Serialization();
        var (data, typeName, serializerId) = ser.Serialize(ResponseMessage);
#if DEBUG
            if (serializerId != Serialization.SERIALIZER_ID_PROTOBUF)
                throw new Exception($"Grains must use ProtoBuf types: {ResponseMessage.GetType().FullName}");
#endif
        return new GrainResponse
        {
            MessageData = ByteString.CopyFrom(data),
            MessageTypeName = typeName,
        };
    }
}
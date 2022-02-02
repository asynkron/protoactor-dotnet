// -----------------------------------------------------------------------
// <copyright file="Extensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using Google.Protobuf;
using Proto.Remote;

namespace Proto.Cluster
{
    public record GrainRequestMessage(int MethodIndex, IMessage? RequestMessage) : IRootSerializable
    {
        //serialize into the on-the-wire format
        public IRootSerialized Serialize(ActorSystem system)
        {
            if (RequestMessage is null) return new GrainRequest {MethodIndex = MethodIndex};

            var ser = system.Serialization();
            var (data, typeName, serializerId) = ser.Serialize(RequestMessage);
#if DEBUG
            if (serializerId != Serialization.SERIALIZER_ID_PROTOBUF)
                throw new Exception($"Grains must use ProtoBuf types: {RequestMessage.GetType().FullName}");
#endif
            
            return new GrainRequest
            {
                MethodIndex = MethodIndex,
                MessageData = data,
                MessageTypeName = typeName,
            };
        }
    }
}
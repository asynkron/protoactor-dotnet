// -----------------------------------------------------------------------
// <copyright file="GrainResponseMessage.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using Google.Protobuf;
using Proto.Remote;

namespace Proto.Cluster
{
    public record GrainResponseMessage(IMessage ResponseMessage)  : IRootSerializable
    {
        public IRootSerialized Serialize(ActorSystem system)
        {
            var ser = system.Serialization();
            var typeName = ser.GetTypeName(ResponseMessage, ser.DefaultSerializerId);
            var data = ser.Serialize(ResponseMessage, ser.DefaultSerializerId);
            return new GrainResponse
            {
                MessageData = data,
                MessageTypeName = typeName,
            };
        }
    }

}
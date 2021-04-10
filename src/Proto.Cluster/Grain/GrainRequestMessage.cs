// -----------------------------------------------------------------------
// <copyright file="Extensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using Google.Protobuf;
using Proto.Remote;

namespace Proto.Cluster
{
    public record GrainRequestMessage(int MethodIndex, IMessage RequestMessage) : IRootSerializable
    {

        //serialize into the on-the-wire format
        public IRootSerialized Serialize(ActorSystem system)
        {
            var ser = system.Serialization();
            var typeName = ser.GetTypeName(RequestMessage, ser.DefaultSerializerId);
            var data = ser.Serialize(RequestMessage, ser.DefaultSerializerId);
            return new GrainRequest
            {
                MethodIndex = MethodIndex,
                MessageData = data,
                MessageTypeName = typeName,
            };
        }
    }
}
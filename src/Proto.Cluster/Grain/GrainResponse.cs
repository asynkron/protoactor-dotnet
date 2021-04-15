// -----------------------------------------------------------------------
// <copyright file="GrainResponse.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using Google.Protobuf;
using Proto.Remote;

namespace Proto.Cluster
{
    public partial class GrainResponse : IRootSerialized
    {
        public IRootSerializable Deserialize(ActorSystem system)
        {
            var ser = system.Serialization();
            var message = ser.Deserialize(MessageTypeName, MessageData, ser.DefaultSerializerId);
            return new GrainResponseMessage((IMessage)message);
        }
    }
}
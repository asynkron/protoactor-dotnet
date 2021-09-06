// -----------------------------------------------------------------------
// <copyright file="Messages.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using Google.Protobuf;
using Proto.Remote;

namespace Messages
{
    public partial class Ping : ICachedSerialization
    {
        public (ByteString bytes, string typename, int serializerId, bool boolHasData) SerializerData { get; set; }
    }
    
    public partial class Pong : ICachedSerialization
    {
        public (ByteString bytes, string typename, int serializerId, bool boolHasData) SerializerData { get; set; }
    }
}
// -----------------------------------------------------------------------
// <copyright file="DefaultProtobufSerializer.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using Google.Protobuf;

namespace Proto.Remote
{
    internal sealed class DefaultProtobufSerializer : ProtobufSerializer
    {
        public DefaultProtobufSerializer(Serialization serialization) : base(serialization) { }
        protected override object Deserialize(ByteString bytes, string typeName) => throw new ArgumentOutOfRangeException($"No deserializer found for type {typeName}");
        protected override string GetTypeName(object message) => throw new ArgumentException("message must be of type IMessage", nameof(message));
        protected override ByteString Serialize(object obj) => throw new ArgumentException("obj must be of type IMessage", nameof(obj));
    }
}
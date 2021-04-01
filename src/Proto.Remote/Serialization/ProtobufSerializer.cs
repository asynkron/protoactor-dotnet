// -----------------------------------------------------------------------
// <copyright file="ProtobufSerializer.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using Google.Protobuf;

namespace Proto.Remote
{
    public class ProtobufSerializer : ISerializer
    {
        private readonly Serialization _serialization;

        public ProtobufSerializer(Serialization serialization) => _serialization = serialization;

        public ByteString Serialize(object obj)
        {
            IMessage? message = obj as IMessage;
            return message.ToByteString();
        }

        public object Deserialize(ByteString bytes, string typeName)
        {
            MessageParser? parser = _serialization.TypeLookup[typeName];
            IMessage? o = parser.ParseFrom(bytes);
            return o;
        }

        public string GetTypeName(object obj)
        {
            if (obj is IMessage message)
            {
                return message.Descriptor.FullName;
            }

            throw new ArgumentException("obj must be of type IMessage", nameof(obj));
        }
    }
}

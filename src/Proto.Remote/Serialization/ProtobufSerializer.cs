// -----------------------------------------------------------------------
// <copyright file="ProtobufSerializer.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using Google.Protobuf;

namespace Proto.Remote
{
    public abstract class ProtobufSerializer : ISerializer
    {
        protected readonly Serialization _serialization;
        public ProtobufSerializer(Serialization serialization) => _serialization = serialization;
        object ISerializer.Deserialize(ByteString bytes, string typeName)
        {
            if (_serialization.TypeLookup.TryGetValue(typeName, out var parser))
                return parser.ParseFrom(bytes);
            return Deserialize(bytes, typeName);
        }

        protected abstract object Deserialize(ByteString bytes, string typeName);

        string ISerializer.GetTypeName(object message)
        {
            if (message is IMessage protobufMessage && _serialization.TypeLookup.ContainsKey(protobufMessage.Descriptor.FullName))
                return protobufMessage.Descriptor.FullName;
            return GetTypeName(message);
        }
        protected abstract string GetTypeName(object message);

        ByteString ISerializer.Serialize(object obj)
        {
            if (obj is IMessage message && _serialization.TypeLookup.ContainsKey(message.Descriptor.FullName))
                return message.ToByteString();
            return Serialize(obj);
        }

        protected abstract ByteString Serialize(object obj);
    }
}
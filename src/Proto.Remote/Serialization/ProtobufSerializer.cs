// -----------------------------------------------------------------------
// <copyright file="ProtobufSerializer.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using Google.Protobuf;
using Microsoft.Extensions.Logging;

namespace Proto.Remote
{
    public class ProtobufSerializer : ISerializer
    {
        private readonly Serialization _serialization;

        public ProtobufSerializer(Serialization serialization) => _serialization = serialization;

        public ByteString Serialize(object obj)
        {
            var message = obj as IMessage;
            return message.ToByteString();
        }

        public object Deserialize(ByteString bytes, string typeName)
        {
            var parser = _serialization.TypeLookup[typeName];
            var o = parser.ParseFrom(bytes);
            return o;
        }

        public string GetTypeName(object obj)
        {
            if (obj is IMessage message)
                return message.Descriptor.FullName;

            throw new ArgumentException("obj must be of type IMessage", nameof(obj));
        }

        public bool CanSerialize(object obj)
        {
            if (obj is IMessage message)
            {
                if (_serialization.TypeLookup.ContainsKey(message.Descriptor.FullName))
                    return true;
                Log.CreateLogger<Serialization>().LogWarning("Descriptor for message type {descriptor} not registered", message.Descriptor.Name);
            }
            return false;
        }
    }
}
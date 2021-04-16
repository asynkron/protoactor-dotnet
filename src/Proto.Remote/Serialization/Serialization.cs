// -----------------------------------------------------------------------
//   <copyright file="Serialization.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Proto.Extensions;

namespace Proto.Remote
{
    public class Serialization : IActorSystemExtension<Serialization>
    {
        private readonly List<ISerializer> _serializers = new();
        public IReadOnlyDictionary<string, MessageParser> TypeLookup { get => _typeLookup; }
        private readonly Dictionary<string, MessageParser> _typeLookup = new();

        public Serialization()
        {
            RegisterFileDescriptor(Proto.ProtosReflection.Descriptor);
            RegisterFileDescriptor(ProtosReflection.Descriptor);
            RegisterSerializer(new DefaultProtobufSerializer(this), true);
            RegisterSerializer(new JsonSerializer(this));
        }

        public int DefaultSerializerId { get; set; }

        public void RegisterSerializer(ISerializer serializer, bool makeDefault = false)
        {
            _serializers.Add(serializer);
            if (makeDefault) DefaultSerializerId = _serializers.Count - 1;
        }

        public void RegisterSerializer(Func<Serialization, ProtobufSerializer> serializerProvider, bool makeDefault = false)
        {
            _serializers.Add(serializerProvider.Invoke(this));
            if (makeDefault) DefaultSerializerId = _serializers.Count - 1;
        }

        public void RegisterFileDescriptor(FileDescriptor fd)
        {
            foreach (var msg in fd.MessageTypes)
            {
                _typeLookup.Add(msg.FullName, msg.Parser);
            }
        }

        public ByteString Serialize(object message, int serializerId) => _serializers[serializerId].Serialize(message);

        public string GetTypeName(object message, int serializerId) => _serializers[serializerId].GetTypeName(message);

        public object Deserialize(string typeName, ByteString bytes, int serializerId) =>
            _serializers[serializerId].Deserialize(bytes, typeName);
    }
}
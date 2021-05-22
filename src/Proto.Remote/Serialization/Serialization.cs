// -----------------------------------------------------------------------
//   <copyright file="Serialization.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Proto.Extensions;

namespace Proto.Remote
{
    public class Serialization : IActorSystemExtension<Serialization>
    {
        struct SerializerItem
        {
            public int SerializerId;
            public int PriorityValue;
            public ISerializer Serializer;
        }

        private struct TypeSerializerItem
        {
            public ISerializer Serializer;
            public int SerializerId;
        }

        private List<SerializerItem> _serializers = new();
        private readonly ConcurrentDictionary<Type, TypeSerializerItem> _serializerLookup = new();
        internal readonly Dictionary<string, MessageParser> TypeLookup = new();

        public const int SERIALIZER_ID_PROTOBUF = 0;
        public const int SERIALIZER_ID_JSON = 1;

        public JsonSerializerOptions? JsonSerializerOptions { get; internal set; }

        public Serialization()
        {
            RegisterFileDescriptor(Proto.ProtosReflection.Descriptor);
            RegisterFileDescriptor(ProtosReflection.Descriptor);
            RegisterSerializer(
                SERIALIZER_ID_PROTOBUF,
                priority: 0,
                new ProtobufSerializer(this));
            RegisterSerializer(
                SERIALIZER_ID_JSON,
                priority: -1000,
                new JsonSerializer(this));
        }

        /// <summary>
        /// Registers a new serializer with this Serialization instance.
        /// SerializerId must correspond to the same serializer on all remote nodes (cluster members).
        /// ProtoBufSerializer's id is 0, JsonSerializer's is 1 by default.
        /// Priority defined in which order Serializers should be considered to be the serializer for a given type.
        /// ProtoBufSerializer has priority of 0, and JsonSerializer has priority of -1000.
        /// </summary>
        public void RegisterSerializer(
            int serializerId,
            int priority,
            ISerializer serializer)
        {
            if (_serializers.Any(v => v.SerializerId == serializerId))
                throw new Exception($"Already registered serializer id: {serializerId} = {_serializers[serializerId].GetType()}");

            _serializers.Add(new SerializerItem()
            {
                SerializerId = serializerId,
                PriorityValue = priority,
                Serializer = serializer,
            });
            // Sort by PriorityValue, from highest to lowest.
            _serializers = _serializers
                .OrderByDescending(v => v.PriorityValue)
                .ToList();
        }

        public void RegisterFileDescriptor(FileDescriptor fd)
        {
            foreach (var msg in fd.MessageTypes)
            {
                TypeLookup.Add(msg.FullName, msg.Parser);
            }
        }

        public (ByteString bytes, string typename, int serializerId) Serialize(object message)
        {
            var serializer = FindSerializerToUse(message);
            var typename = serializer.Serializer.GetTypeName(message);
            var serializerId = serializer.SerializerId;
            var bytes = serializer.Serializer.Serialize(message);
            return (bytes, typename, serializerId);
        }

        TypeSerializerItem FindSerializerToUse(object message)
        {
            var type = message.GetType();
            if (_serializerLookup.TryGetValue(type, out var serializer))
                return serializer;

            // Determine which serializer can serialize this object type.
            foreach (var serializerItem in _serializers)
            {
                if (!serializerItem.Serializer.CanSerialize(message)) continue;

                var item = new TypeSerializerItem
                {
                    Serializer = serializerItem.Serializer,
                    SerializerId = serializerItem.SerializerId,
                };
                _serializerLookup[type] = item;
                return item;
            }
            throw new Exception($"Couldn't find a serializer for {message.GetType()}");
        }

        public object Deserialize(string typeName, ByteString bytes, int serializerId)
        {
            foreach (var serializerItem in _serializers)
            {
                if (serializerItem.SerializerId == serializerId)
                    return serializerItem.Serializer.Deserialize(
                        bytes,
                        typeName);
            }

            throw new Exception($"Couldn't find serializerId: {serializerId} for typeName: {typeName}");
        }
    }
}
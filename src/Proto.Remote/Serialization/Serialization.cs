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
using Microsoft.Extensions.Logging;
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

        private TypeSerializerItem FindSerializerToUse(object message)
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

        public static MessageBatch BuildMessageBatch(IEnumerable<RemoteDeliver> remoteDeliverMessages, ActorSystem system, ILogger logger)
        {
            var envelopes = new List<MessageEnvelope>();
            var typeNames = new Dictionary<string, int>();
            var targetNames = new Dictionary<string, int>();
            var typeNameList = new List<string>();
            var targetNameList = new List<string>();

            foreach (var rd in remoteDeliverMessages)
            {
                var targetName = rd.Target.Id;

                if (!targetNames.TryGetValue(targetName, out var targetId))
                {
                    targetId = targetNames[targetName] = targetNames.Count;
                    targetNameList.Add(targetName);
                }

                var message = rd.Message;
                //if the message can be translated to a serialization representation, we do this here
                //this only apply to root level messages and never to nested child objects inside the message
                if (message is IRootSerializable deserialized) message = deserialized.Serialize(system);

                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if (message is null)
                {
                    logger.LogError("Null message passed to EndpointActor, ignoring message");
                    continue;
                }

                ByteString bytes;
                string typeName;
                int serializerId;

                try
                {
                    var cached = message as ICachedSerialization;

                    if (cached is {SerializerData: {boolHasData: true}})
                    {
                        (bytes, typeName, serializerId, _) = cached.SerializerData;
                    }
                    else
                    {
                        (bytes, typeName, serializerId) = system.Serialization().Serialize(message);

                        if (cached is not null)
                        {
                            cached.SerializerData = (bytes, typeName, serializerId, true);
                        }
                    }
                }
                catch (CodedOutputStream.OutOfSpaceException oom)
                {
                    logger.LogError(oom, "Message is too large {Message}", message.GetType().Name);
                    throw;
                }
                catch (Exception x)
                {
                    logger.LogError(x, "Serialization failed for message {Message}", message.GetType().Name);
                    throw;
                }
                
                if (!typeNames.TryGetValue(typeName, out var typeId))
                {
                    typeId = typeNames[typeName] = typeNames.Count;
                    typeNameList.Add(typeName);
                }

                MessageHeader? header = null;

                if (rd.Header is {Count: > 0})
                {
                    header = new MessageHeader();
                    header.HeaderData.Add(rd.Header.ToDictionary());
                }

                var envelope = new MessageEnvelope
                {
                    MessageData = bytes,
                    Sender = rd.Sender,
                    Target = targetId,
                    TypeId = typeId,
                    SerializerId = serializerId,
                    MessageHeader = header,
                    RequestId = rd.Target.RequestId
                };

                envelopes.Add(envelope);
            }

            var batch = new MessageBatch();
            batch.TargetNames.AddRange(targetNameList);
            batch.TypeNames.AddRange(typeNameList);
            batch.Envelopes.AddRange(envelopes);
            return batch;
        }
    }
}
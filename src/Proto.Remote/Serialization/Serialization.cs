// -----------------------------------------------------------------------
//   <copyright file="Serialization.cs" company="Asynkron AB">
//       Copyright (C) 2015-2022 Asynkron AB All rights reserved
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
using ActorMessages = Proto.ProtosReflection;
using RemoteMessages = Proto.Remote.ProtosReflection;
namespace Proto.Remote;

public class Serialization : IActorSystemExtension<Serialization>
{
    public const int SERIALIZER_ID_PROTOBUF = 0;
    public const int SERIALIZER_ID_JSON = 1;
    private readonly ConcurrentDictionary<Type, TypeSerializerItem> _serializerLookup = new();

    internal readonly
        ConcurrentDictionary<string, (ByteString bytes, string typename, object instance, int serializerId)> Cache =
            new();

    internal readonly Dictionary<string, MessageParser> TypeLookup = new();

    private List<SerializerItem> _serializers = new();

    public Serialization()
    {
        RegisterFileDescriptor(ActorMessages.Descriptor);
        RegisterFileDescriptor(RemoteMessages.Descriptor);

        RegisterSerializer(
            SERIALIZER_ID_PROTOBUF,
            0,
            new ProtobufSerializer(this));
        
        RegisterSerializer(
            SERIALIZER_ID_JSON,
            -1000,
            new JsonSerializer(this));
    }

    public JsonSerializerOptions? JsonSerializerOptions { get; internal set; }

    /// <summary>
    ///     Registers a new serializer with this Serialization instance.
    ///     SerializerId must correspond to the same serializer on all remote nodes (cluster members).
    ///     ProtoBufSerializer's id is 0, JsonSerializer's is 1 by default.
    ///     Priority defined in which order Serializers should be considered to be the serializer for a given type (highest
    ///     value takes precedence).
    ///     ProtoBufSerializer has priority of 0, and JsonSerializer has priority of -1000.
    /// </summary>
    public void RegisterSerializer(
        int serializerId,
        int priority,
        ISerializer serializer)
    {
        if (_serializers.Any(v => v.SerializerId == serializerId))
        {
            throw new Exception(
                $"Already registered serializer id: {serializerId} = {_serializers[serializerId].GetType()}");
        }

        _serializers.Add(new SerializerItem
        {
            SerializerId = serializerId,
            PriorityValue = priority,
            Serializer = serializer
        });

        // Sort by PriorityValue, from highest to lowest.
        _serializers = _serializers
            .OrderByDescending(v => v.PriorityValue)
            .ToList();
    }

    /// <summary>
    ///     Register file descriptor for protobuf messages
    /// </summary>
    /// <param name="fd"></param>
    public void RegisterFileDescriptor(FileDescriptor fd)
    {
        foreach (var msg in fd.MessageTypes)
        {
            if (!TypeLookup.ContainsKey(msg.FullName))
            {
                TypeLookup.Add(msg.FullName, msg.Parser);
            }
        }
    }

    /// <summary>
    ///     Serializes the message with a registered serializer.
    /// </summary>
    /// <param name="message">Message to serialize</param>
    /// <returns>A tuple of message bytes and type name and serializer id of the serializer used</returns>
    /// <exception cref="Exception">Throw if serializer id could not be found for specified type name</exception>
    public (ByteString bytes, string typename, int serializerId) Serialize(object message)
    {
        var serializer = FindSerializerToUse(message);
        var typename = serializer.Serializer.GetTypeName(message);

        if (message is ICachedSerialization && Cache.TryGetValue(typename, out var cached))
        {
            return (cached.bytes, typename, cached.serializerId);
        }

        var serializerId = serializer.SerializerId;
        var bytes = serializer.Serializer.Serialize(message);

        if (message is ICachedSerialization)
        {
            Cache.TryAdd(typename, (bytes, typename, message, serializerId));
        }

        return (bytes, typename, serializerId);
    }

    private TypeSerializerItem FindSerializerToUse(object message)
    {
        var type = message.GetType();

        if (_serializerLookup.TryGetValue(type, out var serializer))
        {
            return serializer;
        }

        // Determine which serializer can serialize this object type.
        foreach (var serializerItem in _serializers)
        {
            if (!serializerItem.Serializer.CanSerialize(message))
            {
                continue;
            }

            var item = new TypeSerializerItem
            {
                Serializer = serializerItem.Serializer,
                SerializerId = serializerItem.SerializerId
            };

            _serializerLookup[type] = item;

            return item;
        }

        throw new Exception($"Couldn't find a serializer for {message.GetType()}");
    }

    /// <summary>
    ///     Deserializes the message with a registered serializer.
    /// </summary>
    /// <param name="typeName">Type name coming from the serializer</param>
    /// <param name="bytes">message bytes</param>
    /// <param name="serializerId">Serializer id</param>
    /// <returns></returns>
    /// <exception cref="Exception">Throw if serializer id could not be found for specified type name</exception>
    public object Deserialize(string typeName, ByteString bytes, int serializerId)
    {
        if (Cache.TryGetValue(typeName, out var cachedMessage))
        {
            return cachedMessage.instance;
        }

        foreach (var serializerItem in _serializers)
        {
            if (serializerItem.SerializerId == serializerId)
            {
                var message = serializerItem.Serializer.Deserialize(
                    bytes,
                    typeName);

                if (message is ICachedSerialization)
                {
                    Cache.TryAdd(typeName, (bytes, typeName, message, serializerId));
                }

                return message;
            }
        }

        throw new Exception($"Couldn't find serializerId: {serializerId} for typeName: {typeName}");
    }

    private struct SerializerItem
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

    public void	 Init(ActorSystem system)
    {
        system.Diagnostics.RegisterObject("Serialization", "Protobuf Types", TypeLookup.Keys.ToArray());

        foreach (var s in _serializers)
        {
            system.Diagnostics.RegisterEvent("Serialization",
                $"Registered Serializer {s.SerializerId} {s.Serializer.GetType().Name}");
        }
    }
}
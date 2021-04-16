// -----------------------------------------------------------------------
// <copyright file="SystemTextJsonSerializer.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Concurrent;
using System.Text.Json;
using Google.Protobuf;

namespace Proto.Remote
{
    public sealed class SystemTextJsonSerializer : ProtobufSerializer
    {
        private readonly ConcurrentDictionary<string, Type> _jsonTypes = new ConcurrentDictionary<string, Type>();
        private readonly JsonSerializerOptions? _options;
        public SystemTextJsonSerializer(Serialization serialization, JsonSerializerOptions? options = null) : base(serialization)
        {
            _options = options;
        }
        protected override object Deserialize(ByteString bytes, string typeName)
        {
            var json = bytes.ToStringUtf8();
            return System.Text.Json.JsonSerializer.Deserialize(
                json,
                _jsonTypes.GetOrAdd(typeName, Type.GetType(typeName) ?? throw new Exception($"Unable to deserialize message with type {typeName}")),
                _options
                ) ?? throw new Exception($"Unable to deserialize message with type {typeName}");
        }
        protected override string GetTypeName(object obj)
        {
            return obj.GetType().AssemblyQualifiedName;
        }
        protected override ByteString Serialize(object obj)
        {
            return ByteString.CopyFromUtf8(System.Text.Json.JsonSerializer.Serialize(obj, _options));
        }
    }
}
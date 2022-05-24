// -----------------------------------------------------------------------
// <copyright file="JsonSerializer.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Concurrent;
using System.Text;
using Google.Protobuf;

namespace Proto.Remote;

public class JsonSerializer : ISerializer
{
    private readonly Serialization _serialization;
    private readonly ConcurrentDictionary<string, Type> _jsonTypes = new ConcurrentDictionary<string, Type>();

    public JsonSerializer(Serialization serialization) => _serialization = serialization;

    public ReadOnlySpan<byte>  Serialize(object obj)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(obj, _serialization.JsonSerializerOptions);
        return Encoding.UTF8.GetBytes(json);
    }

    public object Deserialize(ReadOnlySpan<byte> bytes, string typeName)
    {
        var json = Encoding.UTF8.GetString(bytes);
        var returnType = _jsonTypes.GetOrAdd(typeName, Type.GetType(typeName) ?? throw new Exception($"Type with the specified name {typeName} not found"));
        var message = System.Text.Json.JsonSerializer.Deserialize(json, returnType, _serialization.JsonSerializerOptions) ?? throw new Exception($"Unable to deserialize message with type {typeName}");
        return message;
    }

    public string GetTypeName(object obj)
    {
        return obj?.GetType()?.AssemblyQualifiedName ?? throw new ArgumentNullException(nameof(obj));
    }

    public bool CanSerialize(object obj) => true;
}
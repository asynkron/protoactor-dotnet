// -----------------------------------------------------------------------
// <copyright file="JsonSerializer.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using Google.Protobuf;

namespace Proto.Remote
{
    public class JsonSerializer : ISerializer
    {
        private readonly Serialization _serialization;

        public JsonSerializer(Serialization serialization) => _serialization = serialization;

        public ByteString Serialize(object obj)
        {
            if (obj is JsonMessage jsonMessage) return ByteString.CopyFromUtf8(jsonMessage.Json);

            var message = obj as IMessage;
            var json = JsonFormatter.Default.Format(message);
            return ByteString.CopyFromUtf8(json);
        }

        public object Deserialize(ByteString bytes, string typeName)
        {
            var json = bytes.ToStringUtf8();
            var parser = _serialization.TypeLookup[typeName];

            var o = parser.ParseJson(json);
            return o;
        }

        public string GetTypeName(object obj)
        {
            if (obj is JsonMessage jsonMessage)
                return jsonMessage.TypeName;

            if (obj is IMessage message)
                return message.Descriptor.File.Package + "." + message.Descriptor.Name;

            throw new ArgumentException("obj must be of type IMessage", nameof(obj));
        }
    }
}
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
            if (obj is JsonMessage jsonMessage)
            {
                return ByteString.CopyFromUtf8(jsonMessage.Json);
            }

            IMessage? message = obj as IMessage;
            string? json = JsonFormatter.Default.Format(message);
            return ByteString.CopyFromUtf8(json);
        }

        public object Deserialize(ByteString bytes, string typeName)
        {
            string? json = bytes.ToStringUtf8();
            MessageParser? parser = _serialization.TypeLookup[typeName];

            IMessage? o = parser.ParseJson(json);
            return o;
        }

        public string GetTypeName(object obj)
        {
            if (obj is JsonMessage jsonMessage)
            {
                return jsonMessage.TypeName;
            }

            if (obj is IMessage message)
            {
                return message.Descriptor.FullName;
            }

            throw new ArgumentException("obj must be of type IMessage", nameof(obj));
        }
    }
}

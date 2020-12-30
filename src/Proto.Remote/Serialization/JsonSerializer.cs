// -----------------------------------------------------------------------
// <copyright file="JsonSerializer.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Text;
using Google.Protobuf;

namespace Proto.Remote
{
    public class JsonSerializer : ISerializer
    {
        private readonly Serialization _serialization;

        public JsonSerializer(Serialization serialization)
        {
            _serialization = serialization;
        }

        public ReadOnlySpan<byte> Serialize(object obj)
        {
            if (obj is JsonMessage jsonMessage)
            {
                return Encoding.UTF8.GetBytes(jsonMessage.Json);
            }

            var message = obj as IMessage;
            var json = JsonFormatter.Default.Format(message);
            return Encoding.UTF8.GetBytes(json);
        }

        public object Deserialize(ReadOnlySpan<byte> bytes, string typeName)
        {
            var json = Encoding.UTF8.GetString(bytes);
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
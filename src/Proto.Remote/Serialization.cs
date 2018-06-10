// -----------------------------------------------------------------------
//   <copyright file="Serialization.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace Proto.Remote
{
    public interface ISerializer
    {
        ByteString Serialize(object obj);
        object Deserialize(ByteString bytes, string typeName);
        string GetTypeName(object message);
    }

    public class JsonSerializer : ISerializer
    {
        public ByteString Serialize(object obj)
        {
            if (obj is JsonMessage jsonMessage)
            {
                return ByteString.CopyFromUtf8(jsonMessage.Json);
            }

            var message = obj as IMessage;
            var json = JsonFormatter.Default.Format(message);
            return ByteString.CopyFromUtf8(json);
        }

        public object Deserialize(ByteString bytes, string typeName)
        {
            var json = bytes.ToStringUtf8();
            var parser = Serialization.TypeLookup[typeName];

            var o = parser.ParseJson(json);
            return o;
        }

        public string GetTypeName(object obj)
        {
            if (obj is JsonMessage jsonMessage)
            {
                return jsonMessage.TypeName;
            }

            var message = obj as IMessage;
            if (message == null)
            {
                throw new ArgumentException("obj must be of type IMessage", nameof(obj));
            }
            return message.Descriptor.File.Package + "." + message.Descriptor.Name;
        }
    }

    public class ProtobufSerializer : ISerializer
    {
        public ByteString Serialize(object obj)
        {
            var message = obj as IMessage;
            return message.ToByteString();
        }

        public object Deserialize(ByteString bytes, string typeName)
        {
            var parser = Serialization.TypeLookup[typeName];
            var o = parser.ParseFrom(bytes);
            return o;
        }

        public string GetTypeName(object obj)
        {
            var message = obj as IMessage;
            if (message == null)
            {
                throw new ArgumentException("obj must be of type IMessage", nameof(obj));
            }
            return message.Descriptor.File.Package + "." + message.Descriptor.Name;
        }
    }

    public static class Serialization
    {
        internal static readonly Dictionary<string, MessageParser> TypeLookup = new Dictionary<string, MessageParser>();
        private static readonly List<ISerializer> Serializers = new List<ISerializer>();
        private static readonly ProtobufSerializer ProtobufSerializer = new ProtobufSerializer();
        private static readonly JsonSerializer JsonSerializer = new JsonSerializer();

        static Serialization()
        {
            RegisterFileDescriptor(Proto.ProtosReflection.Descriptor);
            RegisterFileDescriptor(ProtosReflection.Descriptor);
            RegisterSerializer(ProtobufSerializer,true);
            RegisterSerializer(JsonSerializer);
        }

        public static int DefaultSerializerId { get; set; }

        public static void RegisterSerializer(ISerializer serializer, bool makeDefault = false)
        {
            Serializers.Add(serializer);
            if (makeDefault)
            {
                DefaultSerializerId = Serializers.Count - 1;
            }
        }

        public static void RegisterFileDescriptor(FileDescriptor fd)
        {
            foreach (var msg in fd.MessageTypes)
            {
                var name = fd.Package + "." + msg.Name;
                TypeLookup.Add(name, msg.Parser);
            }
        }

        public static ByteString Serialize(object message,int serializerId) => Serializers[serializerId].Serialize(message);

        public static string GetTypeName(object message, int serializerId) => Serializers[serializerId].GetTypeName(message);

        public static object Deserialize(string typeName, ByteString bytes, int serializerId) => Serializers[serializerId].Deserialize(bytes, typeName);
    }
}
// -----------------------------------------------------------------------
//   <copyright file="Serialization.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

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

    public class ProtoBufSerializer : ISerializer
    {
        private readonly Dictionary<string, MessageParser> _typeLookup = new Dictionary<string, MessageParser>();

        public ProtoBufSerializer()
        {
            RegisterFileDescriptor(Proto.ProtosReflection.Descriptor);
            RegisterFileDescriptor(ProtosReflection.Descriptor);
        }

        public ByteString Serialize(object obj)
        {
            var message = obj as IMessage;
            return message.ToByteString();
        }

        public object Deserialize(ByteString bytes, string typeName)
        {
            var parser = _typeLookup[typeName];
            var o = parser.ParseFrom(bytes);
            return o;
        }

        public string GetTypeName(object obj)
        {
            var message = obj as IMessage;
            return message.Descriptor.File.Package + "." + message.Descriptor.Name;
        }

        public void RegisterFileDescriptor(FileDescriptor fd)
        {
            foreach (var msg in fd.MessageTypes)
            {
                var name = fd.Package + "." + msg.Name;
                _typeLookup.Add(name, msg.Parser);
            }
        }
    }

    public static class Serialization
    {
        private static readonly List<ISerializer> Serializers = new List<ISerializer>();
        private static readonly ProtoBufSerializer ProtoBufSerializer = new ProtoBufSerializer();

        static Serialization()
        {
            Serializers.Add(ProtoBufSerializer);
            DefaultSerializerId = 0;
        }

        public static int DefaultSerializerId { get; set; }

        //TODO: remove from this class and let users register on the protobuf serializer?
        public static void RegisterFileDescriptor(FileDescriptor fd)
        {
            ProtoBufSerializer.RegisterFileDescriptor(fd);
        }

        public static ByteString Serialize(object message,int serializerId)
        {
            return Serializers[serializerId].Serialize(message);
        }

        public static string GetTypeName(object message, int serializerId)
        {
            return Serializers[serializerId].GetTypeName(message);
        }

        public static object Deserialize(string typeName, ByteString bytes, int serializerId)
        {
            return Serializers[serializerId].Deserialize(bytes, typeName);
        }
    }
}
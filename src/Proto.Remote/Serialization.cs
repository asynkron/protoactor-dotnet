// -----------------------------------------------------------------------
//  <copyright file="Serialization.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace Proto.Remote
{
    public static class Serialization
    {
        private static readonly Dictionary<string, MessageParser> TypeLookup = new Dictionary<string, MessageParser>();

        static Serialization()
        {
            RegisterFileDescriptor(Proto.ProtosReflection.Descriptor);
            RegisterFileDescriptor(ProtosReflection.Descriptor);
        }

        public static void RegisterFileDescriptor(FileDescriptor fd)
        {
            foreach (var msg in fd.MessageTypes)
            {
                var name = fd.Package + "." + msg.Name;
                TypeLookup.Add(name, msg.Parser);
            }
        }

        public static ByteString Serialize(IMessage message)
        {
            return message.ToByteString();
        }

        public static object Deserialize(string typeName, ByteString bytes)
        {
            var parser = TypeLookup[typeName];
            var o = parser.ParseFrom(bytes);
            return o;
        }
    }
}
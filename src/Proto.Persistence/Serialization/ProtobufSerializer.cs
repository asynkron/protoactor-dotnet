// -----------------------------------------------------------------------
// <copyright file="ProtobufSerializer.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using Google.Protobuf;

namespace Proto.Persistence.Serialization
{
    public class ProtobufSerializer : IPersistentSerializer
    {
        private readonly Dictionary<string, MessageParser> _typeLookup = new();

        public ProtobufSerializer()
        {
        }

        public void RegisterType(string typename, MessageParser parser)
        {
            _typeLookup.Add(typename,parser);
        }

        public ReadOnlySpan<byte> Serialize(object value)
        {
            var message = value as IMessage;
            return message.ToByteArray();
        }

        public object Deserialize(ReadOnlySpan<byte> bytes, string typeName)
        {
            var parser = _typeLookup[typeName];
            var o = parser.ParseFrom(bytes.ToArray());
            return o;
        }

        public string GetTypeName(object obj)
        {
            if (obj is IMessage message)
                return $"{message.Descriptor.File.Package}.{message.Descriptor.Name}";

            throw new ArgumentException("obj must be of type IMessage", nameof(obj));
        }
    }
}
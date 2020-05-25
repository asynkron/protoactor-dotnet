using MessagePack;
using Proto.Serializer.MessagePack;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Messages
{
    [MessagePackId(0)]
    public class MsgPackPing : IMsgPackObject
    {
    }

    [MessagePackId(1)]
    public class MsgPackPong : IMsgPackObject
    {
    }

    public static class MsgPackSerializerCreator
    {
        public static ProtoMessagePackSerializer Create()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var types = ProtoMessagePackSerializer.ScanAssemblyForTypes(assembly);
            return new ProtoMessagePackSerializer(types);
        }
    }
}

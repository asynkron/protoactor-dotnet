using MessagePack;
using Proto.Serializer.MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace Messages
{
    [MessagePackObject]
    public class MsgPackPing : IMsgPackObject
    {
    }

    [MessagePackObject]
    public class MsgPackPong : IMsgPackObject
    {
    }

    public static class MsgPackSerializerCreator
    {
        public static ProtoMessagePackSerializer Create()
        {
            return new ProtoMessagePackSerializer(new Dictionary<int, Type>()
                {
                    { 0, typeof(MsgPackPing) },
                    { 1, typeof(MsgPackPong) },
                });
        }
    }
}

using MessagePack;
using System;

namespace Proto.Serializer.MessagePack
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
    public class MessagePackIdAttribute : MessagePackObjectAttribute
    {
        public int Id { get; }

        public MessagePackIdAttribute(int Id)
        {
            this.Id = Id;
        }
    }
}

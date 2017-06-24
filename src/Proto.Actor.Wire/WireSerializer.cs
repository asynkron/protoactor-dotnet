using System.Collections.Generic;
using System.IO;
using Google.Protobuf;
using Proto.Remote;
using Wire;

namespace Proto.Serialization
{
    public class WireSerializer : ISerializer
    {
        private readonly Serializer _serializer;

        public WireSerializer(IEnumerable<System.Type> knownTypes)
        {
            _serializer = new Serializer(new SerializerOptions(knownTypes: knownTypes));
        }
        
        public ByteString Serialize(object obj)
        {
            var ms = new MemoryStream();
            _serializer.Serialize(obj,ms);
            var arr = ms.ToArray();
            ms.Dispose();
            return ByteString.CopyFrom(arr);
        }

        public object Deserialize(ByteString bytes, string typeName)
        {
            var arr = bytes.ToByteArray();
            var ms = new MemoryStream(arr) {Position = 0};
            var obj = _serializer.Deserialize(ms);
            ms.Dispose();
            return obj;
        }

        public string GetTypeName(object message)
        {
            return "";
        }
    }
}

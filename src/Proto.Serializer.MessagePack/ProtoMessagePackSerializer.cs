using Google.Protobuf;
using Proto.Remote;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Proto.Serializer.MessagePack
{
    /// <summary>
    /// Derive from this to speed up
    /// ProtoMessagePackSerializer.CanSerialize function.
    /// </summary>
    public interface IMsgPackObject { }

    public class ProtoMessagePackSerializer : ISerializer
    {
        Dictionary<Type, string> typeToName = new Dictionary<Type, string>();
        Dictionary<string, Type> nameToType = new Dictionary<string, Type>();
        ConcurrentDictionary<Type, bool> canSerializeTypeMap = new ConcurrentDictionary<Type, bool>();

        public ProtoMessagePackSerializer(Dictionary<int, Type> idToType)
        {
            foreach (var item in idToType)
            {
                var idStr = $"{item.Key}";
                nameToType.Add(idStr, item.Value);
                typeToName.Add(item.Value, idStr);
            }
        }

        public bool CanSerialize(object obj)
        {
            if (obj is IMsgPackObject)
                return true;

            var type = obj.GetType();
            bool canSerialize;
            if (canSerializeTypeMap.TryGetValue(type, out canSerialize))
                return canSerialize;

            canSerialize = Attribute.GetCustomAttribute(
                obj.GetType(),
                typeof(global::MessagePack.MessagePackObjectAttribute)) != null;
            // Cache this type.
            canSerializeTypeMap[type] = canSerialize;
            return canSerialize;
        }

        public object Deserialize(ByteString bytes, string typeName)
        {
            if (nameToType.TryGetValue(typeName, out var serializedType))
            {
                // TODO: Prevent copying the bytes here with ToByteArray.
                return global::MessagePack.MessagePackSerializer.Deserialize(
                    serializedType,
                    bytes.ToByteArray());
            }
            throw new Exception($"Unknown typename: {typeName}");
        }

        public string GetTypeName(object message)
        {
            return typeToName[message.GetType()];
        }

        public ByteString Serialize(object obj)
        {
            return ByteString.CopyFrom(
                global::MessagePack.MessagePackSerializer.Serialize(obj.GetType(), obj));
        }
    }
}

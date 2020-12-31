// -----------------------------------------------------------------------
// <copyright file="ISerializer.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using Google.Protobuf;

namespace Proto.Remote
{
    public interface ISerializer
    {
        ByteString Serialize(object obj);

        object Deserialize(ByteString bytes, string typeName);

        string GetTypeName(object message);
    }
}
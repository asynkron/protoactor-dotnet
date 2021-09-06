// -----------------------------------------------------------------------
// <copyright file="ICachedSerialization.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using Google.Protobuf;

namespace Proto.Remote
{
    public interface ICachedSerialization
    {
        (ByteString bytes, string typename, int serializerId, bool boolHasData) SerializerData { get; set; }
    }
}
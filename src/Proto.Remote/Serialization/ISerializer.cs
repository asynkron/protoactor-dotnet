// -----------------------------------------------------------------------
// <copyright file="ISerializer.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using Google.Protobuf;

namespace Proto.Remote
{
    public interface ISerializer
    {
        ReadOnlySpan<byte> Serialize(object obj);
        object Deserialize(ReadOnlySpan<byte> bytes, string typeName);
        string GetTypeName(object message);
    }
}
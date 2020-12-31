// -----------------------------------------------------------------------
// <copyright file="IPersistentSerializer.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using JetBrains.Annotations;

namespace Proto.Persistence.Serialization
{
    [PublicAPI]
    public interface IPersistentSerializer
    {
        public object Deserialize(ReadOnlySpan<byte> bytes, string typeName);

        public ReadOnlySpan<byte> Serialize(object value);
    }
}
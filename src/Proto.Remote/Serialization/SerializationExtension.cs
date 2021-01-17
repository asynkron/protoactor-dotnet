// -----------------------------------------------------------------------
// <copyright file="SerializationExtension.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using Proto.Extensions;

namespace Proto.Remote
{
    public class SerializationExtension : ActorSystemExtension<SerializationExtension>
    {
        public Serialization Serialization { get; }
        public SerializationExtension(Serialization serialization)
        {
            Serialization = serialization;
        }
    }
}
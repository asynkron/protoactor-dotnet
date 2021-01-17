// -----------------------------------------------------------------------
// <copyright file="RemoteExtension.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using Proto.Extensions;

namespace Proto.Remote
{
    public class RemoteExtension : ActorSystemExtension<RemoteExtension>
    {
        public IRemote Remote { get; }
        public RemoteExtension(IRemote remote)
        {
            AddDependency<SerializationExtension>();
            Remote = remote;
        }
    }
}
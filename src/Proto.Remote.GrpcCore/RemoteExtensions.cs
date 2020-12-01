// -----------------------------------------------------------------------
// <copyright file="RemoteExtensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using JetBrains.Annotations;

namespace Proto.Remote.GrpcCore
{
    [PublicAPI]
    public static class RemoteExtensions
    {
        public static ActorSystem WithRemote(this ActorSystem system, GrpcCoreRemoteConfig remoteConfig)
        {
            _ = new GrpcCoreRemote(system, remoteConfig);
            return system;
        }
    }
}
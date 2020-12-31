// -----------------------------------------------------------------------
//   <copyright file="GrpcRemoteConfig.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using Grpc.Core;

namespace Proto.Remote.GrpcCore
{
    public static class RemoteConfigExtensions
    {
        public static GrpcCoreRemoteConfig WithChannelOptions(this GrpcCoreRemoteConfig remoteConfig, IEnumerable<ChannelOption> options) =>
            remoteConfig with {ChannelOptions = options};

        public static GrpcCoreRemoteConfig WithChannelCredentials(this GrpcCoreRemoteConfig remoteConfig, ChannelCredentials channelCredentials) =>
            remoteConfig with {ChannelCredentials = channelCredentials};

        public static GrpcCoreRemoteConfig WithServerCredentials(this GrpcCoreRemoteConfig remoteConfig, ServerCredentials serverCredentials) =>
            remoteConfig with {ServerCredentials = serverCredentials};
    }
}
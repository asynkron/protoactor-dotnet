// -----------------------------------------------------------------------
//   <copyright file="GrpcRemoteConfig.cs" company="Asynkron AB">
//       Copyright (C) 2015-2022 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Grpc.Core;
using JetBrains.Annotations;

namespace Proto.Remote.GrpcCore
{
    [PublicAPI]
    public static class RemoteConfigExtensions
    {
        [Obsolete(ObsoleteInformation.Text)]
        public static GrpcCoreRemoteConfig WithChannelOptions(this GrpcCoreRemoteConfig remoteConfig, IEnumerable<ChannelOption> options) =>
            remoteConfig with {ChannelOptions = options};

        [Obsolete(ObsoleteInformation.Text)]
        public static GrpcCoreRemoteConfig WithChannelCredentials(this GrpcCoreRemoteConfig remoteConfig, ChannelCredentials channelCredentials) =>
            remoteConfig with {ChannelCredentials = channelCredentials};

        [Obsolete(ObsoleteInformation.Text)]
        public static GrpcCoreRemoteConfig WithServerCredentials(this GrpcCoreRemoteConfig remoteConfig, ServerCredentials serverCredentials) =>
            remoteConfig with {ServerCredentials = serverCredentials};
    }
}
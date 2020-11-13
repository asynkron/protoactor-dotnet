// -----------------------------------------------------------------------
//   <copyright file="GrpcRemoteConfig.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using Grpc.Core;

namespace Proto.Remote
{
    public static class RemoteConfigExtensions
    {
        public static RemoteConfig WithChannelOptions(this RemoteConfig remoteConfig, IEnumerable<ChannelOption> options) =>
            remoteConfig with { ChannelOptions = options };

        public static RemoteConfig WithChannelCredentials(this RemoteConfig remoteConfig, ChannelCredentials channelCredentials) =>
            remoteConfig with { ChannelCredentials = channelCredentials };

        public static RemoteConfig WithServerCredentials(this RemoteConfig remoteConfig, ServerCredentials serverCredentials) =>
            remoteConfig with { ServerCredentials = serverCredentials };

    }
}
// -----------------------------------------------------------------------
//   <copyright file="ChannelProvider.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Grpc.Core;

namespace Proto.Remote
{
    public class ChannelProvider : IChannelProvider
    {
        private readonly GrpcRemoteConfig _remoteConfig;

        public ChannelProvider(GrpcRemoteConfig remoteConfig)
        {
            _remoteConfig = remoteConfig;
        }

        public ChannelBase GetChannel(string address)
        {
            return new Channel(address, _remoteConfig.ChannelCredentials, _remoteConfig.ChannelOptions);
        }
    }
}
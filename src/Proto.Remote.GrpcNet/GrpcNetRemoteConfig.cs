// -----------------------------------------------------------------------
//   <copyright file="GrpcNetRemoteConfig.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using Grpc.Net.Client;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Proto.Remote.GrpcNet
{
    [PublicAPI]
    public record GrpcNetRemoteConfig : RemoteConfigBase
    {
        protected GrpcNetRemoteConfig(string host, int port) : base(host, port)
        {

        }
        public static GrpcNetRemoteConfig BindToAllInterfaces(string advertisedHost, int port = 0) =>
            new GrpcNetRemoteConfig(AllInterfaces, port).WithAdvertisedHost(advertisedHost);
        
        public static GrpcNetRemoteConfig BindToLocalhost(int port = 0) => new GrpcNetRemoteConfig(Localhost, port);
        
        public static GrpcNetRemoteConfig BindTo(string host, int port = 0) => new GrpcNetRemoteConfig(host, port);
        public bool UseHttps { get; init; }
        public GrpcChannelOptions ChannelOptions { get; init; } = new GrpcChannelOptions();
        public Action<ListenOptions>? ConfigureKestrel { get; init; }
    }
}
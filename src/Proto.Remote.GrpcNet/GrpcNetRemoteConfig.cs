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

        public bool UseHttps { get; init; }
        public GrpcChannelOptions ChannelOptions { get; init; } = new();
        public Action<ListenOptions>? ConfigureKestrel { get; init; }

        public static GrpcNetRemoteConfig BindToAllInterfaces(string? advertisedHost = null, int port = 0) =>
            new GrpcNetRemoteConfig(AllInterfaces, port).WithAdvertisedHost(advertisedHost);

        public static GrpcNetRemoteConfig BindToLocalhost(int port = 0) => new(Localhost, port);

        public static GrpcNetRemoteConfig BindTo(string host, int port = 0) => new(host, port);
    }
}
// -----------------------------------------------------------------------
//   <copyright file="GrpcNetRemoteConfig.cs" company="Asynkron AB">
//       Copyright (C) 2015-2022 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
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

        public Func<IEnumerable<Uri>?, Uri?> UriChooser { get; init; } = uris => uris?.FirstOrDefault();

        public static GrpcNetRemoteConfig BindToAllInterfaces(string? advertisedHost = null, int port = 0) =>
            new GrpcNetRemoteConfig(AllInterfaces, port).WithAdvertisedHost(advertisedHost);

        public static GrpcNetRemoteConfig BindToLocalhost(int port = 0) => new(Localhost, port);

        public static GrpcNetRemoteConfig BindTo(string host, int port = 0) => new(host, port);
    }
}